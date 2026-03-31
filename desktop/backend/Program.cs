using System.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Win32;
using NeoWallet.Backend;
using NeoWallet.Backend.Endpoints;

// ── Protocol handler: forward ESR URI to running instance ──────────
// When launched via esr:// protocol, the OS passes the URI as an arg.
// If another instance is already running, POST the URI there and exit.
var esrArg = args.FirstOrDefault(a => a.StartsWith("esr:", StringComparison.OrdinalIgnoreCase));
if (esrArg is not null)
{
    using var probe = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
    try
    {
        var resp = await probe.PostAsync(
            $"http://localhost:5199/api/esr/incoming?uri={Uri.EscapeDataString(esrArg)}",
            null);
        if (resp.IsSuccessStatusCode) return; // Running instance accepted it
    }
    catch { /* No running instance — continue booting normally */ }
}

// ── Build the ASP.NET application ──────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBackendServices();

// Resolve the frontend directory.
// Published builds: wwwroot/ next to the exe (single-file needs exe path, not AppContext).
// Dev builds:       ../app/dist/ relative to the project.
var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
var publishedWwwroot = Path.Combine(exeDir, "wwwroot");
var appContextWwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
var devDist = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "app", "dist"));
var frontendDir = Directory.Exists(publishedWwwroot) ? publishedWwwroot
                : Directory.Exists(appContextWwwroot) ? appContextWwwroot
                : Directory.Exists(devDist)           ? devDist
                : null;

if (frontendDir is not null)
{
    builder.Environment.WebRootPath = frontendDir;
    builder.Services.AddSingleton<IWebHostEnvironment>(sp =>
    {
        var env = sp.GetRequiredService<IHostEnvironment>();
        return builder.Environment;
    });
}

var app = builder.Build();

// ── Trace logging to file (WinExe has no console) ──────────────────
var logDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "NeoWallet");
Directory.CreateDirectory(logDir);
var logFile = Path.Combine(logDir, "backend.log");
Trace.Listeners.Add(new TextWriterTraceListener(logFile) { TraceOutputOptions = TraceOptions.DateTime });
Trace.AutoFlush = true;
Trace.WriteLine($"[BACKEND] ===== Starting at {DateTime.UtcNow:O} =====");

app.UseWebSockets();
app.UseMiddleware<BearerTokenMiddleware>();

if (frontendDir is not null)
{
    var fileProvider = new PhysicalFileProvider(frontendDir);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = fileProvider,
        OnPrepareResponse = ctx =>
        {
            // HTML files must never be cached (they reference hashed JS/CSS).
            // Hashed assets (in /assets/) can be cached indefinitely.
            if (ctx.File.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
                ctx.Context.Response.Headers.Pragma = "no-cache";
            }
            else if (ctx.Context.Request.Path.StartsWithSegments("/assets"))
            {
                ctx.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            }
        },
    });
}

app.MapHealthEndpoints();
app.MapWalletEndpoints();
app.MapAccountEndpoints();
app.MapKeyEndpoints();
app.MapNetworkEndpoints();
app.MapBalanceEndpoints();
app.MapTransferEndpoints();
app.MapEsrEndpoints();
app.MapSettingsEndpoints();

// Unauthenticated endpoint for esr:// protocol handler invocations.
// Accepts an ESR URI, parses it, stores it, and pushes a WebSocket event.
app.MapPost("/api/esr/incoming", async (HttpContext ctx, IServiceProvider sp) =>
{
    var uri = ctx.Request.Query["uri"].ToString();
    if (string.IsNullOrWhiteSpace(uri))
        return Results.BadRequest("Missing uri");

    Trace.WriteLine($"[ESR] Incoming protocol URI: {uri}");

    var esrService = sp.GetRequiredService<SUS.EOS.EosioSigningRequest.Services.IEsrService>();
    var listener = sp.GetRequiredService<NeoWallet.Backend.Services.EsrListenerService>();

    try
    {
        var esr = await esrService.ParseRequestAsync(uri);
        var requestId = Guid.NewGuid().ToString("N");
        listener.PendingRequests[requestId] = (esr, null);

        // Build the same payload shape the frontend expects
        var actions = new List<object>();
        if (esr.Payload.IsAction && esr.Payload.Action is not null)
        {
            var t = esr.Payload.Action.GetType();
            var acct = t.GetProperty("account")?.GetValue(esr.Payload.Action) as string ?? "?";
            var name = t.GetProperty("name")?.GetValue(esr.Payload.Action) as string ?? "?";
            actions.Add(new { account = acct, name });
        }

        var payload = new
        {
            type = "signing_request",
            requestId,
            isIdentity = !esr.Payload.IsTransaction && !esr.Payload.IsAction,
            chainId = esr.ChainId ?? "",
            actions,
            session = (object?)null,
            callbackUrl = esr.Callback,
            rawPayload = uri,
        };

        listener.BroadcastSigningRequestEvent(payload);
        Trace.WriteLine($"[ESR] Stored & broadcast incoming ESR, requestId={requestId}");
        return Results.Ok(new { requestId });
    }
    catch (Exception ex)
    {
        Trace.WriteLine($"[ESR] Failed to process incoming URI: {ex.Message}");
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
    }
});

// SPA fallback: non-API requests serve index.html.
if (frontendDir is not null)
{
    app.MapFallbackToFile("index.html", new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(frontendDir),
        OnPrepareResponse = ctx =>
        {
            ctx.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            ctx.Context.Response.Headers.Pragma = "no-cache";
        },
    });
}

var port = app.Configuration.GetValue("Port", 5199);
var listenUrl = $"http://localhost:{port}";

// ── Register esr:// protocol handler ───────────────────────────────
RegisterEsrProtocol();

// If we were launched with an esr:// URI (no running instance to forward to),
// schedule it for processing once the server is ready.
var pendingEsrUri = esrArg;

System.Diagnostics.Trace.WriteLine($"[BACKEND] Starting on {listenUrl}");

if (frontendDir is not null)
{
    System.Diagnostics.Trace.WriteLine($"[BACKEND] Serving frontend from {frontendDir}");
}
else
{
    Console.WriteLine("WARNING: No frontend build found. Run 'npm run build' in desktop/app first.");
}

// ── Launch mode ────────────────────────────────────────────────────
//   Default    → native app window (Edge/Chrome --app mode, no URL bar)
//   --browser  → open default browser
//   --headless → no UI (Tauri/Capacitor sidecar or server mode)

var useBrowser = args.Contains("--browser");
var useHeadless = args.Contains("--headless");

if (useHeadless)
{
    Console.WriteLine($"Backend API listening on {listenUrl}");
    app.Run(listenUrl);
}
else if (useBrowser)
{
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        try
        {
            Process.Start(new ProcessStartInfo(listenUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[BACKEND] Could not open browser: {ex.Message}");
        }
    });
    Console.WriteLine($"Backend API listening on {listenUrl}");
    app.Run(listenUrl);
}
else
{
    // ── App window via Edge / Chrome ────────────────────────────────
    // Launches a Chromium browser in "app mode" (--app=URL) which renders
    // the frontend in a borderless window without URL bar, tabs, or
    // browser chrome — looks and behaves like a native desktop app.
    // When the window is closed the backend stays alive in the system tray
    // so the ESR listener continues to operate.

    Console.WriteLine($"Backend API listening on {listenUrl}");

    // Start Kestrel without blocking.
#pragma warning disable CS4014 // Fire-and-forget is intentional; the tray icon owns the main thread
    app.RunAsync(listenUrl);
#pragma warning restore CS4014

    // Wait for the server to be ready before opening the window.
    WaitForServer(listenUrl, timeout: TimeSpan.FromSeconds(10));

    // If we were launched with a pending esr:// URI, forward it to ourselves now.
    if (pendingEsrUri is not null)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                await http.PostAsync(
                    $"{listenUrl}/api/esr/incoming?uri={Uri.EscapeDataString(pendingEsrUri)}",
                    null);
                Trace.WriteLine($"[BACKEND] Forwarded startup ESR URI to self");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[BACKEND] Failed to forward startup ESR URI: {ex.Message}");
            }
        });
    }

    var browserExe = FindChromiumBrowser();
    var profileDir = browserExe is not null
        ? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NeoWallet", "browser-profile")
        : null;

    // ── System tray (Windows) ──────────────────────────────────────
    NeoWallet.Backend.Services.TrayIconService? tray = null;

    if (OperatingSystem.IsWindows())
    {
        tray = new NeoWallet.Backend.Services.TrayIconService();

        tray.OpenRequested += () =>
        {
            LaunchBrowserWindow(browserExe, listenUrl, profileDir);
        };

        tray.ExitRequested += () =>
        {
            Trace.WriteLine("[BACKEND] Exit requested from tray — shutting down.");
            _ = app.StopAsync();
        };

        tray.Start();
    }

    // Open the initial browser window.
    LaunchBrowserWindow(browserExe, listenUrl, profileDir);

    // Block on the ASP.NET host — exits when tray Exit is clicked or app is stopped.
    app.WaitForShutdown();

    tray?.Dispose();
}

// ── Helper methods ─────────────────────────────────────────────────

static void LaunchBrowserWindow(string? browserExe, string listenUrl, string? profileDir)
{
    if (browserExe is not null && profileDir is not null)
    {
        Directory.CreateDirectory(profileDir);

        // Purge the browser disk cache so the latest frontend build is always loaded.
        foreach (var cacheDir in new[]
        {
            Path.Combine(profileDir, "Default", "Cache"),
            Path.Combine(profileDir, "Default", "Code Cache"),
        })
        {
            if (Directory.Exists(cacheDir))
            {
                try { Directory.Delete(cacheDir, recursive: true); }
                catch (Exception ex) { Trace.WriteLine($"[BACKEND] Cache purge {cacheDir}: {ex.Message}"); }
            }
        }

        Trace.WriteLine($"[BACKEND] Opening app window via {Path.GetFileName(browserExe)}");

        var psi = new ProcessStartInfo(browserExe)
        {
            ArgumentList =
            {
                $"--app={listenUrl}",
                $"--user-data-dir={profileDir}",
                "--disable-extensions",
                "--disable-features=PasswordManager,TranslateUI",
                "--disable-dev-tools",
                "--disk-cache-size=1",
                "--new-window",
            },
            UseShellExecute = false,
        };

        Process.Start(psi);
    }
    else
    {
        // Fallback: no Chromium browser found — open default browser.
        try
        {
            Process.Start(new ProcessStartInfo(listenUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[BACKEND] Could not open browser: {ex.Message}");
        }
    }
}

static void WaitForServer(string url, TimeSpan timeout)
{
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
    var deadline = DateTime.UtcNow + timeout;
    while (DateTime.UtcNow < deadline)
    {
        try
        {
            var resp = http.GetAsync($"{url}/api/health").Result;
            if (resp.IsSuccessStatusCode) return;
        }
        catch { /* server not ready yet */ }
        Thread.Sleep(250);
    }
}

static string? FindChromiumBrowser()
{
    if (OperatingSystem.IsWindows())
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Google", "Chrome", "Application", "chrome.exe"),
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    if (OperatingSystem.IsMacOS())
    {
        var candidates = new[]
        {
            "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge",
            "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
            "/Applications/Chromium.app/Contents/MacOS/Chromium",
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    // Linux: look for common Chromium-based browsers in PATH.
    string[] names = ["microsoft-edge-stable", "google-chrome-stable",
                      "google-chrome", "chromium-browser", "chromium"];
    foreach (var name in names)
    {
        try
        {
            var proc = Process.Start(new ProcessStartInfo("which", name)
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
            });
            var path = proc?.StandardOutput.ReadToEnd().Trim();
            proc?.WaitForExit();
            if (proc?.ExitCode == 0 && !string.IsNullOrEmpty(path))
                return path;
        }
        catch { /* not found */ }
    }

    return null;
}

/// <summary>
/// Register esr:// as a custom protocol handler so the OS launches this
/// executable when a browser navigates to an esr:// URI.
/// On Windows this writes to HKCU (no admin required).
/// </summary>
static void RegisterEsrProtocol()
{
    if (!OperatingSystem.IsWindows()) return;

    try
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath)) return;

        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\esr");
        key.SetValue("", "URL:ESR Signing Request");
        key.SetValue("URL Protocol", "");

        using var iconKey = key.CreateSubKey("DefaultIcon");
        iconKey.SetValue("", $"\"{exePath}\",0");

        using var commandKey = key.CreateSubKey(@"shell\open\command");
        commandKey.SetValue("", $"\"{exePath}\" \"%1\"");

        Trace.WriteLine("[BACKEND] Registered esr:// protocol handler");
    }
    catch (Exception ex)
    {
        Trace.WriteLine($"[BACKEND] Failed to register esr:// protocol: {ex.Message}");
    }
}

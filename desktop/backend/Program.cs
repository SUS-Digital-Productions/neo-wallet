using Microsoft.Extensions.FileProviders;
using NeoWallet.Backend;
using NeoWallet.Backend.Endpoints;
using PhotinoWindow = Photino.NET.PhotinoWindow;

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
}

var app = builder.Build();

app.UseMiddleware<BearerTokenMiddleware>();

if (frontendDir is not null)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.MapHealthEndpoints();
app.MapWalletEndpoints();
app.MapAccountEndpoints();
app.MapNetworkEndpoints();
app.MapBalanceEndpoints();
app.MapTransferEndpoints();
app.MapEsrEndpoints();
app.MapSettingsEndpoints();

// SPA fallback: non-API requests serve index.html.
if (frontendDir is not null)
{
    app.MapFallbackToFile("index.html");
}

var port = app.Configuration.GetValue("Port", 5199);
var listenUrl = $"http://localhost:{port}";
var useBrowser = args.Contains("--browser");

System.Diagnostics.Trace.WriteLine($"[BACKEND] Starting on {listenUrl}");

if (frontendDir is not null)
{
    System.Diagnostics.Trace.WriteLine($"[BACKEND] Serving frontend from {frontendDir}");
}
else
{
    Console.WriteLine("WARNING: No frontend build found. Run 'npm run build' in desktop/app first.");
}

if (!useBrowser && frontendDir is not null)
{
    // Native window mode (default): start Kestrel in background, open Photino window on main thread.
    app.Urls.Add(listenUrl);

    var serverReady = new ManualResetEventSlim(false);
    var cts = new CancellationTokenSource();
    var appSettings = app.Services.GetRequiredService<NeoWallet.Backend.Services.AppSettingsService>();

    app.Lifetime.ApplicationStarted.Register(() => serverReady.Set());
    _ = Task.Run(async () => await app.RunAsync(cts.Token));

    serverReady.Wait(TimeSpan.FromSeconds(10));

    Console.WriteLine($"Neo Wallet running at {listenUrl}");

    new PhotinoWindow()
        .SetTitle("Neo Wallet")
        .SetUseOsDefaultSize(false)
        .SetSize(new System.Drawing.Size(1280, 860))
        .Center()
        .SetResizable(true)
        .Load(listenUrl)
        .WaitForClose();

    // Window closed — if minimizeToTray is enabled, keep backend alive
    if (appSettings.MinimizeToTray)
    {
        Console.WriteLine("Window closed. Backend still running (minimize to tray). Press Ctrl+C to exit.");
        var exitSignal = new ManualResetEventSlim(false);
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; exitSignal.Set(); };
        exitSignal.Wait();
    }

    cts.Cancel();
}
else
{
    // Browser mode: auto-open browser, keep running until Ctrl+C.
    if (frontendDir is not null && !args.Contains("--no-browser"))
    {
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            try
            {
                var browserUrl = listenUrl;
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(browserUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[BACKEND] Could not open browser: {ex.Message}");
            }
        });
    }

    Console.WriteLine($"Open {listenUrl} in your browser");
    app.Run(listenUrl);
}

using System.ComponentModel;
using System.Diagnostics;

namespace NeoWallet.Backend;

/// <summary>
/// Manages the neo.wallet hostname entry in the system hosts file.
/// One-time setup that maps neo.wallet → 127.0.0.1 so the wallet
/// is accessible at http://neo.wallet:5199 instead of localhost.
/// </summary>
internal static class HostsFileSetup
{
    public const string Hostname = "neo.wallet";

    /// <summary>
    /// Checks if the hosts file already contains a neo.wallet entry.
    /// </summary>
    public static bool IsConfigured()
    {
        try
        {
            var hostsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "drivers", "etc", "hosts");

            if (!File.Exists(hostsPath)) return false;

            return File.ReadAllText(hostsPath)
                .Contains("neo.wallet", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Attempts to add 127.0.0.1 neo.wallet to the hosts file.
    /// Shows a UAC prompt on Windows (first run only). Returns true if configured.
    /// </summary>
    public static bool TrySetup()
    {
        if (IsConfigured()) return true;
        if (!OperatingSystem.IsWindows()) return false;

        try
        {
            Trace.WriteLine("[BACKEND] neo.wallet not found in hosts file, attempting setup...");
            Console.WriteLine("Setting up neo.wallet hostname (administrator approval required)...");

            var scriptPath = Path.Combine(
                Path.GetTempPath(),
                $"neo-wallet-hosts-{Guid.NewGuid():N}.ps1");

            File.WriteAllText(scriptPath, """
                $hostsFile = Join-Path $env:SystemRoot 'System32\drivers\etc\hosts'
                $content = Get-Content $hostsFile -Raw -ErrorAction SilentlyContinue
                if ($content -and $content -match 'neo\.wallet') { exit 0 }
                Add-Content -Path $hostsFile -Value "`r`n127.0.0.1 neo.wallet" -Encoding ASCII
                """);

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                Verb = "runas",
                UseShellExecute = true,
            };

            using var proc = Process.Start(psi);
            proc?.WaitForExit(15_000);

            try { File.Delete(scriptPath); } catch { /* cleanup is best-effort */ }

            var success = IsConfigured();
            if (success)
            {
                Console.WriteLine("neo.wallet hostname configured.");
                Trace.WriteLine("[BACKEND] neo.wallet hostname configured successfully");
            }
            else
            {
                Console.WriteLine("Hostname setup did not complete. Using localhost.");
                Trace.WriteLine("[BACKEND] neo.wallet hostname setup did not complete");
            }

            return success;
        }
        catch (Win32Exception)
        {
            // UAC was denied by the user
            Console.WriteLine("Hostname setup skipped. Using localhost.");
            Trace.WriteLine("[BACKEND] neo.wallet hostname setup skipped (UAC denied)");
            return false;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[BACKEND] Hosts file setup failed: {ex.Message}");
            return false;
        }
    }
}

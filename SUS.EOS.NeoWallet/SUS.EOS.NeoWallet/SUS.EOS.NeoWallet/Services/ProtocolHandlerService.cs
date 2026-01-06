using SUS.EOS.NeoWallet.Services.Interfaces;
using System.Runtime.InteropServices;

#if WINDOWS
using Microsoft.Win32;
#endif

namespace SUS.EOS.NeoWallet.Services;

/// <summary>
/// Service to register and manage protocol handlers (esr://, anchor://)
/// </summary>
public class ProtocolHandlerService : IProtocolHandlerService
{
    private const string EsrProtocol = "esr";
    private const string AnchorProtocol = "anchor";

    public void RegisterProtocolHandlers()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            System.Diagnostics.Trace.WriteLine("[PROTOCOL] Protocol registration only supported on Windows");
            return;
        }

        try
        {
            System.Diagnostics.Trace.WriteLine("[PROTOCOL] Registering protocol handlers...");

            var exePath = Environment.ProcessPath ?? string.Empty;
            if (string.IsNullOrEmpty(exePath))
            {
                System.Diagnostics.Trace.WriteLine("[PROTOCOL] ERROR: Cannot determine executable path");
                return;
            }

            System.Diagnostics.Trace.WriteLine($"[PROTOCOL] Executable: {exePath}");

            // Register esr:// protocol
            RegisterProtocolWindows(EsrProtocol, "EOSIO Signing Request", exePath);
            
            // Register anchor:// protocol
            RegisterProtocolWindows(AnchorProtocol, "Anchor Link", exePath);

            System.Diagnostics.Trace.WriteLine("[PROTOCOL] Protocol handlers registered successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[PROTOCOL] Failed to register handlers: {ex.Message}");
        }
    }

    public bool IsDefaultHandler(string protocol)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;

        return IsDefaultHandlerWindows(protocol);
    }

    public void UnregisterProtocolHandlers()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        try
        {
            System.Diagnostics.Trace.WriteLine("[PROTOCOL] Unregistering protocol handlers...");

            UnregisterProtocolWindows(EsrProtocol);
            UnregisterProtocolWindows(AnchorProtocol);

            System.Diagnostics.Trace.WriteLine("[PROTOCOL] Protocol handlers unregistered");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[PROTOCOL] Failed to unregister handlers: {ex.Message}");
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private void RegisterProtocolWindows(string protocol, string description, string exePath)
    {
        try
        {
            // HKEY_CURRENT_USER\Software\Classes\protocol
            using var protocolKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey($@"Software\Classes\{protocol}");
            protocolKey.SetValue(string.Empty, $"URL:{description}");
            protocolKey.SetValue("URL Protocol", string.Empty);

            // HKEY_CURRENT_USER\Software\Classes\protocol\DefaultIcon
            using var iconKey = protocolKey.CreateSubKey("DefaultIcon");
            iconKey.SetValue(string.Empty, $"\"{exePath}\",0");

            // HKEY_CURRENT_USER\Software\Classes\protocol\shell\open\command
            using var commandKey = protocolKey.CreateSubKey(@"shell\open\command");
            commandKey.SetValue(string.Empty, $"\"{exePath}\" \"%1\"");

            System.Diagnostics.Trace.WriteLine($"[PROTOCOL] Registered {protocol}:// handler");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[PROTOCOL] Failed to register {protocol}: {ex.Message}");
            throw;
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private bool IsDefaultHandlerWindows(string protocol)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey($@"Software\Classes\{protocol}\shell\open\command");
            if (key == null)
                return false;

            var value = key.GetValue(string.Empty) as string;
            var exePath = Environment.ProcessPath ?? string.Empty;
            
            return value?.Contains(exePath) ?? false;
        }
        catch
        {
            return false;
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private void UnregisterProtocolWindows(string protocol)
    {
        try
        {
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{protocol}", false);
            System.Diagnostics.Trace.WriteLine($"[PROTOCOL] Unregistered {protocol}:// handler");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[PROTOCOL] Failed to unregister {protocol}: {ex.Message}");
        }
    }
}

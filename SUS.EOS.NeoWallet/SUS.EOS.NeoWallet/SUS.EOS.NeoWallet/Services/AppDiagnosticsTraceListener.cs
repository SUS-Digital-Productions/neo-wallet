using System.Diagnostics;
using SUS.EOS.NeoWallet.Services.Interfaces;

namespace SUS.EOS.NeoWallet.Services;

public class AppDiagnosticsTraceListener : TraceListener
{
    private readonly IAppDiagnosticsService _diagnostics;

    public AppDiagnosticsTraceListener(IAppDiagnosticsService diagnostics)
    {
        _diagnostics = diagnostics;
    }

    public override void Write(string? message)
    {
        // Split by lines and log
        if (string.IsNullOrEmpty(message)) return;
        foreach (var line in message.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
        {
            _diagnostics.Log("TRACE", line.Trim());
        }
    }

    public override void WriteLine(string? message)
    {
        Write(message);
    }
}
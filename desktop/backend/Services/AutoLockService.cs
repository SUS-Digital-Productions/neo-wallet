using System.Diagnostics;

namespace NeoWallet.Backend.Services;

/// <summary>
/// Background service that auto-locks the wallet after a configurable inactivity timeout.
/// </summary>
public sealed class AutoLockService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);

    private readonly IWalletStateService _wallet;
    private readonly AppSettingsService _settings;
    private DateTime _lastActivity = DateTime.UtcNow;

    public AutoLockService(IWalletStateService wallet, AppSettingsService settings)
    {
        _wallet = wallet;
        _settings = settings;
    }

    /// <summary>Update the activity timestamp. Called by middleware on each authenticated request.</summary>
    public void Touch() => _lastActivity = DateTime.UtcNow;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Trace.WriteLine($"[AUTOLOCK] Started, timeout = {_settings.AutoLockMinutes} min");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(CheckInterval, stoppingToken);

            var timeout = _settings.AutoLockMinutes;
            if (timeout <= 0) continue; // 0 = disabled
            if (!_wallet.WalletUnlocked) continue;

            var elapsed = DateTime.UtcNow - _lastActivity;
            if (elapsed.TotalMinutes >= timeout)
            {
                _wallet.Lock();
                Trace.WriteLine($"[AUTOLOCK] Wallet locked after {elapsed.TotalMinutes:F0} min inactivity");
            }
        }
    }
}

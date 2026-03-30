using System.Diagnostics;
using System.Text.Json;

namespace NeoWallet.Backend.Services;

/// <summary>
/// Manages persisted application settings (auto-lock, startup, tray).
/// </summary>
public sealed class AppSettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NeoWallet", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private int _autoLockMinutes = 180;
    private bool _startAtLogin;
    private bool _minimizeToTray;

    public int AutoLockMinutes
    {
        get => _autoLockMinutes;
        set { _autoLockMinutes = value < 0 ? 0 : value; Save(); }
    }

    public bool StartAtLogin
    {
        get => _startAtLogin;
        set
        {
            _startAtLogin = value;
            ApplyStartAtLogin(value);
            Save();
        }
    }

    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set { _minimizeToTray = value; Save(); }
    }

    public AppSettingsService()
    {
        Load();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var json = File.ReadAllText(SettingsPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("autoLockMinutes", out var lockVal))
                _autoLockMinutes = lockVal.GetInt32();
            if (root.TryGetProperty("startAtLogin", out var startVal))
                _startAtLogin = startVal.GetBoolean();
            if (root.TryGetProperty("minimizeToTray", out var trayVal))
                _minimizeToTray = trayVal.GetBoolean();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[SETTINGS] Failed to load: {ex.Message}");
        }
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(new
            {
                autoLockMinutes = _autoLockMinutes,
                startAtLogin = _startAtLogin,
                minimizeToTray = _minimizeToTray,
            }, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[SETTINGS] Failed to save: {ex.Message}");
        }
    }

    private static void ApplyStartAtLogin(bool enabled)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath)) return;

            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key is null) return;

            if (enabled)
            {
                key.SetValue("NeoWallet", $"\"{exePath}\"");
                Trace.WriteLine("[SETTINGS] Added startup registry entry");
            }
            else
            {
                key.DeleteValue("NeoWallet", throwOnMissingValue: false);
                Trace.WriteLine("[SETTINGS] Removed startup registry entry");
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[SETTINGS] Failed to update startup: {ex.Message}");
        }
    }
}

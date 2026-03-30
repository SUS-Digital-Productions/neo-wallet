using System.Text.Json.Serialization;

namespace NeoWallet.Backend.Dto;

public sealed record AutoLockSettingsDto(
    [property: JsonPropertyName("timeoutMinutes")] int TimeoutMinutes
);

public sealed record AppSettingsDto(
    [property: JsonPropertyName("startAtLogin")] bool StartAtLogin,
    [property: JsonPropertyName("minimizeToTray")] bool MinimizeToTray
);

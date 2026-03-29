using System.Text.Json.Serialization;

namespace NeoWallet.Backend.Dto;

/// <summary>Backend readiness response.</summary>
public sealed record HealthDto(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("walletLoaded")] bool WalletLoaded,
    [property: JsonPropertyName("walletUnlocked")] bool WalletUnlocked
);

/// <summary>Summary of the active wallet state.</summary>
public sealed record WalletSummaryDto(
    [property: JsonPropertyName("activeNetwork")] NetworkDto? ActiveNetwork,
    [property: JsonPropertyName("activeAccount")] AccountDto? ActiveAccount,
    [property: JsonPropertyName("listenerStatus")] string ListenerStatus
);

/// <summary>Create a new wallet.</summary>
public sealed record CreateWalletRequest(
    [property: JsonPropertyName("password")] string Password
);

/// <summary>Unlock the wallet.</summary>
public sealed record UnlockRequest(
    [property: JsonPropertyName("password")] string Password
);

public sealed record UnlockResponse(
    [property: JsonPropertyName("unlocked")] bool Unlocked
);

/// <summary>Import an account by private key.</summary>
public sealed record ImportAccountRequest(
    [property: JsonPropertyName("privateKey")] string PrivateKey,
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("authority")] string Authority,
    [property: JsonPropertyName("password")] string Password
);

/// <summary>Remove an account.</summary>
public sealed record RemoveAccountRequest(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("authority")] string Authority,
    [property: JsonPropertyName("password")] string Password
);

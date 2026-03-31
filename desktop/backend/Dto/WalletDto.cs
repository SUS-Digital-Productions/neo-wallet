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
    [property: JsonPropertyName("unlocked")] bool Unlocked,
    [property: JsonPropertyName("token")] string? Token = null
);

/// <summary>Import accounts by private key (wallet must be unlocked).</summary>
public sealed record ImportAccountRequest(
    [property: JsonPropertyName("privateKey")] string PrivateKey,
    [property: JsonPropertyName("accounts")] ImportAccountEntry[] Accounts
);

/// <summary>Request to view a private key.</summary>
public sealed record GetPrivateKeyRequest(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("authority")] string Authority
);

/// <summary>Response with a private key.</summary>
public sealed record GetPrivateKeyResponse(
    [property: JsonPropertyName("privateKey")] string PrivateKey
);

/// <summary>Import a wallet file.</summary>
public sealed record ImportWalletRequest(
    [property: JsonPropertyName("password")] string Password,
    [property: JsonPropertyName("fileBase64")] string FileBase64
);

/// <summary>Remove an account.</summary>
public sealed record RemoveAccountRequest(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("authority")] string Authority,
    [property: JsonPropertyName("chainId")] string ChainId
);

/// <summary>A stored key visible to the frontend.</summary>
public sealed record KeyDto(
    [property: JsonPropertyName("publicKey")] string PublicKey,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("accountCount")] int AccountCount
);

/// <summary>Request to add a standalone key.</summary>
public sealed record AddKeyRequest(
    [property: JsonPropertyName("privateKey")] string PrivateKey,
    [property: JsonPropertyName("label")] string Label
);

/// <summary>Request to remove a stored key.</summary>
public sealed record RemoveKeyRequest(
    [property: JsonPropertyName("publicKey")] string PublicKey
);

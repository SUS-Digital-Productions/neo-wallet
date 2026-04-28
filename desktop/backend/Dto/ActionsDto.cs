using System.Text.Json;
using System.Text.Json.Serialization;

namespace NeoWallet.Backend.Dto;

/// <summary>
/// A single Antelope action description sent from the frontend.
/// </summary>
public sealed record ActionDto(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("data")] JsonElement Data,
    [property: JsonPropertyName("authorization")] AuthorizationDto[]? Authorization = null
);

public sealed record AuthorizationDto(
    [property: JsonPropertyName("actor")] string Actor,
    [property: JsonPropertyName("permission")] string Permission
);

/// <summary>
/// Request to sign and (optionally) broadcast a list of actions
/// using the wallet's currently active account.
/// </summary>
public sealed record SignActionsRequest(
    [property: JsonPropertyName("chainId")] string? ChainId,
    [property: JsonPropertyName("actions")] ActionDto[] Actions,
    [property: JsonPropertyName("broadcast")] bool Broadcast = true
);

public sealed record SignActionsResponse(
    [property: JsonPropertyName("transactionId")] string TransactionId,
    [property: JsonPropertyName("broadcast")] bool Broadcast
);

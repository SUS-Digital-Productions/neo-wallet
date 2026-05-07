using System.Text.Json;
using System.Text.Json.Serialization;

namespace NeoWallet.Backend.Dto;

public sealed record EsrParseRequest(
    [property: JsonPropertyName("uri")] string Uri
);

public sealed record EsrActionSummary(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("name")] string Name
);

public sealed record EsrParseResponse(
    [property: JsonPropertyName("requestId")] string RequestId,
    [property: JsonPropertyName("chainId")] string ChainId,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("actions")] IReadOnlyList<EsrActionSummary> Actions
);

public sealed record EsrApproveRequest(
    [property: JsonPropertyName("requestId")] string RequestId,
    [property: JsonPropertyName("broadcast")] bool Broadcast,
    [property: JsonPropertyName("account")] string? Account = null,
    [property: JsonPropertyName("authority")] string? Authority = null,
    [property: JsonPropertyName("chainId")] string? ChainId = null
);

public sealed record EsrRejectRequest(
    [property: JsonPropertyName("requestId")] string RequestId,
    [property: JsonPropertyName("reason")] string? Reason
);

/// <summary>Sign raw JSON actions.</summary>
public sealed record SignRawRequest(
    [property: JsonPropertyName("chainId")] string ChainId,
    [property: JsonPropertyName("actions")] JsonElement Actions,
    [property: JsonPropertyName("broadcast")] bool Broadcast = true,
    [property: JsonPropertyName("account")] string? Account = null,
    [property: JsonPropertyName("authority")] string? Authority = null
);

/// <summary>ESR listener connection status.</summary>
public sealed record EsrListenerStatusResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("linkId")] string LinkId,
    [property: JsonPropertyName("requestPublicKey")] string? RequestPublicKey,
    [property: JsonPropertyName("sessionCount")] int SessionCount
);

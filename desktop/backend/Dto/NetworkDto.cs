using System.Text.Json.Serialization;

namespace NeoWallet.Backend.Dto;

public sealed record NetworkDto(
    [property: JsonPropertyName("chainId")] string ChainId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("node")] string Node,
    [property: JsonPropertyName("nodeOptions")] IReadOnlyList<string> NodeOptions
);

public sealed record SetActiveNetworkRequest(
    [property: JsonPropertyName("chainId")] string ChainId
);

public sealed record SetNetworkNodeRequest(
    [property: JsonPropertyName("chainId")] string ChainId,
    [property: JsonPropertyName("node")] string Node
);

public sealed record TestNetworkNodeRequest(
    [property: JsonPropertyName("chainId")] string ChainId,
    [property: JsonPropertyName("node")] string Node
);

public sealed record TestNetworkNodeResponse(
    [property: JsonPropertyName("endpoint")] string Endpoint,
    [property: JsonPropertyName("chainId")] string ChainId,
    [property: JsonPropertyName("headBlockNum")] uint HeadBlockNum,
    [property: JsonPropertyName("serverVersion")] string ServerVersion,
    [property: JsonPropertyName("matchesExpectedChain")] bool MatchesExpectedChain
);

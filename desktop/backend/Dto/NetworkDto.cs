using System.Text.Json.Serialization;

namespace NeoWallet.Backend.Dto;

public sealed record NetworkDto(
    [property: JsonPropertyName("chainId")] string ChainId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("symbol")] string Symbol
);

public sealed record SetActiveNetworkRequest(
    [property: JsonPropertyName("chainId")] string ChainId
);

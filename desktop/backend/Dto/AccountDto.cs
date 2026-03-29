using System.Text.Json.Serialization;

namespace NeoWallet.Backend.Dto;

public sealed record AccountDto(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("authority")] string Authority,
    [property: JsonPropertyName("publicKey")] string PublicKey
);

public sealed record SetActiveAccountRequest(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("authority")] string Authority,
    [property: JsonPropertyName("chainId")] string ChainId
);

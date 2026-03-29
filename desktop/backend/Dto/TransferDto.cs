using System.Text.Json.Serialization;

namespace NeoWallet.Backend.Dto;

public sealed record TransferRequest(
    [property: JsonPropertyName("chainId")] string ChainId,
    [property: JsonPropertyName("from")] string From,
    [property: JsonPropertyName("authority")] string Authority,
    [property: JsonPropertyName("to")] string To,
    [property: JsonPropertyName("quantity")] string Quantity,
    [property: JsonPropertyName("memo")] string? Memo
);

public sealed record TransferResponse(
    [property: JsonPropertyName("transactionId")] string TransactionId,
    [property: JsonPropertyName("broadcast")] bool Broadcast
);

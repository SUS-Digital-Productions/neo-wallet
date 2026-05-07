using System.Text.Json.Serialization;

namespace NeoWallet.Backend.Dto;

public sealed record BalanceDto(
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("contract")] string Contract,
    [property: JsonPropertyName("amount")] string Amount,
    [property: JsonPropertyName("numericAmount")] decimal NumericAmount
);

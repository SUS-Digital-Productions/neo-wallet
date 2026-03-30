using System.Text.Json.Serialization;

namespace NeoWallet.Backend.Dto;

public sealed record AccountDto(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("authority")] string Authority,
    [property: JsonPropertyName("publicKey")] string PublicKey,
    [property: JsonPropertyName("chainId")] string ChainId,
    [property: JsonPropertyName("chainName")] string ChainName
);

public sealed record SetActiveAccountRequest(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("authority")] string Authority,
    [property: JsonPropertyName("chainId")] string ChainId
);

/// <summary>Entry specifying which account/chain to import.</summary>
public sealed record ImportAccountEntry(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("authority")] string Authority,
    [property: JsonPropertyName("chainId")] string ChainId
);

/// <summary>Request to look up accounts by private key across chains.</summary>
public sealed record LookupAccountsRequest(
    [property: JsonPropertyName("privateKey")] string PrivateKey,
    [property: JsonPropertyName("chainIds")] string[]? ChainIds = null
);

public sealed record LookupChainResult(
    [property: JsonPropertyName("chainId")] string ChainId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("accounts")] LookupAccountEntry[] Accounts
);

public sealed record LookupAccountEntry(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("authority")] string Authority
);

public sealed record LookupAccountsResponse(
    [property: JsonPropertyName("publicKey")] string PublicKey,
    [property: JsonPropertyName("chains")] LookupChainResult[] Chains
);

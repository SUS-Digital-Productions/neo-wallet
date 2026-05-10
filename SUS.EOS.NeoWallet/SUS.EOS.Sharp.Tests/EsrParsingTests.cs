using System.IO.Compression;
using System.Net;
using System.Text.Json;
using SUS.EOS.Sharp.Models;
using SUS.EOS.Sharp.Services;
using SUS.EOS.EosioSigningRequest.Services;
using SUS.EOS.EosioSigningRequest.Models;
using Xunit;

namespace SUS.EOS.Sharp.Tests;

public class EsrParsingTests
{
    [Fact]
    public async Task ParseRealEsrRequest_ShouldSucceed()
    {
        // This is a real ESR request - an identity request from Anchor
        var esrUri =
            "esr:g2PgYmZgYLjCyJNpw8BknVFSUlBspa-fnKSXmJeckV-kl5OZl61vlGhmmJZkaKFrmZxmomuSZGChm5hiaKxrYWSQZGRgmWZmapnCxAJSepERbhrzpYiPSqIfeNt49ydyv3P92vwo9lQpW8eu_90NQZ5pCYeOLmV0BNvhA7LCWM9Mz0DBqSi_vDi1KKQoMa-4IL-oBCxsqOCbX5WZk5OobwpUohGemZcCVKXgF6JgaKBnYK0AFDAzsVaoMDPRVHAsKMhJDU9N8s4s0Tc1NtczNlPQ8PYI8fXRUcjJzE5VcE9Nzs7XVHDOKMrPTdU3NDHUMwBBheDEtMSiTJgW_4AgfUMjU4gca3FyfkEqR1JOfnaxXmY-AA";

        var service = new EsrService();

        // This should parse without throwing
        var request = await service.ParseRequestAsync(esrUri);

        Assert.NotNull(request);
        Assert.NotNull(request.ChainId);

        // Log details for debugging
        Console.WriteLine($"ChainId: {request.ChainId}");
        Console.WriteLine($"Version: {request.Version}");
        Console.WriteLine($"Callback: {request.Callback}");
        Console.WriteLine($"Has Payload: {request.Payload != null}");
        Console.WriteLine($"Has Action: {request.Payload?.Action != null}");
        Console.WriteLine($"Has Transaction: {request.Payload?.Transaction != null}");

        // This is an identity request (type 3), so it may not have action/transaction
        // Verify the chain ID is correct for alias 10 (WAX Mainnet)
        Assert.Equal(
            "1064487b3cd1a897ce03ae5b6a865651747e2e152090f99c1d19d44e01aea5a4",
            request.ChainId
        );
    }

    [Fact]
    public async Task ParseWaxIdentityEsr_ShouldSucceed()
    {
        // WAX mainnet identity ESR from the user
        var esrUri =
            "esr:g2PgYmZgYLjCyJNpw8BknVFSUlBspa-fnKSXmJeckV-kl5OZl62fZmFsYJhknqxrkWRqomtiaWEAZCVa6pqZJhoYpxokmydZmjGxgJReZESY9lXj3KzTOSJlN34f3uFf3mfrwNS5pGrebk6vArV1xmGxIksZHcF2-ICsMNYz0zNQcCrKLy9OLQopSswrLsgvKgELGyr45ldl5uQk6psClWiEZ-alAFUp-IUoGBroGVgrAAXMTKwVKsxMNBUcCwpyUsNTk7wzS_RNjc31jM0UNLw9Qnx9dBRyMrNTFdxTk7PzNRWcM4ryc1P1DU0M9QxAUCE4MS2xKBOmxT8gSN_QyBQix1qcnF-QypGUk59drJeZDwA";

        var service = new EsrService();

        // This should parse without throwing
        var request = await service.ParseRequestAsync(esrUri);

        Assert.NotNull(request);
        Assert.NotNull(request.ChainId);

        // Log details for debugging
        Console.WriteLine($"ChainId: {request.ChainId}");
        Console.WriteLine($"Version: {request.Version}");
        Console.WriteLine($"Callback: {request.Callback}");
        Console.WriteLine($"Has Payload: {request.Payload != null}");
        Console.WriteLine($"Has Action: {request.Payload?.Action != null}");
        Console.WriteLine($"Has Transaction: {request.Payload?.Transaction != null}");
        Console.WriteLine(
            $"Is Identity Request: {request.Payload?.Action == null && request.Payload?.Transaction == null}"
        );

        // Verify this is WAX mainnet (chain alias 1)
        Assert.Equal(
            "1064487b3cd1a897ce03ae5b6a865651747e2e152090f99c1d19d44e01aea5a4",
            request.ChainId
        );

        // Identity requests (type 3) should not have action or transaction
        Assert.Null(request.Payload?.Action);
        Assert.Null(request.Payload?.Transaction);
    }

    [Fact]
    public async Task SignWaxIdentityEsr_ShouldNotFetchChainInfo()
    {
        var service = new EsrService();
        var request = new SUS.EOS.EosioSigningRequest.Models.Esr
        {
            ChainId = "1064487b3cd1a897ce03ae5b6a865651747e2e152090f99c1d19d44e01aea5a4",
            Payload = new SUS.EOS.EosioSigningRequest.Models.EsrRequestPayload(),
        };
        var privateKeyWif = SUS.EOS.Sharp.Cryptography.EosioKey.FromHex(
            "0000000000000000000000000000000000000000000000000000000000000001"
        ).PrivateKeyWif;
        var response = await service.SignRequestAsync(
            request,
            privateKeyWif,
            "eosio",
            "active",
            new ThrowingBlockchainClient(),
            broadcast: true
        );

        Assert.NotEmpty(response.Signatures);
        Assert.True(response.SerializedTransaction is { Length: > 0 });
        Assert.False(string.IsNullOrEmpty(response.PackedTransaction));
        Assert.Equal(
            "1064487b3cd1a897ce03ae5b6a865651747e2e152090f99c1d19d44e01aea5a4",
            response.ChainId
        );
    }

    [Fact]
    public async Task SendCallbackAsync_ShouldUseTransactionIdAndRefBlockPrefix()
    {
        var handler = new CapturingHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var request = new SUS.EOS.EosioSigningRequest.Models.Esr
        {
            Callback = "https://example.test/callback",
            OriginalUri = "esr://abc123",
        };

        var response = new EsrCallbackResponse
        {
            Signatures = ["SIG_K1_test"],
            TransactionId = "308d206c51c5dd6c02e0417e44560cdc2e76db7765cea19dfa8f9f94922f928a",
            PackedTransaction = "deadbeef",
            Signer = "alice",
            SignerPermission = "active",
            RefBlockNum = 1234,
            RefBlockPrefix = 56789,
            RefBlockId = "00000000000004d2ffffffffffffffffffffffffffffffffffffffffffffffffffffffff",
            Expiration = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc),
            ChainId = "1064487b3cd1a897ce03ae5b6a865651747e2e152090f99c1d19d44e01aea5a4",
        };

        await request.SendCallbackAsync(response, httpClient);

        Assert.NotNull(handler.Body);
        using var doc = JsonDocument.Parse(handler.Body!);
        var root = doc.RootElement;

        Assert.Equal(response.TransactionId, root.GetProperty("tx").GetString());
        Assert.Equal("1234", root.GetProperty("rbn").GetString());
        Assert.Equal("56789", root.GetProperty("rid").GetString());
        Assert.Equal("2026-05-10T12:00:00.000", root.GetProperty("ex").GetString());
        Assert.NotEqual(response.PackedTransaction, root.GetProperty("tx").GetString());
        Assert.NotEqual(response.RefBlockId, root.GetProperty("rid").GetString());
    }

    [Fact]
    public void DebugDecompressEsr()
    {
        // Test the base64url decoding and decompression
        var esrData =
            "g2PgYmZgYLjCyJNpw8BknVFSUlBspa-fnKSXmJeckV-kl5OZl61vlGhmmJZkaKFrmZxmomuSZGChm5hiaKxrYWSQZGRgmWZmapnCxAJSepERbhrzpYiPSqIfeNt49ydyv3P92vwo9lQpW8eu_90NQZ5pCYeOLmV0BNvhA7LCWM9Mz0DBqSi_vDi1KKQoMa-4IL-oBCxsqOCbX5WZk5OobwpUohGemZcCVKXgF6JgaKBnYK0AFDAzsVaoMDPRVHAsKMhJDU9N8s4s0Tc1NtczNlPQ8PYI8fXRUcjJzE5VcE9Nzs7XVHDOKMrPTdU3NDHUMwBBheDEtMSiTJgW_4AgfUMjU4gca3FyfkEqR1JOfnaxXmY-AA";

        // Convert base64url to base64
        var base64 = esrData.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2:
                base64 += "==";
                break;
            case 3:
                base64 += "=";
                break;
        }

        var bytes = Convert.FromBase64String(base64);

        Assert.NotEmpty(bytes);

        // First byte is the header
        var header = bytes[0];
        var version = header & 0x07;
        var isCompressed = (header & 0x80) != 0;

        Console.WriteLine($"Header byte: 0x{header:X2}");
        Console.WriteLine($"Version: {version}");
        Console.WriteLine($"Is compressed: {isCompressed}");
        Console.WriteLine($"Total bytes: {bytes.Length}");
        Console.WriteLine($"First 16 bytes: {BitConverter.ToString([.. bytes.Take(16)])}");

        // If compressed, try decompressing WITHOUT skipping zlib header
        // ESR uses raw deflate, not zlib!
        if (isCompressed)
        {
            var compressedData = bytes.Skip(1).ToArray();
            Console.WriteLine($"Compressed data length: {compressedData.Length}");
            Console.WriteLine(
                $"First bytes of compressed: 0x{compressedData[0]:X2} 0x{compressedData[1]:X2}"
            );

            // Try raw deflate (no zlib header)
            using var inputStream = new MemoryStream(compressedData);
            using var deflateStream = new DeflateStream(inputStream, CompressionMode.Decompress);
            using var outputStream = new MemoryStream();

            deflateStream.CopyTo(outputStream);
            var decompressed = outputStream.ToArray();

            Console.WriteLine($"Decompressed length: {decompressed.Length}");
            Console.WriteLine(
                $"First 64 bytes decompressed: {BitConverter.ToString(decompressed.Take(64).ToArray())}"
            );
            Console.WriteLine($"Chain ID type byte: {decompressed[0]}");

            // Decode the callback at offset 11+
            var callbackStart = 11;
            var callbackLen = decompressed[callbackStart];
            var callback = System.Text.Encoding.UTF8.GetString(
                decompressed,
                callbackStart + 1,
                callbackLen
            );
            Console.WriteLine($"Callback (len={callbackLen}): {callback}");

            // According to ESR spec, chain_id is a variant:
            // variant_type (varint) + data
            // 0 = chain_alias (uint8)
            // 1 = chain_id (checksum256 = 32 bytes)

            var chainIdType = decompressed[0];
            Console.WriteLine($"Chain ID type: {chainIdType}");

            if (chainIdType == 0)
            {
                // Chain alias
                var alias = decompressed[1];
                Console.WriteLine($"Chain alias: {alias}");

                // Request type
                var requestType = decompressed[2];
                Console.WriteLine($"Request type: {requestType}");
            }
            else if (chainIdType == 1)
            {
                // Full chain ID - 32 bytes
                var chainId = BitConverter
                    .ToString(decompressed.Skip(1).Take(32).ToArray())
                    .Replace("-", "")
                    .ToLowerInvariant();
                Console.WriteLine($"Chain ID (full): {chainId}");

                // Request type should be at offset 33
                var requestType = decompressed[33];
                Console.WriteLine($"Request type: {requestType}");
            }
        }
    }

    private sealed class ThrowingBlockchainClient : IAntelopeBlockchainClient
    {
        public string Endpoint => "https://example.invalid";
        public string? ChainId => null;

        public Task<ChainInfo> GetInfoAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Identity signing must not fetch chain info.");

        public Task<Account> GetAccountAsync(
            string accountName,
            CancellationToken cancellationToken = default
        ) => ThrowAsync<Account>();

        public Task<Block> GetBlockAsync(
            string blockNumOrId,
            CancellationToken cancellationToken = default
        ) => ThrowAsync<Block>();

        public Task<TransactionResult> PushTransactionAsync(
            object signedTransaction,
            CancellationToken cancellationToken = default
        ) => throw new InvalidOperationException("Identity signing must not broadcast.");

        public Task<TableRowsResult<T>> GetTableRowsAsync<T>(
            string contract,
            string scope,
            string table,
            CancellationToken cancellationToken = default
        ) => ThrowAsync<TableRowsResult<T>>();

        public Task<TableRowsResult<T>> GetTableRowsAsync<T>(
            string contract,
            string scope,
            string table,
            int limit,
            string? lowerBound = null,
            string? upperBound = null,
            bool reverse = false,
            CancellationToken cancellationToken = default
        ) => ThrowAsync<TableRowsResult<T>>();

        public Task<List<string>> GetCurrencyBalanceAsync(
            string contract,
            string account,
            string? symbol = null,
            CancellationToken cancellationToken = default
        ) => ThrowAsync<List<string>>();

        public Task<AbiDefinition?> GetAbiAsync(
            string contractAccount,
            CancellationToken cancellationToken = default
        ) => ThrowAsync<AbiDefinition?>();

        public Task<byte[]> AbiJsonToBinAsync(
            string contract,
            string action,
            object data,
            CancellationToken cancellationToken = default
        ) => ThrowAsync<byte[]>();

        public Task<object?> AbiBinToJsonAsync(
            string contract,
            string action,
            string binArgs,
            CancellationToken cancellationToken = default
        ) => ThrowAsync<object?>();

        public void Dispose()
        {
        }

        private static Task<T> ThrowAsync<T>() =>
            throw new InvalidOperationException("Identity signing must not use chain RPC.");
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}

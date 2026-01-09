using System.IO.Compression;
using SUS.EOS.EosioSigningRequest.Services;
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
}

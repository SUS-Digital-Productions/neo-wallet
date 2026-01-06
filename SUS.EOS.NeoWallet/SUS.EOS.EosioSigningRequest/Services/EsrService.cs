using System.Text;
using System.Text.Json;
using SUS.EOS.EosioSigningRequest.Models;
using SUS.EOS.Sharp.Models;
using SUS.EOS.Sharp.Services;

namespace SUS.EOS.EosioSigningRequest.Services;

/// <summary>
/// ESR service implementation
/// </summary>
public class EsrService : IEsrService
{
    private readonly HttpClient _httpClient;

    public EsrService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public Task<Esr> ParseRequestAsync(string uri)
    {
        try
        {
            var request = Esr.FromUri(uri);
            return Task.FromResult(request);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse ESR: {ex.Message}", ex);
        }
    }

    public async Task<EsrCallbackResponse> SignRequestAsync(
        Esr request,
        string privateKeyWif,
        string signer,
        string signerPermission,
        object? blockchainClient = null,
        bool broadcast = false,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            ChainInfo chainInfo;

            if (blockchainClient is IAntelopeBlockchainClient client)
            {
                // Get real chain info from blockchain
                chainInfo = await client.GetInfoAsync(cancellationToken);
            }
            else
            {
                // Fallback to dummy chain info (won't work for real transactions)
                var chainId =
                    request.ChainId
                    ?? throw new InvalidOperationException("Chain ID not specified");
                chainInfo = new ChainInfo
                {
                    ChainId = chainId,
                    ServerVersion = "unknown",
                    HeadBlockNum = 0,
                    LastIrreversibleBlockNum = 0,
                    LastIrreversibleBlockId = string.Empty,
                    HeadBlockId = string.Empty,
                    HeadBlockTime = DateTime.UtcNow,
                    HeadBlockProducer = string.Empty,
                    VirtualBlockCpuLimit = 0,
                    VirtualBlockNetLimit = 0,
                    BlockCpuLimit = 0,
                    BlockNetLimit = 0,
                    RefBlockPrefix = 0,
                };
            }

            var response = request.Sign(privateKeyWif, chainInfo, signer, signerPermission);

            // Broadcast if requested or if Broadcast flag is set in ESR
            if (broadcast || request.Flags.HasFlag(EsrFlags.Broadcast))
            {
                if (blockchainClient is not IAntelopeBlockchainClient bcClient)
                    throw new InvalidOperationException(
                        "Blockchain client required for broadcasting"
                    );

                // Only broadcast if there's an actual transaction (not identity requests)
                if (
                    response.SerializedTransaction != null
                    && response.SerializedTransaction.Length > 0
                )
                {
                    var pushRequest = new
                    {
                        signatures = response.Signatures,
                        compression = 0,
                        packed_context_free_data = "",
                        packed_trx = response.PackedTransaction,
                    };

                    var result = await bcClient.PushTransactionAsync(
                        pushRequest,
                        cancellationToken
                    );

                    // Update response with blockchain data (use direct property access)
                    response.TransactionId = result.TransactionId;
                    if (result.Processed != null)
                    {
                        response.BlockNum = result.Processed.BlockNum;
                        response.BlockId = result.Processed.Id;
                    }
                }
            }

            return response;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to sign ESR: {ex.Message}", ex);
        }
    }

    public async Task<EsrCallbackResponse> SignAndBroadcastAsync(
        Esr request,
        string privateKeyWif,
        string signer,
        string signerPermission,
        object blockchainClient,
        CancellationToken cancellationToken = default
    )
    {
        return await SignRequestAsync(
            request,
            privateKeyWif,
            signer,
            signerPermission,
            blockchainClient,
            broadcast: true,
            cancellationToken
        );
    }

    public async Task<bool> SendCallbackAsync(Esr request, EsrCallbackResponse response)
    {
        try
        {
            var httpResponse = await request.SendCallbackAsync(response, _httpClient);
            return httpResponse.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

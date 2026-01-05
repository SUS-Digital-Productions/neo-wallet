using System.Text;
using System.Text.Json;
using SUS.EOS.NeoWallet.Services.Interfaces;
using SUS.EOS.Sharp.ESR;
using SUS.EOS.Sharp.Services;

namespace SUS.EOS.NeoWallet.Services;

/// <summary>
/// Anchor-compatible callback service
/// Handles external application integration and transaction signing requests
/// Compatible with Anchor wallet callback protocol
/// </summary>
public interface IAnchorCallbackService
{
    /// <summary>
    /// Handle ESR signing request from external application
    /// </summary>
    Task<AnchorCallbackResult> HandleSigningRequestAsync(string esrUri, string? password = null);

    /// <summary>
    /// Register callback handler for specific chain
    /// </summary>
    void RegisterCallbackHandler(string chainId, Func<EosioSigningRequest, Task<bool>> handler);

    /// <summary>
    /// Send signed transaction back to requesting application
    /// </summary>
    Task<bool> SendCallbackResponseAsync(string callbackUrl, AnchorCallbackPayload payload);

    /// <summary>
    /// Handle deep link from external application (esr://, anchor://)
    /// </summary>
    Task<bool> HandleDeepLinkAsync(string deepLink);
}

/// <summary>
/// Anchor callback service implementation
/// </summary>
public class AnchorCallbackService : IAnchorCallbackService
{
    private readonly IWalletAccountService _accountService;
    private readonly IWalletStorageService _storageService;
    private readonly INetworkService _networkService;
    private readonly IEsrService _esrService;
    private readonly Dictionary<string, Func<EosioSigningRequest, Task<bool>>> _callbackHandlers = new();
    private readonly HttpClient _httpClient;

    public AnchorCallbackService(
        IWalletAccountService accountService,
        IWalletStorageService storageService,
        INetworkService networkService,
        IEsrService esrService,
        HttpClient? httpClient = null)
    {
        _accountService = accountService;
        _storageService = storageService;
        _networkService = networkService;
        _esrService = esrService;
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <summary>
    /// Handle ESR signing request from external application
    /// </summary>
    public async Task<AnchorCallbackResult> HandleSigningRequestAsync(string esrUri, string? password = null)
    {
        try
        {
            // Parse ESR
            var request = await _esrService.ParseRequestAsync(esrUri);

            // Get chain info
            var chainId = request.ChainId ?? throw new InvalidOperationException("Chain ID not specified in request");
            var network = await _networkService.GetNetworkAsync(chainId);
            if (network == null)
                throw new InvalidOperationException($"Network '{chainId}' not configured");

            // Get current account
            var currentAccount = await _accountService.GetCurrentAccountAsync();
            if (currentAccount == null)
                throw new InvalidOperationException("No active account. Please select an account.");

            // Get private key (requires password if wallet is locked)
            string? privateKey;
            if (_storageService.IsUnlocked)
            {
                privateKey = _storageService.GetUnlockedPrivateKey(currentAccount.Data.PublicKey);
            }
            else
            {
                if (string.IsNullOrEmpty(password))
                    throw new UnauthorizedAccessException("Wallet is locked. Password required.");

                privateKey = await _accountService.GetPrivateKeyAsync(
                    currentAccount.Data.Account,
                    currentAccount.Data.Authority,
                    currentAccount.Data.ChainId,
                    password);
            }

            if (privateKey == null)
                throw new InvalidOperationException("Failed to retrieve private key");

            // Create blockchain client for the network
            var blockchainClient = new Sharp.Services.AntelopeHttpClient(network.HttpEndpoint);

            // Sign the request (broadcast if ESR Broadcast flag is set)
            var response = await _esrService.SignRequestAsync(request, privateKey, blockchainClient, broadcast: false, CancellationToken.None);

            // Add signer info
            response.Signer = currentAccount.Data.Account;
            response.SignerPermission = currentAccount.Data.Authority;

            // Send callback if specified
            if (!string.IsNullOrEmpty(request.Callback))
            {
                await _esrService.SendCallbackAsync(request, response);
            }

            // Call registered handler
            if (_callbackHandlers.TryGetValue(chainId, out var handler))
            {
                await handler(request);
            }

            return new AnchorCallbackResult
            {
                Success = true,
                Response = response,
                Account = currentAccount.Data.Account,
                Permission = currentAccount.Data.Authority,
                ChainId = chainId
            };
        }
        catch (Exception ex)
        {
            return new AnchorCallbackResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Register callback handler for specific chain
    /// </summary>
    public void RegisterCallbackHandler(string chainId, Func<EosioSigningRequest, Task<bool>> handler)
    {
        _callbackHandlers[chainId] = handler;
    }

    /// <summary>
    /// Send signed transaction back to requesting application
    /// </summary>
    public async Task<bool> SendCallbackResponseAsync(string callbackUrl, AnchorCallbackPayload payload)
    {
        try
        {
            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync(callbackUrl, content);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Handle deep link from external application
    /// </summary>
    public async Task<bool> HandleDeepLinkAsync(string deepLink)
    {
        try
        {
            // Parse deep link protocol
            if (deepLink.StartsWith("esr://") || deepLink.StartsWith("web+esr://"))
            {
                // ESR protocol
                var result = await HandleSigningRequestAsync(deepLink);
                return result.Success;
            }
            else if (deepLink.StartsWith("anchor://"))
            {
                // Custom anchor protocol (could be used for app-to-app communication)
                return await HandleAnchorProtocolAsync(deepLink);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Handle custom anchor:// protocol
    /// </summary>
    private async Task<bool> HandleAnchorProtocolAsync(string uri)
    {
        try
        {
            // Parse anchor:// URI
            // Format: anchor://action?param1=value1&param2=value2
            var cleanUri = uri.Replace("anchor://", "");
            var parts = cleanUri.Split('?');
            var action = parts[0];
            var parameters = parts.Length > 1 ? ParseQueryString(parts[1]) : new Dictionary<string, string>();

            switch (action.ToLowerInvariant())
            {
                case "sign":
                    // Sign transaction
                    if (parameters.TryGetValue("esr", out var esrUri))
                    {
                        var result = await HandleSigningRequestAsync(esrUri);
                        return result.Success;
                    }
                    break;

                case "identity":
                    // Provide identity (current account info)
                    var account = await _accountService.GetCurrentAccountAsync();
                    if (account != null && parameters.TryGetValue("callback", out var callback))
                    {
                        var identityPayload = new AnchorCallbackPayload
                        {
                            Account = account.Data.Account,
                            Permission = account.Data.Authority,
                            PublicKey = account.Data.PublicKey,
                            ChainId = account.Data.ChainId
                        };
                        return await SendCallbackResponseAsync(callback, identityPayload);
                    }
                    break;

                case "link":
                    // Link wallet to dApp
                    // Implementation would store dApp connection info
                    break;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var result = new Dictionary<string, string>();
        var pairs = query.Split('&');
        
        foreach (var pair in pairs)
        {
            var parts = pair.Split('=');
            if (parts.Length == 2)
            {
                result[Uri.UnescapeDataString(parts[0])] = Uri.UnescapeDataString(parts[1]);
            }
        }

        return result;
    }
}

/// <summary>
/// Result of Anchor callback processing
/// </summary>
public class AnchorCallbackResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public EsrCallbackResponse? Response { get; set; }
    public string? Account { get; set; }
    public string? Permission { get; set; }
    public string? ChainId { get; set; }
}

/// <summary>
/// Anchor callback payload (sent to external application)
/// </summary>
public class AnchorCallbackPayload
{
    public string? Account { get; set; }
    public string? Permission { get; set; }
    public string? PublicKey { get; set; }
    public string? ChainId { get; set; }
    public List<string>? Signatures { get; set; }
    public string? TransactionId { get; set; }
    public object? Transaction { get; set; }
    public uint? BlockNum { get; set; }
    public string? BlockId { get; set; }
}

/// <summary>
/// Anchor protocol handler for deep link registration
/// </summary>
public static class AnchorProtocolHandler
{
    private static IAnchorCallbackService? _callbackService;

    /// <summary>
    /// Initialize protocol handler with callback service
    /// </summary>
    public static void Initialize(IAnchorCallbackService callbackService)
    {
        _callbackService = callbackService;
    }

    /// <summary>
    /// Handle incoming protocol URI
    /// </summary>
    public static async Task<bool> HandleUriAsync(string uri)
    {
        if (_callbackService == null)
            throw new InvalidOperationException("Protocol handler not initialized");

        return await _callbackService.HandleDeepLinkAsync(uri);
    }

    /// <summary>
    /// Register custom URI scheme with OS (platform-specific)
    /// </summary>
    public static void RegisterUriScheme(string scheme = "neowallet")
    {
        // Platform-specific implementation would register the URI scheme
        // Windows: Registry modification
        // macOS: Info.plist CFBundleURLTypes
        // iOS: URL Schemes in Info.plist
        // Android: Intent filters in AndroidManifest.xml
    }
}
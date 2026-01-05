using System.Text.Json;
using SUS.EOS.NeoWallet.Services;
using SUS.EOS.NeoWallet.Services.Interfaces;
using SUS.EOS.Sharp.ESR;
using SUS.EOS.Sharp.Services;

namespace SUS.EOS.NeoWallet.Pages;

/// <summary>
/// ESR Signing Request Popup Page
/// Displays transaction details and allows user to approve or reject signing requests
/// </summary>
public partial class EsrSigningPopupPage : ContentPage
{
    private readonly IWalletContextService _walletContext;
    private readonly IEsrService _esrService;
    private readonly IWalletAccountService _accountService;
    private readonly IWalletStorageService _storageService;
    private readonly INetworkService _networkService;
    private readonly IAntelopeBlockchainClient _blockchainClient;

    private EosioSigningRequest? _request;
    private string? _rawPayload;
    private string? _callbackUrl;
    private TaskCompletionSource<EsrSigningResult>? _completionSource;

    public EsrSigningPopupPage(
        IWalletContextService walletContext,
        IEsrService esrService,
        IWalletAccountService accountService,
        IWalletStorageService storageService,
        INetworkService networkService,
        IAntelopeBlockchainClient blockchainClient)
    {
        InitializeComponent();
        _walletContext = walletContext;
        _esrService = esrService;
        _accountService = accountService;
        _storageService = storageService;
        _networkService = networkService;
        _blockchainClient = blockchainClient;
    }

    /// <summary>
    /// Initialize the page with signing request details
    /// </summary>
    public async Task<EsrSigningResult> ShowSigningRequestAsync(
        EosioSigningRequest request,
        string? rawPayload = null,
        string? callbackUrl = null,
        string? dAppName = null)
    {
        _request = request;
        _rawPayload = rawPayload;
        _callbackUrl = callbackUrl;
        _completionSource = new TaskCompletionSource<EsrSigningResult>();

        // Set dApp info
        DAppNameLabel.Text = dAppName ?? "Unknown Application";
        
        // Set chain info
        if (!string.IsNullOrEmpty(request.ChainId))
        {
            var network = await _networkService.GetNetworkAsync(request.ChainId);
            DAppChainLabel.Text = network?.Name ?? request.ChainId[..8] + "...";
        }

        // Set account info from wallet context
        var account = _walletContext.ActiveAccount;
        if (account != null)
        {
            AccountNameLabel.Text = account.Data.Account;
            AccountPermissionLabel.Text = $"@{account.Data.Authority}";
        }
        else
        {
            AccountNameLabel.Text = "No account selected";
            AccountPermissionLabel.Text = "";
        }

        // Set actions
        var actions = ParseActions(request);
        ActionCountLabel.Text = actions.Count == 1 ? "1 Action" : $"{actions.Count} Actions";
        ActionsCollectionView.ItemsSource = actions;

        // Set raw transaction
        RawTransactionLabel.Text = FormatJson(request.Payload.IsTransaction ? request.Payload.Transaction : request.Payload.Action);

        // Check for sensitive operations
        CheckForWarnings(request, actions);

        return await _completionSource.Task;
    }

    private List<ActionDisplayItem> ParseActions(EosioSigningRequest request)
    {
        var actions = new List<ActionDisplayItem>();

        // Handle action payload
        if (request.Payload.IsAction && request.Payload.Action != null)
        {
            var action = ParseActionFromObject(request.Payload.Action);
            if (action != null)
                actions.Add(action);
        }
        // Handle transaction payload with actions array
        else if (request.Payload.IsTransaction && request.Payload.Transaction != null)
        {
            var transactionActions = ExtractActionsFromTransaction(request.Payload.Transaction);
            actions.AddRange(transactionActions);
        }

        return actions;
    }

    private static ActionDisplayItem? ParseActionFromObject(object actionObj)
    {
        try
        {
            // Try to parse as JsonElement
            if (actionObj is JsonElement element)
            {
                return new ActionDisplayItem
                {
                    Contract = element.TryGetProperty("account", out var acc) ? acc.GetString() ?? "unknown" : "unknown",
                    Action = element.TryGetProperty("name", out var name) ? name.GetString() ?? "unknown" : "unknown",
                    DataPreview = element.TryGetProperty("data", out var data) ? FormatDataPreview(data) : "No data"
                };
            }
            
            // Try to serialize and re-parse
            var json = JsonSerializer.Serialize(actionObj);
            var parsed = JsonSerializer.Deserialize<JsonElement>(json);
            return new ActionDisplayItem
            {
                Contract = parsed.TryGetProperty("account", out var acc2) ? acc2.GetString() ?? "unknown" : "unknown",
                Action = parsed.TryGetProperty("name", out var name2) ? name2.GetString() ?? "unknown" : "unknown",
                DataPreview = parsed.TryGetProperty("data", out var data2) ? FormatDataPreview(data2) : "No data"
            };
        }
        catch
        {
            return new ActionDisplayItem
            {
                Contract = "unknown",
                Action = "unknown",
                DataPreview = actionObj.ToString() ?? "Unable to parse"
            };
        }
    }

    private static List<ActionDisplayItem> ExtractActionsFromTransaction(object transactionObj)
    {
        var result = new List<ActionDisplayItem>();
        
        try
        {
            JsonElement element;
            if (transactionObj is JsonElement je)
            {
                element = je;
            }
            else
            {
                var json = JsonSerializer.Serialize(transactionObj);
                element = JsonSerializer.Deserialize<JsonElement>(json);
            }

            if (element.TryGetProperty("actions", out var actionsArray) && actionsArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var actionElement in actionsArray.EnumerateArray())
                {
                    var action = ParseActionFromObject(actionElement);
                    if (action != null)
                        result.Add(action);
                }
            }
        }
        catch
        {
            // If parsing fails, add a placeholder
            result.Add(new ActionDisplayItem
            {
                Contract = "transaction",
                Action = "unknown",
                DataPreview = transactionObj.ToString() ?? "Unable to parse transaction"
            });
        }

        return result;
    }

    private static string FormatDataPreview(object data)
    {
        try
        {
            var json = data is string s ? s : JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = false });
            // Truncate if too long
            return json.Length > 200 ? json[..197] + "..." : json;
        }
        catch
        {
            return data.ToString() ?? "Unable to display";
        }
    }

    private static string FormatJson(object? obj)
    {
        if (obj == null) return "{}";
        try
        {
            return JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return obj.ToString() ?? "{}";
        }
    }

    private void CheckForWarnings(EosioSigningRequest request, List<ActionDisplayItem> actions)
    {
        var warnings = new List<string>();

        foreach (var action in actions)
        {
            // Check for sensitive actions
            if (action.Contract == "eosio" && action.Action == "updateauth")
                warnings.Add("Modifying account permissions");
            if (action.Contract == "eosio" && action.Action == "deleteauth")
                warnings.Add("Deleting account permissions");
            if (action.Contract == "eosio" && action.Action == "linkauth")
                warnings.Add("Linking contract permissions");
            if (action.Contract == "eosio" && action.Action == "unlinkauth")
                warnings.Add("Unlinking contract permissions");
            if (action.Action?.Contains("transfer", StringComparison.OrdinalIgnoreCase) == true)
                warnings.Add("Token transfer");
        }

        if (warnings.Count > 0)
        {
            WarningBorder.IsVisible = true;
            WarningLabel.Text = string.Join(", ", warnings.Distinct());
        }
    }

    private void OnShowRawToggled(object sender, ToggledEventArgs e)
    {
        RawTransactionBorder.IsVisible = e.Value;
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        _completionSource?.TrySetResult(new EsrSigningResult
        {
            Success = false,
            Cancelled = true,
            Error = "User cancelled the request"
        });

        await Navigation.PopModalAsync();
    }

    private async void OnSignClicked(object sender, EventArgs e)
    {
        if (_request == null)
        {
            _completionSource?.TrySetResult(new EsrSigningResult
            {
                Success = false,
                Error = "No signing request available"
            });
            return;
        }

        SignButton.IsEnabled = false;
        CancelButton.IsEnabled = false;
        LoadingIndicator.IsRunning = true;
        LoadingIndicator.IsVisible = true;

        try
        {
            var account = _walletContext.ActiveAccount;
            if (account == null)
                throw new InvalidOperationException("No active account selected");

            // Get private key
            string? privateKey;
            if (_storageService.IsUnlocked)
            {
                privateKey = _storageService.GetUnlockedPrivateKey(account.Data.PublicKey);
            }
            else
            {
                // Show password prompt
                var password = await DisplayPromptAsync(
                    "Wallet Locked",
                    "Enter your password to sign the transaction:",
                    "Sign",
                    "Cancel",
                    keyboard: Keyboard.Default);

                if (string.IsNullOrEmpty(password))
                {
                    _completionSource?.TrySetResult(new EsrSigningResult
                    {
                        Success = false,
                        Cancelled = true,
                        Error = "Password not provided"
                    });
                    await Navigation.PopModalAsync();
                    return;
                }

                privateKey = await _accountService.GetPrivateKeyAsync(
                    account.Data.Account,
                    account.Data.Authority,
                    account.Data.ChainId,
                    password);
            }

            if (string.IsNullOrEmpty(privateKey))
                throw new InvalidOperationException("Failed to retrieve private key");

            // Sign the request with blockchain client (enables proper chain info and broadcasting)
            var chainId = _request.ChainId ?? account.Data.ChainId;
            var response = await _esrService.SignRequestAsync(
                _request, 
                privateKey, 
                _blockchainClient, 
                broadcast: true, // Always broadcast when user clicks sign
                CancellationToken.None);

            // Send callback if specified
            if (!string.IsNullOrEmpty(_request.Callback))
            {
                await _esrService.SendCallbackAsync(_request, response);
            }

            // Compute transaction ID from packed transaction (if available)
            var txId = response.PackedTransaction != null 
                ? ComputeTransactionId(response.SerializedTransaction) 
                : null;

            _completionSource?.TrySetResult(new EsrSigningResult
            {
                Success = true,
                TransactionId = txId,
                Signatures = response.Signatures,
                Account = account.Data.Account,
                Permission = account.Data.Authority
            });

            await Navigation.PopModalAsync();
        }
        catch (Exception ex)
        {
            _completionSource?.TrySetResult(new EsrSigningResult
            {
                Success = false,
                Error = ex.Message
            });

            await DisplayAlert("Signing Failed", ex.Message, "OK");
        }
        finally
        {
            SignButton.IsEnabled = true;
            CancelButton.IsEnabled = true;
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        }
    }

    /// <summary>
    /// Compute transaction ID from serialized transaction
    /// </summary>
    private static string? ComputeTransactionId(byte[]? serializedTransaction)
    {
        if (serializedTransaction == null || serializedTransaction.Length == 0)
            return null;

        try
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hash = sha256.ComputeHash(serializedTransaction);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Action display item for the CollectionView
/// </summary>
public class ActionDisplayItem
{
    public string Contract { get; set; } = "";
    public string Action { get; set; } = "";
    public string DataPreview { get; set; } = "";
}

/// <summary>
/// Result of ESR signing operation
/// </summary>
public class EsrSigningResult
{
    public bool Success { get; set; }
    public bool Cancelled { get; set; }
    public string? Error { get; set; }
    public string? TransactionId { get; set; }
    public List<string>? Signatures { get; set; }
    public string? Account { get; set; }
    public string? Permission { get; set; }
}

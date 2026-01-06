#pragma warning disable CS0618 // DisplayAlert/DisplayActionSheet obsolete warnings

using System.Text;
using System.Text.Json;
using SUS.EOS.EosioSigningRequest.Models;
using SUS.EOS.EosioSigningRequest.Services;
using SUS.EOS.NeoWallet.Services.Interfaces;
using SUS.EOS.Sharp.Services;
using EsrRequest = SUS.EOS.EosioSigningRequest.Models.Esr;

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
    private readonly IEsrSessionManager _esrSessionManager;

    private EsrRequest? _request;
    private string? _rawPayload;
    private string? _callbackUrl;
    private TaskCompletionSource<EsrSigningResult>? _completionSource;

    public EsrSigningPopupPage(
        IWalletContextService walletContext,
        IEsrService esrService,
        IWalletAccountService accountService,
        IWalletStorageService storageService,
        INetworkService networkService,
        IAntelopeBlockchainClient blockchainClient,
        IEsrSessionManager esrSessionManager
    )
    {
        InitializeComponent();
        _walletContext = walletContext;
        _esrService = esrService;
        _accountService = accountService;
        _storageService = storageService;
        _networkService = networkService;
        _blockchainClient = blockchainClient;
        _esrSessionManager = esrSessionManager;
    }

    /// <summary>
    /// Initialize the page with signing request details
    /// </summary>
    public async Task<EsrSigningResult> ShowSigningRequestAsync(
        EsrRequest request,
        string? rawPayload = null,
        string? callbackUrl = null,
        string? dAppName = null
    )
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
        RawTransactionLabel.Text = FormatJson(
            request.Payload.IsTransaction ? request.Payload.Transaction : request.Payload.Action
        );

        // Check for sensitive operations
        CheckForWarnings(request, actions);

        return await _completionSource.Task;
    }

    private List<ActionDisplayItem> ParseActions(EsrRequest request)
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
                    Contract = element.TryGetProperty("account", out var acc)
                        ? acc.GetString() ?? "unknown"
                        : "unknown",
                    Action = element.TryGetProperty("name", out var name)
                        ? name.GetString() ?? "unknown"
                        : "unknown",
                    DataPreview = element.TryGetProperty("data", out var data)
                        ? FormatDataPreview(data)
                        : "No data",
                };
            }

            // Try to serialize and re-parse
            var json = JsonSerializer.Serialize(actionObj);
            var parsed = JsonSerializer.Deserialize<JsonElement>(json);
            return new ActionDisplayItem
            {
                Contract = parsed.TryGetProperty("account", out var acc2)
                    ? acc2.GetString() ?? "unknown"
                    : "unknown",
                Action = parsed.TryGetProperty("name", out var name2)
                    ? name2.GetString() ?? "unknown"
                    : "unknown",
                DataPreview = parsed.TryGetProperty("data", out var data2)
                    ? FormatDataPreview(data2)
                    : "No data",
            };
        }
        catch
        {
            return new ActionDisplayItem
            {
                Contract = "unknown",
                Action = "unknown",
                DataPreview = actionObj.ToString() ?? "Unable to parse",
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

            if (
                element.TryGetProperty("actions", out var actionsArray)
                && actionsArray.ValueKind == JsonValueKind.Array
            )
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
            result.Add(
                new ActionDisplayItem
                {
                    Contract = "transaction",
                    Action = "unknown",
                    DataPreview = transactionObj.ToString() ?? "Unable to parse transaction",
                }
            );
        }

        return result;
    }

    private static string FormatDataPreview(object data)
    {
        try
        {
            var json = data is string s
                ? s
                : JsonSerializer.Serialize(
                    data,
                    new JsonSerializerOptions { WriteIndented = false }
                );
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
        if (obj == null)
            return "{}";
        try
        {
            return JsonSerializer.Serialize(
                obj,
                new JsonSerializerOptions { WriteIndented = true }
            );
        }
        catch
        {
            return obj.ToString() ?? "{}";
        }
    }

    private void CheckForWarnings(EsrRequest request, List<ActionDisplayItem> actions)
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
        // Send rejection callback to the dApp if we have a callback URL
        await SendRejectionCallbackAsync("User cancelled the request");
        
        _completionSource?.TrySetResult(
            new EsrSigningResult
            {
                Success = false,
                Cancelled = true,
                Error = "User cancelled the request",
            }
        );

        await Navigation.PopModalAsync();
    }
    
    private async Task SendRejectionCallbackAsync(string reason)
    {
        try
        {
            var callbackUrl = _callbackUrl ?? _request?.Callback;
            if (string.IsNullOrEmpty(callbackUrl))
            {
                System.Diagnostics.Trace.WriteLine("[ESRSIGNING] No callback URL for rejection");
                return;
            }
            
            System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] Sending rejection callback: {reason}");
            
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            
            // Send rejection with error payload
            var rejectData = new
            {
                rejected = reason,
                cid = _request?.ChainId ?? "",
            };
            
            var json = JsonSerializer.Serialize(rejectData);
            System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] Rejection payload: {json}");
            
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(callbackUrl, content);
            
            System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] Rejection response: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] Failed to send rejection: {ex.Message}");
        }
    }

    private async void OnSignClicked(object sender, EventArgs e)
    {
        if (_request == null)
        {
            _completionSource?.TrySetResult(
                new EsrSigningResult { Success = false, Error = "No signing request available" }
            );
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
                    keyboard: Keyboard.Default
                );

                if (string.IsNullOrEmpty(password))
                {
                    _completionSource?.TrySetResult(
                        new EsrSigningResult
                        {
                            Success = false,
                            Cancelled = true,
                            Error = "Password not provided",
                        }
                    );
                    await Navigation.PopModalAsync();
                    return;
                }

                privateKey = await _accountService.GetPrivateKeyAsync(
                    account.Data.Account,
                    account.Data.Authority,
                    account.Data.ChainId,
                    password
                );
            }

            if (string.IsNullOrEmpty(privateKey))
                throw new InvalidOperationException("Failed to retrieve private key");

            // Debug: Verify which public key corresponds to this private key
            var keyObj = SUS.EOS.Sharp.Cryptography.EosioKey.FromPrivateKey(privateKey);
            System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] ========== KEY VERIFICATION ==========");
            System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] Private key corresponds to public key: {keyObj.PublicKey}");
            System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] Account stored public key: {account.Data.PublicKey}");
            System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] Signing as: {account.Data.Account}@{account.Data.Authority}");
            
            // Fetch on-chain account to verify authorized keys
            try
            {
                var onChainAccount = await _blockchainClient.GetAccountAsync(account.Data.Account);
                System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] On-chain account fetched successfully");
                
                // Find the permission we're trying to use
                var permission = onChainAccount.Permissions?.FirstOrDefault(p => p.PermName == account.Data.Authority);
                if (permission != null)
                {
                    System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] On-chain {account.Data.Authority} permission found");
                    System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] Required auth threshold: {permission.RequiredAuth?.Threshold}");
                    System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] Authorized keys count: {permission.RequiredAuth?.Keys?.Count ?? 0}");
                    
                    if (permission.RequiredAuth?.Keys != null)
                    {
                        foreach (var key in permission.RequiredAuth.Keys)
                        {
                            System.Diagnostics.Trace.WriteLine($"[ESRSIGNING]   Key: {key.Key} (weight: {key.Weight})");
                            if (key.Key == keyObj.PublicKey)
                            {
                                System.Diagnostics.Trace.WriteLine($"[ESRSIGNING]   ✅ MATCH! This key is authorized on-chain");
                            }
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] ⚠️ Permission '{account.Data.Authority}' not found on-chain!");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] Failed to fetch on-chain account: {ex.Message}");
            }
            System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] ====================================");

            // Sign the request with blockchain client (for chain info only)
            // DO NOT broadcast - Anchor Link protocol requires the dApp/callback service to broadcast
            var chainId = _request.ChainId ?? account.Data.ChainId;
            var response = await _esrService.SignRequestAsync(
                _request,
                privateKey,
                account.Data.Account,
                account.Data.Authority,
                _blockchainClient,
                broadcast: false, // Anchor Link: dApp broadcasts via callback, not wallet
                CancellationToken.None
            );

            // Debug: Log ESR request details
            System.Diagnostics.Trace.WriteLine("[ESRSIGNING] ===== ESR REQUEST DEBUG =====");
            System.Diagnostics.Trace.WriteLine(
                $"[ESRSIGNING] ESR.Callback: {_request.Callback ?? "(null)"}"
            );
            System.Diagnostics.Trace.WriteLine(
                $"[ESRSIGNING] Envelope.Callback (_callbackUrl): {_callbackUrl ?? "(null)"}"
            );
            System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] ChainId: {_request.ChainId}");
            System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] Flags: {_request.Flags}");
            System.Diagnostics.Trace.WriteLine(
                $"[ESRSIGNING] Info count: {_request.Info?.Count ?? 0}"
            );
            System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] RawPayload: {_rawPayload ?? "(null)"}");
            if (_request.Info != null)
            {
                foreach (var kvp in _request.Info)
                {
                    System.Diagnostics.Trace.WriteLine(
                        $"[ESRSIGNING] Info[{kvp.Key}]: {kvp.Value}"
                    );
                }
            }
            
            // CRITICAL: Check if this is a link session establishment request
            // Anchor Link protocol embeds session info in the ESR Info dictionary
            var hasLinkInfo = _request.Info?.ContainsKey("link") ?? false;
            var hasReqKey = _request.Info?.ContainsKey("req_key") ?? false;
            System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] Has 'link' in Info: {hasLinkInfo}");
            System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] Has 'req_key' in Info: {hasReqKey}");
            System.Diagnostics.Trace.WriteLine("[ESRSIGNING] ============================");

            // Determine which callback URL to use (envelope takes precedence over ESR)
            var callbackUrl = _callbackUrl ?? _request.Callback;

            // Handle callback or direct ESR completion
            if (!string.IsNullOrEmpty(callbackUrl))
            {
                // Session-based or callback-enabled request
                System.Diagnostics.Trace.WriteLine(
                    $"[ESRSIGNING] Sending HTTP callback to: {callbackUrl}"
                );

                // Get session info from ESR session manager for Anchor Link protocol
                var linkChannel = $"https://cb.anchor.link/{_esrSessionManager.LinkId}";
                var linkKey = _esrSessionManager.RequestPublicKey;
                var linkName = "NeoWallet";

                System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] link_ch: {linkChannel}");
                System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] link_key: {linkKey}");
                System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] link_name: {linkName}");

                // Manually construct and send callback since _request.Callback might be null
                try
                {
                    using var httpClient = new HttpClient();
                    
                    // Get the original ESR URI - this is REQUIRED for the callback
                    var originalEsrUri = _request.ToUri();
                    
                    // Calculate expiration (5 minutes from now in ISO format)
                    var expiration = DateTime.UtcNow.AddMinutes(5).ToString("yyyy-MM-ddTHH:mm:ss");
                    
                    // CRITICAL: Use the chain ID from the response (used for signing), NOT the request
                    // The response.ChainId comes from the actual blockchain client used for signing
                    var chainIdForCallback = !string.IsNullOrEmpty(response.ChainId) 
                        ? response.ChainId 
                        : _request.ChainId ?? "";
                    
                    System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] Request ChainId: {_request.ChainId}");
                    System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] Response ChainId: {response.ChainId}");
                    System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] Using ChainId for callback: {chainIdForCallback}");
                    
                    // CRITICAL: Include ALL required ESR callback fields per spec
                    // See: https://github.com/greymass/eosio-signing-request
                    var callbackData = new
                    {
                        // Required callback fields
                        sig = response.Signatures?.FirstOrDefault() ?? "",  // First signature
                        tx = response.PackedTransaction ?? "",               // Transaction ID (hex)
                        sa = account.Data.Account,                           // Signer account
                        sp = account.Data.Authority,                         // Signer permission
                        rbn = response.RefBlockNum?.ToString() ?? "0",       // Reference block num
                        rid = response.RefBlockPrefix?.ToString() ?? "0",    // Reference block prefix (uint32)
                        ex = expiration,                                     // Expiration time
                        req = originalEsrUri,                                // Original ESR request URI
                        cid = chainIdForCallback,                            // Chain ID from signing
                        
                        // Optional: block number if broadcast
                        bn = response.BlockNum?.ToString(),
                        
                        // Anchor Link session info (REQUIRED for dApp to establish session)
                        link_ch = linkChannel,
                        link_key = linkKey,
                        link_name = linkName,
                    };

                    var json = System.Text.Json.JsonSerializer.Serialize(callbackData);
                    System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] Callback payload: {json}");
                    
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var callbackResponse = await httpClient.PostAsync(callbackUrl, content);

                    System.Diagnostics.Trace.WriteLine(
                        $"[ESRSIGNING] HTTP callback response: {callbackResponse.StatusCode}"
                    );
                    
                    var responseBody = await callbackResponse.Content.ReadAsStringAsync();
                    System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] Response body: {responseBody}");

                    if (callbackResponse.IsSuccessStatusCode)
                    {
                        await DisplayAlert(
                            "Success",
                            "Transaction signed and session established!",
                            "OK"
                        );
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine(
                        $"[ESRSIGNING] Callback failed: {ex.Message}"
                    );
                    await DisplayAlert(
                        "Warning",
                        $"Transaction signed but callback failed: {ex.Message}",
                        "OK"
                    );
                }
            }
            else
            {
                // Direct ESR without explicit callback URL
                // Try to find callback from other sources
                System.Diagnostics.Trace.WriteLine(
                    "[ESRSIGNING] No callback URL - checking for alternate callback methods"
                );

                // Check for 'return_path' in info (used by some implementations)
                string? returnPath = null;
                if (_request.Info != null)
                {
                    if (_request.Info.TryGetValue("return_path", out var rp))
                        returnPath = rp?.ToString();
                    else if (_request.Info.TryGetValue("req", out var req))
                        returnPath = req?.ToString();
                    else if (_request.Info.TryGetValue("buoy", out var buoy))
                        returnPath = buoy?.ToString();
                }
                
                // Check if callback is a background flag (Anchor Link style)
                // When flags indicate background callback, we should POST to buoy
                var hasBackgroundFlag = (_request.Flags & EsrFlags.Background) != 0;
                System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] Has background flag: {hasBackgroundFlag}");
                System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] Return path from info: {returnPath ?? "(null)"}");
                
                // Check if this is an identity request
                var isIdentityRequest =
                    !_request.Payload.IsTransaction && !_request.Payload.IsAction;

                // For identity requests, we should try to send via buoy if available
                if (returnPath != null)
                {
                    System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] Sending callback to return_path: {returnPath}");
                    await SendCallbackToUrl(returnPath, response);
                }
                else if (isIdentityRequest)
                {
                    System.Diagnostics.Trace.WriteLine(
                        "[ESRSIGNING] Identity request without callback"
                    );
                    
                    // Check if we have an active ESR session that can be used
                    // This happens when the website uses WebSocket-based Anchor Link
                    if (_esrSessionManager.Status == EsrSessionStatus.Connected)
                    {
                        System.Diagnostics.Trace.WriteLine("[ESRSIGNING] Trying to send via ESR session manager");
                        
                        // Build callback payload
                        var callbackPayload = new EsrCallbackPayload
                        {
                            Signature = response.Signatures?.FirstOrDefault(),
                            BlockNum = response.BlockNum,
                            TransactionId = response.PackedTransaction,
                            SignerActor = account.Data.Account,
                            SignerPermission = account.Data.Authority
                        };
                        
                        try
                        {
                            await _esrSessionManager.SendCallbackAsync(callbackPayload);
                            System.Diagnostics.Trace.WriteLine("[ESRSIGNING] Callback sent via session manager");
                            
                            await DisplayAlert(
                                "Identity Verified",
                                $"Your identity has been verified as {account.Data.Account}@{account.Data.Authority}.\n\n" +
                                "The website should update shortly.",
                                "OK"
                            );
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] Session callback failed: {ex.Message}");
                            ShowIdentityProofDialog(response);
                        }
                    }
                    else
                    {
                        // No way to send callback - show proof to user
                        ShowIdentityProofDialog(response);
                    }
                }
                else
                {
                    // Transaction was broadcast
                    System.Diagnostics.Trace.WriteLine(
                        "[ESRSIGNING] Transaction broadcast completed"
                    );

                    await DisplayAlert(
                        "Transaction Signed",
                        $"Signature: {response.Signatures?.FirstOrDefault()?[..16]}...\n\n"
                            + $"The transaction has been broadcast to the blockchain.",
                        "OK"
                    );
                }
            }

            // Compute transaction ID from packed transaction (if available)
            var txId =
                response.PackedTransaction != null
                    ? ComputeTransactionId(response.SerializedTransaction)
                    : null;

            _completionSource?.TrySetResult(
                new EsrSigningResult
                {
                    Success = true,
                    TransactionId = txId,
                    Signatures = response.Signatures,
                    Account = account.Data.Account,
                    Permission = account.Data.Authority,
                }
            );

            await Navigation.PopModalAsync();
        }
        catch (Exception ex)
        {
            _completionSource?.TrySetResult(
                new EsrSigningResult { Success = false, Error = ex.Message }
            );

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
    
    /// <summary>
    /// Send callback to a specific URL
    /// </summary>
    private async Task SendCallbackToUrl(string url, EsrCallbackResponse response)
    {
        try
        {
            using var httpClient = new HttpClient();
            
            var account = _walletContext.ActiveAccount;
            var linkChannel = $"https://cb.anchor.link/{_esrSessionManager.LinkId}";
            var linkKey = _esrSessionManager.RequestPublicKey;
            var linkName = "NeoWallet";
            
            // Get the original ESR URI - this is REQUIRED for the callback
            var originalEsrUri = _request?.ToUri() ?? "";
            
            // Calculate expiration (5 minutes from now in ISO format)
            var expiration = DateTime.UtcNow.AddMinutes(5).ToString("yyyy-MM-ddTHH:mm:ss");
            
            // CRITICAL: Use the chain ID from the response (used for signing)
            var chainIdForCallback = !string.IsNullOrEmpty(response.ChainId) 
                ? response.ChainId 
                : _request?.ChainId ?? "";
            
            // CRITICAL: Include ALL required ESR callback fields per spec
            var callbackData = new
            {
                // Required callback fields
                sig = response.Signatures?.FirstOrDefault() ?? "",  // First signature
                tx = response.PackedTransaction ?? "",               // Transaction ID (hex)
                sa = account?.Data.Account ?? "",                    // Signer account
                sp = account?.Data.Authority ?? "",                  // Signer permission
                rbn = response.RefBlockNum?.ToString() ?? "0",       // Reference block num
                rid = response.RefBlockPrefix?.ToString() ?? "0",    // Reference block prefix (uint32)
                ex = expiration,                                     // Expiration time
                req = originalEsrUri,                                // Original ESR request URI
                cid = chainIdForCallback,                            // Chain ID from signing
                
                // Optional: block number if broadcast
                bn = response.BlockNum?.ToString(),
                
                // Anchor Link session info
                link_ch = linkChannel,
                link_key = linkKey,
                link_name = linkName,
            };

            var json = System.Text.Json.JsonSerializer.Serialize(callbackData);
            System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] Sending callback to {url}");
            System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] Callback payload: {json}");
            
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var callbackResponse = await httpClient.PostAsync(url, content);

            System.Diagnostics.Trace.WriteLine(
                $"[ESRSIGNING] HTTP callback response: {callbackResponse.StatusCode}"
            );
            
            var responseBody = await callbackResponse.Content.ReadAsStringAsync();
            System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] Response body: {responseBody}");

            if (callbackResponse.IsSuccessStatusCode)
            {
                await DisplayAlert(
                    "Success",
                    "Identity verified successfully!",
                    "OK"
                );
            }
            else
            {
                await DisplayAlert(
                    "Warning",
                    $"Callback returned status {callbackResponse.StatusCode}",
                    "OK"
                );
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[ESRSIGNING] Callback to {url} failed: {ex.Message}");
            await DisplayAlert(
                "Warning",
                $"Failed to send callback: {ex.Message}",
                "OK"
            );
        }
    }
    
    /// <summary>
    /// Show identity proof dialog when no callback method is available
    /// </summary>
    private async void ShowIdentityProofDialog(EsrCallbackResponse response)
    {
        var account = _walletContext.ActiveAccount;
        var signature = response.Signatures?.FirstOrDefault();
        
        await DisplayAlert(
            "Identity Proof Generated",
            $"Account: {account?.Data.Account}@{account?.Data.Authority}\n" +
            $"Signature: {signature?[..Math.Min(16, signature?.Length ?? 0)]}...\n\n" +
            $"Chain: {_request?.ChainId?[..Math.Min(8, _request?.ChainId?.Length ?? 0)]}...\n\n" +
            "Note: The website may need to be refreshed manually as no callback URL was provided.",
            "OK"
        );
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

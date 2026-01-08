using System.Text.Json;
using SUS.EOS.NeoWallet.Services.Interfaces;
using SUS.EOS.Sharp.Services;
using Microsoft.Maui.Controls.Shapes;

namespace SUS.EOS.NeoWallet.Pages;

public partial class ContractActionsPage : ContentPage
{
    private readonly IAntelopeBlockchainClient _blockchainClient;
    private readonly IAntelopeTransactionService _transactionService;
    private readonly IWalletAccountService _accountService;
    private readonly IWalletStorageService _storageService;
    private readonly IWalletContextService _contextService;
    private string? _lastTransactionId;
    private List<string> _availableActions = [];

    public ContractActionsPage(
        IAntelopeBlockchainClient blockchainClient,
        IAntelopeTransactionService transactionService,
        IWalletAccountService accountService,
        IWalletStorageService storageService,
        IWalletContextService contextService
    )
    {
        InitializeComponent();
        _blockchainClient = blockchainClient;
        _transactionService = transactionService;
        _accountService = accountService;
        _storageService = storageService;
        _contextService = contextService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await LoadCurrentAccountAsync();

        var contract = _contextService.ActiveAccount?.Data.Account ?? "eosio.token";
        if (!string.IsNullOrEmpty(contract))
        {
            ContractEntry.Text = contract;
            OnLoadAbiClicked(this, EventArgs.Empty);
        }
    }

    private async Task LoadCurrentAccountAsync()
    {
        try
        {
            System.Diagnostics.Trace.WriteLine("[CONTRACTACTIONS] Loading current account");
            var currentAccount = await _accountService.GetCurrentAccountAsync();
            if (currentAccount != null)
            {
                System.Diagnostics.Trace.WriteLine($"[CONTRACTACTIONS] Current account: {currentAccount.Data.Account}");
                ActorEntry.Text = currentAccount.Data.Account;
                PermissionEntry.Text = currentAccount.Data.Authority;
                return;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[CONTRACTACTIONS] GetCurrentAccountAsync failed: {ex.Message}");
        }

        // Fallback to context service
        var activeAccount = _contextService.ActiveAccount;
        if (activeAccount != null)
        {
            System.Diagnostics.Trace.WriteLine($"[CONTRACTACTIONS] Using active account from context: {activeAccount.Data.Account}");
            ActorEntry.Text = activeAccount.Data.Account;
            PermissionEntry.Text = activeAccount.Data.Authority;
        }
        else
        {
            System.Diagnostics.Trace.WriteLine("[CONTRACTACTIONS] No account found");
            ActorEntry.Text = string.Empty;
            ActorEntry.Placeholder = "Enter account name";
        }
    }

    private async void OnLoadAbiClicked(object sender, EventArgs e)
    {
        try
        {
            LoadingIndicator.IsRunning = true;
            LoadingIndicator.IsVisible = true;

            var contract = ContractEntry.Text?.Trim();
            if (string.IsNullOrWhiteSpace(contract))
            {
                await DisplayAlertAsync("Error", "Please enter a contract name", "OK");
                return;
            }

            // Get contract ABI from blockchain
            try
            {
                var abi = await _blockchainClient.GetAbiAsync(contract, CancellationToken.None);
                
                if (abi?.Actions != null && abi.Actions.Any())
                {
                    _availableActions = abi.Actions
                        .Select(a => a.Name)
                        .OrderBy(name => name)
                        .ToList();
                }
                else
                {
                    // Fallback to common action names if ABI fetch fails or has no actions
                    _availableActions = ["transfer", "issue", "retire", "open", "close"];
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[CONTRACTACTIONS] Failed to get ABI: {ex.Message}");
                // Fallback to common action names
                _availableActions = ["transfer", "issue", "retire", "open", "close"];
            }

            ActionPicker.Items.Clear();
            foreach (var action in _availableActions)
            {
                ActionPicker.Items.Add(action);
            }

            ActionPickerSection.IsVisible = true;
            System.Diagnostics.Trace.WriteLine($"[CONTRACTACTIONS] Loaded {_availableActions.Count} actions for {contract}");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Failed to load ABI: {ex.Message}", "OK");
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        }
    }

    private async void OnActionSelected(object sender, EventArgs e)
    {
        if (ActionPicker.SelectedIndex >= 0)
        {
            var action = ActionPicker.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(action)) return;

            ActionDescriptionLabel.Text = $"Selected action: {action}";
            ActionDescriptionLabel.IsVisible = true;
            ParametersBorder.IsVisible = true;
            ResultBorder.IsVisible = false;

            // Always regenerate fields when action changes
            DynamicFieldsContainer.Children.Clear();
            await GenerateDynamicFieldsAsync(action);
        }
    }

    private async Task GenerateDynamicFieldsAsync(string actionName)
    {
        try
        {
            DynamicFieldsContainer.Children.Clear();

            var contract = ContractEntry.Text?.Trim();
            if (string.IsNullOrWhiteSpace(contract)) return;

            // Get ABI
            var abi = await _blockchainClient.GetAbiAsync(contract, CancellationToken.None);
            if (abi == null) return;

            // Get action type
            var actionType = abi.GetActionType(actionName);
            if (string.IsNullOrEmpty(actionType)) return;

            // Get struct for this action
            var actionStruct = abi.GetStruct(actionType);
            if (actionStruct == null) return;

            // Generate fields
            var fields = actionStruct.GetAllFields(abi).ToList();
            foreach (var field in fields)
            {
                var fieldStack = new VerticalStackLayout { Spacing = 4 };
                
                var label = new Label
                {
                    Text = $"{field.Name} ({field.Type})",
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold
                };
                
                var entry = new Entry
                {
                    Placeholder = $"Enter {field.Name}",
                    StyleId = field.Name // Use StyleId to identify the field later
                };

                // Pre-fill common fields
                if (field.Name == "from" && !string.IsNullOrWhiteSpace(ActorEntry.Text))
                {
                    entry.Text = ActorEntry.Text;
                }
                else if (field.Name == "actor" && !string.IsNullOrWhiteSpace(ActorEntry.Text))
                {
                    entry.Text = ActorEntry.Text;
                }

                fieldStack.Children.Add(label);
                fieldStack.Children.Add(entry);
                DynamicFieldsContainer.Children.Add(fieldStack);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[CONTRACTACTIONS] Failed to generate fields: {ex.Message}");
        }
    }

    private async void OnExecuteActionClicked(object sender, EventArgs e)
    {
        try
        {
            LoadingIndicator.IsRunning = true;
            LoadingIndicator.IsVisible = true;

            var contract = ContractEntry.Text?.Trim();
            var action = ActionPicker.SelectedItem?.ToString();
            var actor = ActorEntry.Text?.Trim();
            var permission = PermissionEntry.Text?.Trim() ?? "active";

            if (string.IsNullOrWhiteSpace(contract) || string.IsNullOrWhiteSpace(action))
            {
                await DisplayAlertAsync("Error", "Please select a contract and action", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(actor))
            {
                await DisplayAlertAsync("Error", "Please enter an account name in the Actor field", "OK");
                return;
            }

            // Build parameters from dynamic fields
            var parameters = new Dictionary<string, object>();
            foreach (var child in DynamicFieldsContainer.Children)
            {
                if (child is VerticalStackLayout fieldStack)
                {
                    var entry = fieldStack.Children.OfType<Entry>().FirstOrDefault();
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.StyleId))
                    {
                        var fieldName = entry.StyleId;
                        var fieldValue = entry.Text ?? "";
                        parameters[fieldName] = fieldValue;
                    }
                }
            }

            if (parameters.Count == 0)
            {
                await DisplayAlertAsync("Error", "Please fill in the action parameters", "OK");
                return;
            }

            // Confirm action
            var parametersDisplay = string.Join("\n", parameters.Select(p => $"{p.Key}: {p.Value}"));
            var confirm = await DisplayAlertAsync(
                "Confirm Action",
                $"Execute {action} on {contract}?\n\nParameters:\n{parametersDisplay}",
                "Execute",
                "Cancel"
            );

            if (!confirm)
                return;

            // Prompt for password to decrypt private key
            System.Diagnostics.Trace.WriteLine("[CONTRACTACTIONS] Prompting for password");
            var password = await ShowPasswordDialogAsync();

            if (string.IsNullOrWhiteSpace(password))
            {
                System.Diagnostics.Trace.WriteLine("[CONTRACTACTIONS] Password prompt cancelled");
                return;
            }

            // Get chain info to retrieve chainId
            System.Diagnostics.Trace.WriteLine("[CONTRACTACTIONS] Getting chain info");
            var chainInfo = await _blockchainClient.GetInfoAsync(CancellationToken.None);
            System.Diagnostics.Trace.WriteLine($"[CONTRACTACTIONS] Chain ID: {chainInfo.ChainId}");

            // Get private key for signing
            System.Diagnostics.Trace.WriteLine($"[CONTRACTACTIONS] Getting private key for {actor}@{permission}");
            var privateKey = await _accountService.GetPrivateKeyAsync(
                actor,
                permission,
                chainInfo.ChainId,
                password
            );

            if (string.IsNullOrEmpty(privateKey))
            {
                await DisplayAlertAsync("Error", "Failed to get private key. Please check your password.", "OK");
                return;
            }

            // Build and sign transaction using proper EOSIO transaction format
            try
            {
                System.Diagnostics.Trace.WriteLine($"[CONTRACTACTIONS] Building transaction for {contract}.{action}");
                
                // Build and sign transaction using ABI-based serialization
                var result = await _transactionService.BuildAndSignWithAbiAsync(
                    client: _blockchainClient,
                    chainInfo: chainInfo,
                    actor: actor,
                    privateKeyWif: privateKey,
                    contract: contract,
                    action: action,
                    data: parameters!,
                    authority: permission
                );

                System.Diagnostics.Trace.WriteLine($"[CONTRACTACTIONS] Transaction signed with {result.Signatures.Count} signatures");

                // Broadcast transaction with all required fields
                var pushResult = await _blockchainClient.PushTransactionAsync(new
                {
                    signatures = result.Signatures,
                    compression = 0,
                    packed_context_free_data = "",
                    packed_trx = result.PackedTransaction
                }, CancellationToken.None);
                
                System.Diagnostics.Trace.WriteLine($"[CONTRACTACTIONS] Transaction pushed: {pushResult.TransactionId}");

                _lastTransactionId = pushResult.TransactionId;

                ResultStatusLabel.Text = "✅ Success";
                ResultStatusLabel.TextColor = Colors.Green;
                TransactionIdLabel.Text = $"Transaction ID: {_lastTransactionId}";
                ResultBorder.IsVisible = true;

                await DisplayAlertAsync(
                    "Success",
                    $"Action executed successfully!\n\nTransaction ID:\n{_lastTransactionId}",
                    "OK"
                );
            }
            catch (Exception txEx)
            {
                System.Diagnostics.Trace.WriteLine($"[CONTRACTACTIONS] Transaction failed: {txEx.Message}");
                throw new InvalidOperationException($"Transaction failed: {txEx.Message}", txEx);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[CONTRACTACTIONS] Transaction execution failed: {ex.Message}");
            System.Diagnostics.Trace.WriteLine($"[CONTRACTACTIONS] Stack trace: {ex.StackTrace}");
            
            ResultStatusLabel.Text = "❌ Failed";
            ResultStatusLabel.TextColor = Colors.Red;
            TransactionIdLabel.Text = $"Error: {ex.Message}";
            ResultBorder.IsVisible = true;

            await DisplayAlertAsync("Error", $"Failed to execute action:\n\n{ex.Message}", "OK");
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        }
    }

    private async void OnViewTransactionClicked(object sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(_lastTransactionId))
        {
            var url = $"https://waxblock.io/transaction/{_lastTransactionId}";
            await Launcher.OpenAsync(url);
        }
    }

    private async void OnViewTablesClicked(object sender, EventArgs e)
    {
        var contract = ContractEntry.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(contract))
        {
            await Shell.Current.GoToAsync($"//ContractTablesPage?contract={contract}");
        }
        else
        {
            await Shell.Current.GoToAsync("ContractTablesPage");
        }
    }

    private async void OnViewContractOnBloksClicked(object sender, EventArgs e)
    {
        var contract = ContractEntry.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(contract))
        {
            var url = $"https://waxblock.io/account/{contract}";
            await Launcher.OpenAsync(url);
        }
        else
        {
            await DisplayAlertAsync("Error", "Please enter a contract name first", "OK");
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async Task<string?> ShowPasswordDialogAsync()
    {
        var tcs = new TaskCompletionSource<string?>();

        var dialogPage = new ContentPage
        {
            BackgroundColor = Color.FromArgb("#80000000"),
        };

        dialogPage.Disappearing += (s, e) => 
        { 
            System.Diagnostics.Trace.WriteLine("[CONTRACTACTIONS] Password dialog disappearing (dismissed)");
            tcs.TrySetResult(null); 
        };

        var entry = new Entry
        {
            Placeholder = "Enter password",
            IsPassword = true,
            BackgroundColor = Colors.White,
            TextColor = Colors.Black,
            FontSize = 16,
            Margin = new Thickness(0, 10),
        };

        var okButton = new Button
        {
            Text = "OK",
            BackgroundColor = Color.FromArgb("#007AFF"),
            TextColor = Colors.White,
            CornerRadius = 8,
            Padding = new Thickness(20, 10),
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#8E8E93"),
            TextColor = Colors.White,
            CornerRadius = 8,
            Padding = new Thickness(20, 10),
        };

        okButton.Clicked += async (s, e) =>
        {
            System.Diagnostics.Trace.WriteLine("[CONTRACTACTIONS] OK button clicked in password dialog");
            var result = entry.Text;
            tcs.TrySetResult(result);
            await Navigation.PopModalAsync();
        };

        cancelButton.Clicked += async (s, e) =>
        {
            System.Diagnostics.Trace.WriteLine("[CONTRACTACTIONS] Cancel button clicked in password dialog");
            tcs.TrySetResult(null);
            await Navigation.PopModalAsync();
        };

        entry.Completed += async (s, e) =>
        {
            System.Diagnostics.Trace.WriteLine("[CONTRACTACTIONS] Enter key pressed in password dialog");
            var result = entry.Text;
            tcs.TrySetResult(result);
            await Navigation.PopModalAsync();
        };

        var dialogFrame = new Border
        {
            BackgroundColor = Colors.White,
            StrokeThickness = 0,
            Padding = 20,
            MaximumWidthRequest = 400,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Content = new VerticalStackLayout
            {
                Spacing = 15,
                Children =
                {
                    new Label
                    {
                        Text = "Password Required",
                        FontSize = 20,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Colors.Black,
                        HorizontalOptions = LayoutOptions.Center,
                    },
                    new Label
                    {
                        Text = "Enter your wallet password to sign the transaction:",
                        FontSize = 14,
                        TextColor = Color.FromArgb("#666666"),
                        HorizontalOptions = LayoutOptions.Start,
                    },
                    entry,
                    new HorizontalStackLayout
                    {
                        Spacing = 10,
                        HorizontalOptions = LayoutOptions.End,
                        Children = { cancelButton, okButton },
                    },
                },
            },
        };

        dialogPage.Content = dialogFrame;

        await Navigation.PushModalAsync(dialogPage, true);
        entry.Focus();

        return await tcs.Task;
    }
}

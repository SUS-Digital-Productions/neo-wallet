using System.Text.Json;
using SUS.EOS.NeoWallet.Services.Interfaces;
using SUS.EOS.Sharp.Services;

namespace SUS.EOS.NeoWallet.Pages;

public partial class ContractActionsPage : ContentPage
{
    private readonly IAntelopeBlockchainClient _blockchainClient;
    private readonly IAntelopeTransactionService _transactionService;
    private readonly IWalletAccountService _accountService;
    private readonly IWalletStorageService _storageService;
    private readonly IWalletContextService _contextService;
    private string? _lastTransactionId;
    private List<string> _availableActions = new();

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

        LoadCurrentAccount();
        _contextService = contextService;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        var contract = _contextService.ActiveAccount?.Data.Account ?? "eosio";
        if (!string.IsNullOrEmpty(contract))
        {
            ContractEntry.Text = contract;
            OnLoadAbiClicked(this, EventArgs.Empty);
        }
    }

    private async void LoadCurrentAccount()
    {
        try
        {
            var currentAccount = await _accountService.GetCurrentAccountAsync();
            if (currentAccount != null)
            {
                ActorLabel.Text = currentAccount.Data.Account;
                PermissionEntry.Text = currentAccount.Data.Authority;
            }
        }
        catch
        {
            ActorLabel.Text = "No account selected";
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

            // TODO: Get contract ABI from blockchain
            // For now, use common action names
            _availableActions = new List<string> { "transfer", "issue", "retire", "open", "close" };

            ActionPicker.Items.Clear();
            foreach (var action in _availableActions)
            {
                ActionPicker.Items.Add(action);
            }

            ActionPickerSection.IsVisible = true;
            await DisplayAlertAsync(
                "Success",
                $"Loaded {_availableActions.Count} actions for {contract}",
                "OK"
            );
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

    private void OnActionSelected(object sender, EventArgs e)
    {
        if (ActionPicker.SelectedIndex >= 0)
        {
            var action = ActionPicker.SelectedItem.ToString();
            ActionDescriptionLabel.Text = $"Selected action: {action}";
            ActionDescriptionLabel.IsVisible = true;
            ParametersBorder.IsVisible = true;
            ResultBorder.IsVisible = false;

            // Set default JSON template
            ParametersEditor.Text = "{\n  \n}";
        }
    }

    private void OnTemplateTransferClicked(object sender, EventArgs e)
    {
        ParametersEditor.Text =
            @"{
  ""from"": """
            + ActorLabel.Text
            + @""",
  ""to"": ""receiver"",
  ""quantity"": ""1.0000 WAX"",
  ""memo"": ""Transfer from wallet""
}";
    }

    private void OnTemplateIssueClicked(object sender, EventArgs e)
    {
        ParametersEditor.Text =
            @"{
  ""to"": """
            + ActorLabel.Text
            + @""",
  ""quantity"": ""100.0000 TOKEN"",
  ""memo"": ""Issue tokens""
}";
    }

    private async void OnExecuteActionClicked(object sender, EventArgs e)
    {
        try
        {
            LoadingIndicator.IsRunning = true;
            LoadingIndicator.IsVisible = true;

            var contract = ContractEntry.Text?.Trim();
            var action = ActionPicker.SelectedItem?.ToString();
            var parametersJson = ParametersEditor.Text?.Trim();
            var actor = ActorLabel.Text;
            var permission = PermissionEntry.Text?.Trim() ?? "active";

            if (string.IsNullOrWhiteSpace(contract) || string.IsNullOrWhiteSpace(action))
            {
                await DisplayAlertAsync("Error", "Please select a contract and action", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(parametersJson))
            {
                await DisplayAlertAsync("Error", "Please enter action parameters", "OK");
                return;
            }

            if (actor == "No account selected")
            {
                await DisplayAlertAsync("Error", "Please select an account first", "OK");
                return;
            }

            // Parse JSON parameters
            Dictionary<string, object>? parameters;
            try
            {
                parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(parametersJson);
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Error", $"Invalid JSON: {ex.Message}", "OK");
                return;
            }

            // Confirm action
            var confirm = await DisplayAlertAsync(
                "Confirm Action",
                $"Execute {action} on {contract}?\n\nParameters:\n{parametersJson}",
                "Execute",
                "Cancel"
            );

            if (!confirm)
                return;

            // Prompt for password if wallet is locked
            string? password = null;
            if (!_storageService.IsUnlocked)
            {
                password = await DisplayPromptAsync(
                    "Password Required",
                    "Enter your wallet password to sign the transaction:",
                    placeholder: "Password"
                );

                if (string.IsNullOrWhiteSpace(password))
                {
                    return;
                }
            }

            // Get private key for signing
            var privateKey = await _accountService.GetPrivateKeyAsync(
                actor,
                permission,
                "", // chainId will be determined from network
                password ?? string.Empty
            );

            if (string.IsNullOrEmpty(privateKey))
            {
                await DisplayAlertAsync("Error", "Failed to get private key", "OK");
                return;
            }

            // Get chain info
            var chainInfo = await _blockchainClient.GetInfoAsync(CancellationToken.None);

            // TODO: Build and sign transaction using proper EOSIO transaction format
            // For now, show what would be executed
            await DisplayAlertAsync(
                "TODO",
                $"Transaction execution is being implemented.\n\n"
                    + $"Contract: {contract}\n"
                    + $"Action: {action}\n"
                    + $"Actor: {actor}@{permission}\n"
                    + $"Data: {parametersJson}\n"
                    + $"Chain: {chainInfo.ChainId}",
                "OK"
            );

            // Placeholder for success
            _lastTransactionId = "pending_implementation";

            ResultStatusLabel.Text = "⏳ Pending";
            ResultStatusLabel.TextColor = Colors.Orange;
            TransactionIdLabel.Text = "Transaction execution being implemented";
            ResultBorder.IsVisible = true;
        }
        catch (Exception ex)
        {
            ResultStatusLabel.Text = "❌ Failed";
            ResultStatusLabel.TextColor = Colors.Red;
            TransactionIdLabel.Text = $"Error: {ex.Message}";
            ResultBorder.IsVisible = true;

            await DisplayAlertAsync("Error", $"Failed to execute action: {ex.Message}", "OK");
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
        await Shell.Current.GoToAsync("..");
    }
}

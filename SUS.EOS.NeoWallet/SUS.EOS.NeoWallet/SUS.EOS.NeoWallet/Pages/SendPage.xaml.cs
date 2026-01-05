using SUS.EOS.NeoWallet.Services;
using SUS.EOS.NeoWallet.Services.Interfaces;
using SUS.EOS.Sharp.Services;

namespace SUS.EOS.NeoWallet.Pages;

public partial class SendPage : ContentPage
{
    private readonly IWalletAccountService _accountService;
    private readonly IWalletStorageService _storageService;
    private readonly IAntelopeBlockchainClient _blockchainClient;
    private readonly IBlockchainOperationsService _operationsService;
    private readonly IPriceFeedService _priceFeedService;

    public SendPage(
        IWalletAccountService accountService,
        IWalletStorageService storageService,
        IAntelopeBlockchainClient blockchainClient,
        IBlockchainOperationsService operationsService,
        IPriceFeedService priceFeedService)
    {
        InitializeComponent();
        _accountService = accountService;
        _storageService = storageService;
        _blockchainClient = blockchainClient;
        _operationsService = operationsService;
        _priceFeedService = priceFeedService;
        
        AssetPicker.SelectedIndex = 0;
        LoadAvailableBalance();
    }

    private async void LoadAvailableBalance()
    {
        try
        {
            var currentAccount = await _accountService.GetCurrentAccountAsync();
            if (currentAccount == null)
            {
                AvailableLabel.Text = "No account";
                return;
            }
            
            var balances = await _blockchainClient.GetCurrencyBalanceAsync(
                "eosio.token",
                currentAccount.Data.Account,
                null,
                CancellationToken.None
            );
            
            if (balances.Any())
            {
                AvailableLabel.Text = $"Available: {balances.First()}";
            }
            else
            {
                AvailableLabel.Text = "Available: 0.0000 WAX";
            }
        }
        catch
        {
            AvailableLabel.Text = "Available: 0.0000 WAX";
        }
    }

    private void OnAssetChanged(object sender, EventArgs e)
    {
        // TODO: Update available balance
        var selectedAsset = AssetPicker.SelectedItem?.ToString() ?? "NEO";
        AvailableLabel.Text = $"Available: 0 {selectedAsset}";
    }

    private async void OnScanAddressClicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync("QR Scanner", "QR code scanning not yet implemented", "OK");
    }

    private async void OnAmountChanged(object sender, TextChangedEventArgs e)
    {
        if (decimal.TryParse(e.NewTextValue, out var amount) && amount > 0)
        {
            var asset = AssetPicker.SelectedItem?.ToString() ?? "WAX";
            var usdValue = await _priceFeedService.ConvertToUsdAsync(asset, amount);
            AmountUsdLabel.Text = $"≈ ${usdValue:F2} USD";
        }
        else
        {
            AmountUsdLabel.Text = "≈ $0.00 USD";
        }
        SendButton.IsEnabled = !string.IsNullOrWhiteSpace(RecipientEntry.Text) && !string.IsNullOrWhiteSpace(e.NewTextValue);
    }

    private void OnMaxClicked(object sender, EventArgs e)
    {
        // TODO: Set max available amount
        AmountEntry.Text = "0";
    }

    private void OnSlowFeeClicked(object sender, EventArgs e)
    {
        UpdateFeeSelection("slow");
    }

    private void OnNormalFeeClicked(object sender, EventArgs e)
    {
        UpdateFeeSelection("normal");
    }

    private void OnFastFeeClicked(object sender, EventArgs e)
    {
        UpdateFeeSelection("fast");
    }

    private void UpdateFeeSelection(string speed)
    {
        // Reset all borders
        SlowFeeBorder.BackgroundColor = Colors.Transparent;
        SlowFeeBorder.Stroke = (Color)Application.Current!.Resources["Border"];
        NormalFeeBorder.BackgroundColor = Colors.Transparent;
        NormalFeeBorder.Stroke = (Color)Application.Current!.Resources["Border"];
        FastFeeBorder.BackgroundColor = Colors.Transparent;
        FastFeeBorder.Stroke = (Color)Application.Current!.Resources["Border"];

        // Highlight selected
        var primaryColor = (Color)Application.Current!.Resources["Primary"];
        switch (speed)
        {
            case "slow":
                SlowFeeBorder.BackgroundColor = primaryColor;
                SlowFeeBorder.Stroke = primaryColor;
                break;
            case "normal":
                NormalFeeBorder.BackgroundColor = primaryColor;
                NormalFeeBorder.Stroke = primaryColor;
                break;
            case "fast":
                FastFeeBorder.BackgroundColor = primaryColor;
                FastFeeBorder.Stroke = primaryColor;
                break;
        }
    }

    private async void OnSendClicked(object sender, EventArgs e)
    {
        try
        {
            var amount = AmountEntry.Text;
            var recipient = RecipientEntry.Text;
            var memo = MemoEntry.Text ?? "";
            var asset = AssetPicker.SelectedItem?.ToString() ?? "WAX";
            
            var result = await DisplayAlertAsync("Confirm Transaction",
                $"Send {amount} {asset} to {recipient}?",
                "Confirm", "Cancel");

            if (!result) return;

            SendButton.IsEnabled = false;
            
            // Get current account
            var currentAccount = await _accountService.GetCurrentAccountAsync();
            if (currentAccount == null)
            {
                await DisplayAlertAsync("Error", "No active account", "OK");
                SendButton.IsEnabled = true;
                return;
            }
            
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
                    SendButton.IsEnabled = true;
                    return;
                }
            }
            
            // Get private key for signing
            var privateKey = await _accountService.GetPrivateKeyAsync(
                currentAccount.Data.Account,
                currentAccount.Data.Authority,
                currentAccount.Data.ChainId,
                password ?? ""
            );
            
            if (string.IsNullOrEmpty(privateKey))
            {
                await DisplayAlertAsync("Error", "Could not decrypt private key. Check your password.", "OK");
                SendButton.IsEnabled = true;
                return;
            }
            
            // Create blockchain operations service
            var transactionService = new SUS.EOS.Sharp.Services.AntelopeTransactionService();
            var operationsService = new SUS.EOS.Sharp.Services.BlockchainOperationsService(
                _blockchainClient, transactionService);
            
            // Format amount (assumes 4 decimal places for most tokens)
            var formattedAmount = decimal.Parse(amount).ToString("F4");
            
            // Send transaction
            var txResult = await operationsService.TransferAsync(
                currentAccount.Data.Account,
                recipient,
                formattedAmount,
                asset,
                memo,
                privateKey
            );
            
            await DisplayAlertAsync("Success",
                $"Transaction sent!\nID: {txResult.TransactionId}",
                "OK");
            
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Failed to send transaction: {ex.Message}", "OK");
            SendButton.IsEnabled = true;
        }
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}

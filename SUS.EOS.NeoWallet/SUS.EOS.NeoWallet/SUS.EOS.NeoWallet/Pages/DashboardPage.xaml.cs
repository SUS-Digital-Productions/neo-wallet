using SUS.EOS.NeoWallet.Services;
using SUS.EOS.NeoWallet.Services.Interfaces;
using SUS.EOS.Sharp.Services;

namespace SUS.EOS.NeoWallet.Pages;

public partial class DashboardPage : ContentPage
{
    private readonly IWalletAccountService _accountService;
    private readonly IAntelopeBlockchainClient _blockchainClient;
    private readonly INetworkService _networkService;
    private readonly IPriceFeedService _priceFeedService;

    public DashboardPage(
        IWalletAccountService accountService,
        IAntelopeBlockchainClient blockchainClient,
        INetworkService networkService,
        IPriceFeedService priceFeedService)
    {
        InitializeComponent();
        _accountService = accountService;
        _blockchainClient = blockchainClient;
        _networkService = networkService;
        _priceFeedService = priceFeedService;
        
        LoadDashboardData();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RefreshData();
    }

    private async void LoadDashboardData()
    {
        try
        {
            var currentAccount = await _accountService.GetCurrentAccountAsync();
            if (currentAccount == null)
            {
                TotalBalanceLabel.Text = "No account";
                NeoBalanceLabel.Text = "0 WAX";
                GasBalanceLabel.Text = "0 WAX";
                return;
            }
            
            // Get account balance
            var balances = await _blockchainClient.GetCurrencyBalanceAsync(
                "eosio.token",
                currentAccount.Data.Account,
                null,
                CancellationToken.None
            );
            
            if (balances.Any())
            {
                var mainBalance = balances.First();
                var parts = mainBalance.Split(' ');
                var amount = decimal.TryParse(parts[0], out var amt) ? amt : 0;
                var symbol = parts.Length > 1 ? parts[1] : "WAX";
                
                // Get price
                var usdValue = await _priceFeedService.ConvertToUsdAsync(symbol, amount);
                
                TotalBalanceLabel.Text = $"${usdValue:F2} USD";
                NeoBalanceLabel.Text = mainBalance;
                GasBalanceLabel.Text = mainBalance;
                NeoAmountLabel.Text = mainBalance;
                GasAmountLabel.Text = mainBalance;
                NeoUsdLabel.Text = $"${usdValue:F2}";
                GasUsdLabel.Text = $"${usdValue:F2}";
            }
            else
            {
                TotalBalanceLabel.Text = "0.0000 WAX";
                NeoBalanceLabel.Text = "0.0000 WAX";
                GasBalanceLabel.Text = "0.0000 WAX";
                NeoAmountLabel.Text = "0.0000 WAX";
                GasAmountLabel.Text = "0.0000 WAX";
                NeoUsdLabel.Text = "$0.00";
                GasUsdLabel.Text = "$0.00";
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Failed to load balance: {ex.Message}", "OK");
            TotalBalanceLabel.Text = "Error loading balance";
        }
    }

    private void RefreshData()
    {
        LoadDashboardData();
    }

    private async void OnSendClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("SendPage");
    }

    private async void OnStakeClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("StakingPage");
    }

    private async void OnViewAllTransactionsClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("WalletPage");
    }
}

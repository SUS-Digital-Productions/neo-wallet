using System.Collections.ObjectModel;
using SUS.EOS.NeoWallet.Services.Interfaces;
using SUS.EOS.NeoWallet.Services.Models;

namespace SUS.EOS.NeoWallet.Pages;

public partial class AccountManagementPage : ContentPage
{
    private readonly IWalletStorageService _storageService;
    private readonly IWalletAccountService _accountService;
    private readonly IWalletContextService _walletContext;
    private readonly INetworkService _networkService;

    public ObservableCollection<AccountItemViewModel> Accounts { get; } = new();

    public AccountManagementPage(
        IWalletStorageService storageService,
        IWalletAccountService accountService,
        IWalletContextService walletContext,
        INetworkService networkService)
    {
        InitializeComponent();
        _storageService = storageService;
        _accountService = accountService;
        _walletContext = walletContext;
        _networkService = networkService;

        AccountsCollectionView.ItemsSource = Accounts;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAccountsAsync();
    }

    private async Task LoadAccountsAsync()
    {
        try
        {
            LoadingIndicator.IsVisible = true;
            LoadingIndicator.IsRunning = true;

            Accounts.Clear();

            var wallet = await _storageService.LoadWalletAsync();
            if (wallet?.Wallets == null || wallet.Wallets.Count == 0)
            {
                EmptyStateLayout.IsVisible = true;
                AccountsCollectionView.IsVisible = false;
                return;
            }

            EmptyStateLayout.IsVisible = false;
            AccountsCollectionView.IsVisible = true;

            var networks = await _networkService.GetNetworksAsync();

            foreach (var account in wallet.Wallets)
            {
                var chainName = "Unknown Chain";
                var networkEntry = networks.FirstOrDefault(n => n.Value.ChainId == account.Data.ChainId);
                if (!string.IsNullOrEmpty(networkEntry.Key))
                {
                    chainName = networkEntry.Value.Name;
                }

                Accounts.Add(new AccountItemViewModel
                {
                    Account = account,
                    AccountName = $"{account.Data.Account}@{account.Data.Authority}",
                    ChainName = chainName,
                    PublicKeyShort = account.Data.PublicKey.Length > 24
                        ? $"{account.Data.PublicKey[..12]}...{account.Data.PublicKey[^10..]}"
                        : account.Data.PublicKey
                });
            }

            System.Diagnostics.Trace.WriteLine($"[ACCOUNTMANAGEMENT] Loaded {Accounts.Count} accounts");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[ACCOUNTMANAGEMENT] Error loading accounts: {ex.Message}");
            await DisplayAlert("Error", $"Failed to load accounts: {ex.Message}", "OK");
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;
        }
    }

    private async void OnDeleteAccountClicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is Button button && button.CommandParameter is AccountItemViewModel accountItem)
            {
                var confirm = await DisplayAlert(
                    "Confirm Delete",
                    $"Are you sure you want to remove {accountItem.AccountName} from your wallet?\n\nThis will not delete the account on-chain, only remove it from this wallet.",
                    "Delete",
                    "Cancel"
                );

                if (!confirm)
                    return;

                LoadingIndicator.IsVisible = true;
                LoadingIndicator.IsRunning = true;

                // Remove the account
                await _accountService.RemoveAccountAsync(
                    accountItem.Account.Data.Account,
                    accountItem.Account.Data.Authority,
                    accountItem.Account.Data.ChainId
                );

                // If this was the active account, clear it
                if (_walletContext.ActiveAccount?.Data.Account == accountItem.Account.Data.Account &&
                    _walletContext.ActiveAccount?.Data.Authority == accountItem.Account.Data.Authority &&
                    _walletContext.ActiveAccount?.Data.ChainId == accountItem.Account.Data.ChainId)
                {
                    _walletContext.ClearActiveAccount();
                }

                // Refresh the list
                await LoadAccountsAsync();

                // Refresh wallet context
                await _walletContext.RefreshAsync();

                await DisplayAlert("Success", $"Removed {accountItem.AccountName}", "OK");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[ACCOUNTMANAGEMENT] Error deleting account: {ex.Message}");
            await DisplayAlert("Error", $"Failed to delete account: {ex.Message}", "OK");
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;
        }
    }
}

public class AccountItemViewModel
{
    public WalletAccount Account { get; set; } = null!;
    public string AccountName { get; set; } = string.Empty;
    public string ChainName { get; set; } = string.Empty;
    public string PublicKeyShort { get; set; } = string.Empty;
}

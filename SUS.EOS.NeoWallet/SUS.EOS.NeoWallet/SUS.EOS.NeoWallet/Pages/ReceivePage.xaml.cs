using SUS.EOS.NeoWallet.Services.Interfaces;

namespace SUS.EOS.NeoWallet.Pages;

public partial class ReceivePage : ContentPage
{
    private readonly IWalletAccountService _accountService;
    private string _accountName = "No account";
    private string _publicKey = "No key";

    public ReceivePage(IWalletAccountService accountService)
    {
        InitializeComponent();
        _accountService = accountService;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadWalletAddress();
    }

    private async void LoadWalletAddress()
    {
        try
        {
            var currentAccount = await _accountService.GetCurrentAccountAsync();
            if (currentAccount != null)
            {
                _accountName = currentAccount.Data.Account;
                _publicKey = currentAccount.Data.PublicKey;
                
                AccountNameLabel.Text = _accountName;
                AddressLabel.Text = _publicKey.Length > 40 
                    ? $"{_publicKey[..20]}...{_publicKey[^16..]}" 
                    : _publicKey;
                
                // TODO: Generate QR code for account name or ESR
            }
            else
            {
                AccountNameLabel.Text = "No account selected";
                AddressLabel.Text = "Import keys to view address";
            }
        }
        catch (Exception ex)
        {
            AccountNameLabel.Text = "Error loading";
            AddressLabel.Text = ex.Message;
        }
    }

    private async void OnCopyAccountClicked(object sender, EventArgs e)
    {
        await Clipboard.SetTextAsync(_accountName);
        CopyAccountButton.Text = "✓ Copied!";
        await Task.Delay(1500);
        CopyAccountButton.Text = "📋 Copy Account";
    }

    private async void OnCopyAddressClicked(object sender, EventArgs e)
    {
        await Clipboard.SetTextAsync(_publicKey);
        CopyAddressButton.Text = "✓ Copied!";
        await Task.Delay(1500);
        CopyAddressButton.Text = "📋 Copy Key";
    }

    private async void OnShareEmailClicked(object sender, EventArgs e)
    {
        try
        {
            await Email.ComposeAsync(new EmailMessage
            {
                Subject = "My Wallet Address",
                Body = $"Account: {_accountName}\nPublic Key: {_publicKey}"
            });
        }
        catch
        {
            await DisplayAlertAsync("Share", "Email app not available", "OK");
        }
    }

    private async void OnShareMessageClicked(object sender, EventArgs e)
    {
        try
        {
            await Sms.ComposeAsync(new SmsMessage
            {
                Body = $"My wallet: {_accountName}"
            });
        }
        catch
        {
            await DisplayAlertAsync("Share", "SMS not available", "OK");
        }
    }

    private async void OnShareMoreClicked(object sender, EventArgs e)
    {
        await Share.RequestAsync(new ShareTextRequest
        {
            Text = $"Account: {_accountName}\nPublic Key: {_publicKey}",
            Title = "Share Wallet Address"
        });
    }

    private async void OnDoneClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}

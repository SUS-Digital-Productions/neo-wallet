using SUS.EOS.NeoWallet.Services.Interfaces;

namespace SUS.EOS.NeoWallet.Pages;

public partial class WalletSetupPage : ContentPage
{
    private readonly IWalletStorageService _storageService;
    private int currentStep = 1;
    private string[] seedPhrase = Array.Empty<string>();

    public WalletSetupPage(IWalletStorageService storageService)
    {
        InitializeComponent();
        _storageService = storageService;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadMnemonicFromProperties();
    }

    private void LoadMnemonicFromProperties()
    {
        // Get mnemonic from Preferences (set by CreateAccountPage)
        var mnemonic = Preferences.Get("NewAccountMnemonic", string.Empty);
        var publicKey = Preferences.Get("NewAccountPublicKey", string.Empty);
        var walletName = Preferences.Get("NewAccountName", "My Wallet");
        
        // Display wallet info
        if (!string.IsNullOrEmpty(publicKey))
        {
            WalletInfoLabel.Text = $"Wallet: {walletName}\nPublic Key: {publicKey[..20]}...";
        }
        
        if (!string.IsNullOrEmpty(mnemonic))
        {
            seedPhrase = mnemonic.Split(' ');
            UpdateSeedPhraseDisplay();
        }
        else
        {
            // This shouldn't happen - redirect back
            _ = this.DisplayAlertAsync("Error", "No recovery phrase found. Please create a wallet first.", "OK");
        }
    }

    private void UpdateSeedPhraseDisplay()
    {
        if (seedPhrase.Length < 12) return;
        
        Word1.Text = $"1. {seedPhrase[0]}";
        Word2.Text = $"2. {seedPhrase[1]}";
        Word3.Text = $"3. {seedPhrase[2]}";
        Word4.Text = $"4. {seedPhrase[3]}";
        Word5.Text = $"5. {seedPhrase[4]}";
        Word6.Text = $"6. {seedPhrase[5]}";
        Word7.Text = $"7. {seedPhrase[6]}";
        Word8.Text = $"8. {seedPhrase[7]}";
        Word9.Text = $"9. {seedPhrase[8]}";
        Word10.Text = $"10. {seedPhrase[9]}";
        Word11.Text = $"11. {seedPhrase[10]}";
        Word12.Text = $"12. {seedPhrase[11]}";
    }

    private async void OnCopyPhraseClicked(object sender, EventArgs e)
    {
        await Clipboard.SetTextAsync(string.Join(" ", seedPhrase));
        await this.DisplayAlertAsync("Copied", "Recovery phrase copied to clipboard. Store it safely!", "OK");
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        if (currentStep > 1)
        {
            currentStep--;
            UpdateStepDisplay();
        }
        else
        {
            await Shell.Current.GoToAsync("CreateAccountPage");
        }
    }

    private async void OnNextClicked(object sender, EventArgs e)
    {
        if (currentStep < 3)
        {
            currentStep++;
            UpdateStepDisplay();
        }
        else
        {
            // Clear temporary data from preferences
            Preferences.Remove("NewAccountMnemonic");
            Preferences.Remove("NewAccountName");
            Preferences.Remove("NewAccountPublicKey");
            
            await this.DisplayAlertAsync("Success", "Wallet created successfully! You can now use your wallet.", "OK");
            await Shell.Current.GoToAsync("DashboardPage");
        }
    }

    private void UpdateStepDisplay()
    {
        // Update progress indicators
        Step1Frame.BackgroundColor = currentStep >= 1 ? Colors.Blue : Colors.LightGray;
        Step2Frame.BackgroundColor = currentStep >= 2 ? Colors.Blue : Colors.LightGray;
        Step3Frame.BackgroundColor = currentStep >= 3 ? Colors.Blue : Colors.LightGray;

        // TODO: Update step content based on currentStep
        NextButton.Text = currentStep == 3 ? "Finish" : "Next";
    }
}

namespace SUS.EOS.NeoWallet.Pages;

public partial class RecoverAccountPage : ContentPage
{
    public RecoverAccountPage()
    {
        InitializeComponent();
        RecoveryPhraseEditor.TextChanged += OnRecoveryPhraseChanged;
    }

    private void OnRecoveryPhraseChanged(object? sender, TextChangedEventArgs e)
    {
        var phrase = e.NewTextValue?.Trim() ?? string.Empty;
        var words = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (words.Length == 12 || words.Length == 24)
        {
            RecoverButton.IsEnabled = true;
            ValidationLabel.IsVisible = false;
        }
        else
        {
            RecoverButton.IsEnabled = false;
            if (!string.IsNullOrWhiteSpace(phrase))
            {
                ValidationLabel.Text = $"Recovery phrase must be 12 or 24 words (current: {words.Length})";
                ValidationLabel.TextColor = Colors.Red;
                ValidationLabel.IsVisible = true;
            }
        }
    }

    private async void OnScanQRClicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync("QR Scanner", "QR code scanning not yet implemented", "OK");
    }

    private async void OnRecoverClicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync("Recovery", "Wallet recovery in progress...", "OK");
        await Shell.Current.GoToAsync("EnterPasswordPage");
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}

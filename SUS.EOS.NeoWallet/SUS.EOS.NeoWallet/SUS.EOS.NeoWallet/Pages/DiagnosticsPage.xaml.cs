using SUS.EOS.NeoWallet.Services.Interfaces;

namespace SUS.EOS.NeoWallet.Pages;

public partial class DiagnosticsPage : ContentPage
{
    private readonly IAppDiagnosticsService _diagnostics;

    public DiagnosticsPage(IAppDiagnosticsService diagnostics)
    {
        InitializeComponent();
        _diagnostics = diagnostics;
        LoadEntries();
    }

    private void LoadEntries()
    {
        var entries = _diagnostics.GetEntries();
        LogsCollectionView.ItemsSource = entries;
    }

    private void OnRefreshClicked(object sender, EventArgs e)
    {
        LoadEntries();
    }

    private async void OnExportClicked(object sender, EventArgs e)
    {
        try
        {
            var tmp = Path.Combine(
                Path.GetTempPath(),
                $"neo-wallet-diagnostics-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log"
            );
            var path = await _diagnostics.ExportAsync(tmp);
            await DisplayAlertAsync("Exported", $"Diagnostics exported to: {path}", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Failed to export diagnostics: {ex.Message}", "OK");
        }
    }

    private async void OnClearClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlertAsync(
            "Confirm",
            "Clear diagnostic logs?",
            "Clear",
            "Cancel"
        );
        if (!confirm)
            return;
        _diagnostics.Clear();
        LoadEntries();
    }
}

using System.Text.Json;
using SUS.EOS.Sharp.Services;

namespace SUS.EOS.NeoWallet.Pages;

public partial class ContractTablesPage : ContentPage
{
    private readonly IAntelopeBlockchainClient _blockchainClient;
    private string? _lastJsonData;

    public ContractTablesPage(IAntelopeBlockchainClient blockchainClient)
    {
        InitializeComponent();
        _blockchainClient = blockchainClient;
    }

    private async void OnLoadTableClicked(object sender, EventArgs e)
    {
        try
        {
            LoadingIndicator.IsRunning = true;
            LoadingIndicator.IsVisible = true;
            ResultsBorder.IsVisible = false;

            var contract = ContractEntry.Text?.Trim();
            var scope = ScopeEntry.Text?.Trim();
            var table = TableEntry.Text?.Trim();

            if (string.IsNullOrWhiteSpace(contract) || string.IsNullOrWhiteSpace(scope) || string.IsNullOrWhiteSpace(table))
            {
                await DisplayAlertAsync("Error", "Please fill in contract, scope, and table name", "OK");
                return;
            }

            var limit = int.TryParse(LimitEntry.Text, out var l) ? l : 10;
            var lowerBound = LowerBoundEntry.Text?.Trim();
            var upperBound = UpperBoundEntry.Text?.Trim();
            var reverse = ReverseCheckBox.IsChecked;

            // Query table rows
            // Note: The current API doesn't support all query parameters yet
            // TODO: Extend GetTableRowsAsync to support limit, bounds, reverse
            var result = await _blockchainClient.GetTableRowsAsync<Dictionary<string, object>>(
                contract,
                scope,
                table,
                CancellationToken.None
            );

            // Format JSON for display
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            _lastJsonData = JsonSerializer.Serialize(result, options);
            TableDataLabel.Text = _lastJsonData;
            RowCountLabel.Text = $"{result.Rows.Count} rows found{(result.More ? " (more available)" : "")}";

            ResultsBorder.IsVisible = true;
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Failed to load table: {ex.Message}", "OK");
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        }
    }

    private async void OnCopyJsonClicked(object sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(_lastJsonData))
        {
            await Clipboard.SetTextAsync(_lastJsonData);
            await DisplayAlertAsync("Copied", "JSON data copied to clipboard", "OK");
        }
    }

    private async void OnPreviousPageClicked(object sender, EventArgs e)
    {
        // TODO: Implement pagination with cursor
        await DisplayAlertAsync("Info", "Pagination not yet implemented", "OK");
    }

    private async void OnNextPageClicked(object sender, EventArgs e)
    {
        // TODO: Implement pagination with next_key cursor
        await DisplayAlertAsync("Info", "Pagination not yet implemented", "OK");
    }

    private async void OnViewActionsClicked(object sender, EventArgs e)
    {
        var contract = ContractEntry.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(contract))
        {
            await Shell.Current.GoToAsync($"//ContractActionsPage?contract={contract}");
        }
        else
        {
            await DisplayAlertAsync("Error", "Please enter a contract name first", "OK");
        }
    }

    private async void OnViewOnBloksClicked(object sender, EventArgs e)
    {
        var contract = ContractEntry.Text?.Trim();
        var scope = ScopeEntry.Text?.Trim();
        var table = TableEntry.Text?.Trim();

        if (!string.IsNullOrWhiteSpace(contract))
        {
            var url = $"https://waxblock.io/account/{contract}?code={contract}&scope={scope}&table={table}#contract-tables";
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

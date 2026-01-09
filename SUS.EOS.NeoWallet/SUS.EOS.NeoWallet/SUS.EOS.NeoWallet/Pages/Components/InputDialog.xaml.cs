using System.Threading.Tasks;

namespace SUS.EOS.NeoWallet.Pages.Components;

public partial class InputDialog : ContentPage
{
    private TaskCompletionSource<string?> _tcs = new();

    public InputDialog(string title, string message, string accept = "OK", string cancel = "Cancel", bool isPassword = false)
    {
        InitializeComponent();
        TitleLabel.Text = title ?? string.Empty;
        MessageLabel.Text = message ?? string.Empty;
        AcceptButton.Text = accept ?? "OK";
        CancelButton.Text = cancel ?? "Cancel";
        InputEntry.IsPassword = isPassword;

        // Hook handlers
        AcceptButton.Clicked += AcceptButton_Clicked;
        CancelButton.Clicked += CancelButton_Clicked;
        InputEntry.Completed += InputEntry_Completed;

        // Handle dismissed modal
        this.Disappearing += (s, e) =>
        {
            if (!_tcs.Task.IsCompleted)
                _tcs.TrySetResult(null);
        };
    }

    private async void AcceptButton_Clicked(object? sender, EventArgs e)
    {
        // Set result first to avoid race with Disappearing
        _tcs.TrySetResult(InputEntry.Text);
        await Navigation.PopModalAsync();
    }

    private async void CancelButton_Clicked(object? sender, EventArgs e)
    {
        _tcs.TrySetResult(null);
        await Navigation.PopModalAsync();
    }

    private async void InputEntry_Completed(object? sender, EventArgs e)
    {
        _tcs.TrySetResult(InputEntry.Text);
        await Navigation.PopModalAsync();
    }

    /// <summary>
    /// Optional test hook: override default Show behavior (used by unit tests)
    /// </summary>
    public static Func<InputDialog, Page?, Task<string?>>? ShowAsyncHandler { get; set; }

    /// <summary>
    /// Show the dialog modally and await the result (string or null if cancelled)
    /// </summary>
    public Task<string?> ShowAsync(Page? parent = null)
    {
        // If a test handler is provided, use it (this allows unit tests to bypass Navigation)
        if (ShowAsyncHandler != null)
            return ShowAsyncHandler(this, parent);

        _tcs = new TaskCompletionSource<string?>();

        // Use provided parent or fallback to Application.Current.MainPage
        var nav = parent?.Navigation;
        if (nav == null)
        {
            // Try to find first window's page navigation as fallback
            var window = Application.Current?.Windows?.FirstOrDefault();
            nav = window?.Page?.Navigation;
        }

        if (nav == null)
        {
            _tcs.TrySetResult(null);
            return _tcs.Task;
        }

        // Push modal and return the task
        _ = nav.PushModalAsync(this, true);
        InputEntry.Focus();
        return _tcs.Task;
    }

    /// <summary>
    /// Keyboard for the input entry (exposed for calling pages to set specialized keyboards)
    /// </summary>
    public Keyboard EntryKeyboard
    {
        get => InputEntry?.Keyboard ?? Keyboard.Default;
        set
        {
            if (InputEntry != null)
                InputEntry.Keyboard = value;
        }
    }

    // Test helpers --------------------------------------------------------
    // These helpers allow unit tests to simulate acceptance/cancel without
    // depending on actual Navigation/Modal lifecycle.
    public Task<string?> SimulateAcceptAsync(string? text)
    {
        _tcs = new TaskCompletionSource<string?>();
        InputEntry.Text = text;
        _tcs.TrySetResult(text);
        return _tcs.Task;
    }

    public Task<string?> SimulateCancelAsync()
    {
        _tcs = new TaskCompletionSource<string?>();
        _tcs.TrySetResult(null);
        return _tcs.Task;
    }
}

using System.Windows.Input;

namespace SUS.EOS.NeoWallet.Pages.Components;

public partial class WalletKeyItemView : ContentView
{
    public static readonly BindableProperty PublicKeyProperty = BindableProperty.Create(
        nameof(PublicKey),
        typeof(string),
        typeof(WalletKeyItemView),
        string.Empty,
        propertyChanged: (b, o, n) => ((WalletKeyItemView)b).PublicKeyLabel.Text = (string)n!
    );

    public static readonly BindableProperty AccountCountTextProperty = BindableProperty.Create(
        nameof(AccountCountText),
        typeof(string),
        typeof(WalletKeyItemView),
        string.Empty,
        propertyChanged: (b, o, n) => ((WalletKeyItemView)b).AccountCountLabel.Text = (string)n!
    );

    public static readonly BindableProperty MenuCommandProperty = BindableProperty.Create(
        nameof(MenuCommand),
        typeof(ICommand),
        typeof(WalletKeyItemView),
        null
    );

    public string PublicKey
    {
        get => (string)GetValue(PublicKeyProperty);
        set => SetValue(PublicKeyProperty, value);
    }

    public string AccountCountText
    {
        get => (string)GetValue(AccountCountTextProperty);
        set => SetValue(AccountCountTextProperty, value);
    }

    public ICommand? MenuCommand
    {
        get => (ICommand?)GetValue(MenuCommandProperty);
        set => SetValue(MenuCommandProperty, value);
    }

    public WalletKeyItemView()
    {
        InitializeComponent();
        MenuButton.Clicked += (s, e) =>
        {
            if (MenuCommand != null && MenuCommand.CanExecute(BindingContext))
                MenuCommand.Execute(BindingContext);
        };
    }
}
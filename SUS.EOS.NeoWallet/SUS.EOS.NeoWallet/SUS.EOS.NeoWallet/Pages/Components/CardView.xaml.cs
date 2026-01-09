namespace SUS.EOS.NeoWallet.Pages.Components;

public partial class CardView : ContentView
{
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title),
        typeof(string),
        typeof(CardView),
        string.Empty,
        propertyChanged: (b, o, n) => ((CardView)b).TitleLabel.Text = (string)n!
    );

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public CardView()
    {
        InitializeComponent();
    }
}
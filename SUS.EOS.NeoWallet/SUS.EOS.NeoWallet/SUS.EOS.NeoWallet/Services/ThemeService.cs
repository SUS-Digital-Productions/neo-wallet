using System.ComponentModel;

namespace SUS.EOS.NeoWallet.Services;

/// <summary>
/// Service for managing application theme (Light/Dark mode)
/// </summary>
public sealed class ThemeService : INotifyPropertyChanged
{
    private AppTheme _currentTheme;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ThemeService()
    {
        _currentTheme = Application.Current?.RequestedTheme ?? AppTheme.Light;
    }

    /// <summary>
    /// Gets the current theme
    /// </summary>
    public AppTheme CurrentTheme
    {
        get => _currentTheme;
        private set
        {
            if (_currentTheme != value)
            {
                _currentTheme = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentTheme)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDarkMode)));
            }
        }
    }

    /// <summary>
    /// Gets whether dark mode is active
    /// </summary>
    public bool IsDarkMode => CurrentTheme == AppTheme.Dark;

    /// <summary>
    /// Sets the application theme
    /// </summary>
    public void SetTheme(AppTheme theme)
    {
        if (Application.Current != null)
        {
            Application.Current.UserAppTheme = theme;
            CurrentTheme = theme;
        }
    }

    /// <summary>
    /// Toggles between light and dark themes
    /// </summary>
    public void ToggleTheme()
    {
        var newTheme = CurrentTheme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light;
        SetTheme(newTheme);
    }

    /// <summary>
    /// Gets the theme display name
    /// </summary>
    public string GetThemeDisplayName()
    {
        return CurrentTheme switch
        {
            AppTheme.Light => "Light",
            AppTheme.Dark => "Dark",
            _ => "System"
        };
    }
}

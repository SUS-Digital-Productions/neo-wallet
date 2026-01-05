# Theme System Guide

## Color Palette

The Neo Wallet now uses a comprehensive color system designed for both light and dark modes:

### Primary Colors (Neo Green)
- **Light Mode**: `#00E599` (Neo signature green)
- **Dark Mode**: `#00E599` (same vibrant green)
- Lighter/Darker variants for hover states

### Theme-Aware Colors
All colors automatically adapt to light/dark mode using `{AppThemeBinding}`:

```xaml
<Label TextColor="{DynamicResource TextPrimary}" />
<Border BackgroundColor="{DynamicResource Card}" />
<Border Stroke="{DynamicResource Border}" />
```

### Available Color Resources

| Resource Key | Light Mode | Dark Mode | Usage |
|-------------|------------|-----------|--------|
| `PageBackgroundColor` | White (#FFFFFF) | Dark Gray (#121212) | Page backgrounds |
| `Primary` | Neo Green (#00E599) | Neo Green (#00E599) | Primary actions |
| `Secondary` | Blue (#4A90E2) | Light Blue (#5BA3FF) | Secondary actions |
| `Surface` | Light Gray (#F5F5F5) | Dark Gray (#1E1E1E) | Surfaces |
| `Card` | White (#FFFFFF) | Dark Gray (#2C2C2C) | Card backgrounds |
| `TextPrimary` | Dark (#212121) | White (#FFFFFF) | Primary text |
| `TextSecondary` | Gray (#757575) | Light Gray (#B3B3B3) | Secondary text |
| `Border` | Light Gray (#E0E0E0) | Gray (#3D3D3D) | Borders |

### Status Colors (Same in both modes)
- `Success`: #00E599 (Green)
- `Warning`: #FFA726 (Orange)
- `Error`: #EF5350 (Red)
- `Info`: #29B6F6 (Light Blue)

## Theme Switching

### Using ThemeService

The `ThemeService` is registered as a singleton and can be injected into any page:

```csharp
public class MyPage : ContentPage
{
    private readonly ThemeService _themeService;

    public MyPage(ThemeService themeService)
    {
        InitializeComponent();
        _themeService = themeService;
    }

    private void OnToggleTheme(object sender, EventArgs e)
    {
        _themeService.ToggleTheme();
    }
}
```

### ThemeService API

```csharp
// Get current theme
AppTheme theme = _themeService.CurrentTheme;

// Check if dark mode
bool isDark = _themeService.IsDarkMode;

// Set specific theme
_themeService.SetTheme(AppTheme.Dark);
_themeService.SetTheme(AppTheme.Light);

// Toggle between themes
_themeService.ToggleTheme();

// Get display name
string name = _themeService.GetThemeDisplayName(); // "Light" or "Dark"
```

### Settings Page

Users can toggle themes in the Settings page:
1. Open hamburger menu (☰)
2. Navigate to **Settings**
3. Toggle **Dark Mode** switch
4. Theme changes immediately across all pages

## Migration from Old Colors

### Before (Broken References)
```xaml
<Label TextColor="{DynamicResource Gray600}" />
<Frame BorderColor="{DynamicResource Gray200}" />
```

### After (Theme-Aware)
```xaml
<Label TextColor="{DynamicResource TextSecondary}" />
<Border Stroke="{DynamicResource Border}" />
```

## Best Practices

1. **Always use DynamicResource**: `{DynamicResource TextPrimary}` instead of `{StaticResource}`
2. **Use semantic names**: `TextPrimary` instead of `Gray900`
3. **Test both themes**: Always check your UI in both light and dark modes
4. **Avoid hardcoded colors**: Use color resources for consistency

## Updating Existing Pages

Replace old color references:
- `Gray600` → `TextSecondary`
- `Gray900` → `TextPrimary`
- `Gray200` → `Border`
- `Gray100` → `Surface`
- Background colors → `Card` or `PageBackgroundColor`

## Examples

### Card with Border
```xaml
<Border Stroke="{DynamicResource Border}" 
        StrokeThickness="1"
        StrokeShape="RoundRectangle 12"
        BackgroundColor="{DynamicResource Card}"
        Padding="20">
    <Label Text="Content" 
           TextColor="{DynamicResource TextPrimary}"/>
</Border>
```

### Button with Primary Color
```xaml
<Button Text="Action"
        BackgroundColor="{DynamicResource Primary}"
        TextColor="White"/>
```

### Text Hierarchy
```xaml
<VerticalStackLayout>
    <!-- Primary text -->
    <Label Text="Title" 
           TextColor="{DynamicResource TextPrimary}"
           FontSize="18"
           FontAttributes="Bold"/>
    
    <!-- Secondary text -->
    <Label Text="Subtitle" 
           TextColor="{DynamicResource TextSecondary}"
           FontSize="12"/>
</VerticalStackLayout>
```

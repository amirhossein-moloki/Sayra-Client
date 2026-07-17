# Color Management System (Theme System) Documentation

This document outlines the centralized, semantic Color Management System implemented for both **Sayra.UI** and **Sayra.Client.UI** applications.

All hard-coded colors have been extracted and moved into centralized theme configuration files, making it possible to change the entire application's look and feel from a single location and supporting future Light/Dark themes.

---

## Theme Structure
The theme system resides in both projects under the following structure:
```
Theme/
 └── Colors/
      ├── AppColors.xaml       # Base primitive color palette (Raw Hex scale)
      ├── DarkTheme.xaml       # Mapping semantic colors to Dark Theme palette
      ├── LightTheme.xaml      # Mapping semantic colors to Light Theme palette (Future use)
      └── ColorTokens.xaml     # Central resource dictionary exporting semantic "Theme.Brushes.*"
```

---

## Semantic Color Tokens & Brushes

### 1. Backgrounds & Surfaces

| Token Name (Brush / Color) | Purpose | Current Value (Dark Theme) | Used In Components |
| :--- | :--- | :--- | :--- |
| `Theme.Brushes.Background.Primary`<br>`Theme.Colors.Background.Primary` | Main application background color | `#101014` (UI)<br>`#0D0D12` (Client UI) | `LoginWindow`, `HomeWindow`, `AdminWindow`, `GameDetailWindow`, `MainWindow` |
| `Theme.Brushes.Background.Secondary`<br>`Theme.Colors.Background.Secondary` | Secondary panel/sidebar background color | `#1F1F23` (UI)<br>`#252525` (Client UI) | `AdPanel`, `NavigationContainer`, `LauncherView`, `BillingView` |
| `Theme.Brushes.Surface.Card`<br>`Theme.Colors.Surface.Card` | Background color for game cards and preview grids | `#1F1F23` (UI)<br>`#050507` (Client UI) | `GameCard`, `PriceCard`, `SystemInfoCard` |
| `Theme.Brushes.Surface.Panel`<br>`Theme.Colors.Surface.Panel` | Panel background for glassmorphic elements | `#181A20` (UI)<br>`#181820` (Client UI) | `HardwarePanel`, `GlassCard` Style, `LauncherView` (Inner grid) |
| `Theme.Brushes.Surface.Dialog`<br>`Theme.Colors.Surface.Dialog` | Overlay background for popup modals and logins | `#A60A0A0A` (UI)<br>`#2D2D2D` (Client UI) | `LoginWindow` Form Border, `WarningOverlay` Dialog |

### 2. Typography / Texts

| Token Name (Brush / Color) | Purpose | Current Value (Dark Theme) | Used In Components |
| :--- | :--- | :--- | :--- |
| `Theme.Brushes.Text.Primary`<br>`Theme.Colors.Text.Primary` | Default high-contrast text color | `#ffffff` | All TextBlocks, Headings, Button Labels |
| `Theme.Brushes.Text.Secondary`<br>`Theme.Colors.Text.Secondary` | Secondary descriptions and metadata text | `#9A9A9A` (UI)<br>`#A1A1AA` (Client UI) | `SecondaryText` Style, Game Details metadata lists |
| `Theme.Brushes.Text.Muted`<br>`Theme.Colors.Text.Muted` | Low-priority placeholder/watermark texts | `#9A9A9A` (UI)<br>`#888888` (Client UI) | Search placeholder input, "STARTED/ELAPSED" labels |
| `Theme.Brushes.Text.Disabled`<br>`Theme.Colors.Text.Disabled` | Disabled button or input label colors | `#55555A` | Disabled `PlayButton`, non-interactive states |

### 3. Borders & Dividers

| Token Name (Brush / Color) | Purpose | Current Value (Dark Theme) | Used In Components |
| :--- | :--- | :--- | :--- |
| `Theme.Brushes.Border.Default`<br>`Theme.Colors.Border.Default` | General border color for fields and inputs | `#252528` (UI)<br>`#333333` (Client UI) | `GlassInput` borders, form control containers |
| `Theme.Brushes.Border.Active`<br>`Theme.Colors.Border.Active` | Highlighted/active field border color | `#ffff3d` (UI)<br>`#F5FF00` (Client UI) | Active/Focused text fields, hovered buttons |
| `Theme.Brushes.Border.Cyan`<br>`Theme.Colors.Border.Cyan` | Cyan accent border for admin components | `#63E6FF` (UI)<br>`#00FFFF` (Client UI) | Admin layout separators, `GameCard` focus states |
| `Theme.Brushes.Border.Translucent`<br>`Theme.Colors.Border.Translucent` | High-translucency border for glassmorphic elements | `#1AFFFFFF` (UI)<br>`#24FFFFFF` (Client UI) | `GlassCard`, `GameCard` borders |

### 4. Interactive States & Brand Colors

| Token Name (Brush / Color) | Purpose | Current Value (Dark Theme) | Used In Components |
| :--- | :--- | :--- | :--- |
| `Theme.Brushes.Primary`<br>`Theme.Colors.Primary` | Brand primary yellow accent color | `#ffff3d` (UI)<br>`#F5FF00` (Client UI) | `PlayButton`, Timer glowing effects, Active indicators |
| `Theme.Brushes.PrimaryHover`<br>`Theme.Colors.PrimaryHover` | Primary hover state color | `#f4f46b` (UI)<br>`#FFFF3D` (Client UI) | Hovered button backgrounds, navigation transitions |
| `Theme.Brushes.Secondary`<br>`Theme.Colors.Secondary` | Secondary brand color (Cyan accent) | `#63E6FF` (UI)<br>`#00FFFF` (Client UI) | Scrollbars, Admin toolbar actions |

### 5. Semantic / Status Colors

| Token Name (Brush / Color) | Purpose | Current Value (Dark Theme) | Used In Components |
| :--- | :--- | :--- | :--- |
| `Theme.Brushes.Status.Success`<br>`Theme.Colors.Status.Success` | Success / Active state highlights | `#14BE78` (UI)<br>`#22C55E` (Client UI) | "PLAYING" state indicator, success notifications |
| `Theme.Brushes.Status.Warning`<br>`Theme.Colors.Status.Warning` | Warning/Alert indicators | `#E5A000` (UI)<br>`#FFA500` (Client UI) | Warning card borders, warning overlays |
| `Theme.Brushes.Status.Error`<br>`Theme.Colors.Status.Error` | Danger / Action termination highlights | `#F46B6B` (UI)<br>`#EF4444` (Client UI) | `ShutdownButton`, "LOCKED" game status, error logs |

---

## Support for Future Theme Customization
To customize colors or support high-contrast light theme rendering across the entire application:
1. Update values inside `LightTheme.xaml`.
2. Clear the merged resource dictionaries inside `App.xaml.cs` at runtime and load `LightTheme.xaml` inside `ColorTokens.xaml`.
The entire UI will instantly hot-reload with the new color mappings!

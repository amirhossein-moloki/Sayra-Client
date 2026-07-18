# WPF Style Audit Report

This report presents the complete forensic audit, discovery, and verification of all WPF Styles, Resources, Templates, and Theme Bindings across the `Sayra.UI` project.

---

## 1. Style & Resource Inventory

All resources across the project have been systematically cataloged:
- **Global Themes/Colors:** `Theme/Colors/AppColors.xaml`, `Theme/Colors/DarkTheme.xaml`, `Theme/Colors/ColorTokens.xaml`
- **Global Styles & Templates:** `Resources/Fonts.xaml`, `Resources/Styles.xaml`, `Resources/Templates.xaml`, `Resources/GameCardStyles.xaml`
- **Total Registered Resources:** 180 unique keys (including colors, brushes, fonts, styles, geometries, and converters).

---

## 2. Critical Issues

### CRITICAL #1: Frozen Freezable Animation on `svgc:SvgViewbox` Style
- **File:** `Sayra.UI/Views/Components/SessionHero.xaml`
- **Line:** 18-39 (Style: `HeroLogoStyle`)
- **Problem:** Style-defined frozen `Freezable` animation target path causing potential `MarkupException` / `XamlParseException` on view initialization.
- **Root Cause:**
  The Style `HeroLogoStyle` targets `svgc:SvgViewbox` and defines the `Effect` property via a `Setter`:
  ```xml
  <Setter Property="Effect">
      <Setter.Value>
          <DropShadowEffect x:Name="HeroLogoGlow" Color="{DynamicResource Theme.Colors.Primary}" BlurRadius="20" ... />
      </Setter.Value>
  </Setter>
  ```
  Since the style is declared in resources, the `DropShadowEffect` is sealed and frozen when the Style is loaded.
  However, the `Style.Triggers` block defines an `EventTrigger` for the `Loaded` event containing animations:
  ```xml
  <DoubleAnimation Storyboard.TargetProperty="(UIElement.Effect).(DropShadowEffect.Opacity)" From="0.3" To="0.85" ... />
  <DoubleAnimation Storyboard.TargetProperty="(UIElement.Effect).(DropShadowEffect.BlurRadius)" From="20" To="60" ... />
  ```
  WPF throws an exception at runtime because animating properties of a frozen `Freezable` is strictly illegal and causes a runtime crash.
- **WPF Failure Mode:** `System.InvalidOperationException: Cannot animate the 'Opacity' property on a frozen 'System.Windows.Media.Effects.DropShadowEffect'`.
- **Recommended Fix:**
  Completely remove the `HeroLogoStyle` resource definition. Instead, define the properties (`Opacity="0.15"` and `<svgc:SvgViewbox.Effect>`) and the `EventTrigger` storyboard directly on the `<svgc:SvgViewbox>` element itself inside `SessionHero.xaml`. This ensures the `DropShadowEffect` is instantiated locally on the element and is not frozen, enabling safe, crash-free animation of its properties.

---

## 3. Warning Issues

### WARNING #1: Duplicate Resource Key `WindowFadeInStoryboard`
- **Files & Lines:**
  - `Sayra.UI/Views/LoginWindow.xaml` (line 26)
  - `Sayra.UI/Views/HomeWindow.xaml` (line 21)
  - `Sayra.UI/Views/GameDetailWindow.xaml` (line 22)
- **Problem:** The storyboard key `WindowFadeInStoryboard` is duplicated across multiple windows.
- **Risk:** Very low / non-blocking, as each window has its own local resource dictionary scope.
- **Recommended Fix:** Re-declare the storyboard once inside a shared resource dictionary (e.g. `Styles.xaml`) and reference it globally.

### WARNING #2: Duplicate Resource Key `EndSessionButtonStyle`
- **Files & Lines:**
  - `Sayra.UI/Views/HomeWindow.xaml` (line 26)
  - `Sayra.UI/Views/GameDetailWindow.xaml` (line 61)
- **Problem:** The style key `EndSessionButtonStyle` is defined with the exact same template in both windows.
- **Risk:** Low, but increases code redundancy.
- **Recommended Fix:** Consolidate into `Resources/Styles.xaml`.

---

## 4. Safe Refactoring Suggestions

### Suggestion #1: Clean Up Unused Colors Resource
- **File:** `Sayra.UI/Resources/Colors.xaml`
- **Description:** This file contains legacy color and brush definitions.
- **Reason:** All active color and brush bindings now semantically resolve against the new theme directories under `Theme/Colors/`. `Resources/Colors.xaml` is completely unreferenced by `App.xaml` or any view, and can be safely deleted to reduce file clutter.

### Suggestion #2: Refactor TextBlock Styles
- **Description:** Reusable centralized typography definitions in `Resources/Styles.xaml`.
- **Reason:** Ensuring all standard UI captions use implicit styles or the consolidated `HeadingText` and `SecondaryText` styles will guarantee font-rendering uniformity.

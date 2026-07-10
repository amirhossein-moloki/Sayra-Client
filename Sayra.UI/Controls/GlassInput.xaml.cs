using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace Sayra.UI.Controls
{
    public partial class GlassInput : UserControl
    {
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(GlassInput),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register("Placeholder", typeof(string), typeof(GlassInput),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register("Icon", typeof(object), typeof(GlassInput),
                new PropertyMetadata(null));

        public static readonly DependencyProperty IsPasswordProperty =
            DependencyProperty.Register("IsPassword", typeof(bool), typeof(GlassInput),
                new PropertyMetadata(false, OnIsPasswordChanged));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public string Placeholder
        {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        public object Icon
        {
            get => GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public bool IsPassword
        {
            get => (bool)GetValue(IsPasswordProperty);
            set => SetValue(IsPasswordProperty, value);
        }

        private bool _isFocused;

        public GlassInput()
        {
            InitializeComponent();
            UpdatePlaceholderVisibility();
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GlassInput input)
            {
                input.UpdatePlaceholderVisibility();
                // Ensure PasswordBox matches if this is a password field (for programmatic changes)
                if (input.IsPassword && input.SecuredPasswordBox.Password != (string)e.NewValue)
                {
                    input.SecuredPasswordBox.Password = (string)e.NewValue ?? string.Empty;
                }
            }
        }

        private static void OnIsPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GlassInput input)
            {
                input.UpdatePlaceholderVisibility();
            }
        }

        private void SecuredPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (IsPassword)
            {
                Text = SecuredPasswordBox.Password;
            }
        }

        private void UpdatePlaceholderVisibility()
        {
            if (PlaceholderTextBlock == null) return;

            bool hasText = !string.IsNullOrEmpty(Text);
            PlaceholderTextBlock.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            _isFocused = true;
            AnimateToState();
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            _isFocused = false;
            AnimateToState();
        }

        private void TextBox_MouseEnter(object sender, RoutedEventArgs e)
        {
            AnimateToState();
        }

        private void TextBox_MouseLeave(object sender, RoutedEventArgs e)
        {
            AnimateToState();
        }

        private void AnimateToState()
        {
            Color targetBorderColor;
            double targetShadowOpacity;

            if (_isFocused)
            {
                // Full focus: Solid Yellow, glow high
                targetBorderColor = Color.FromRgb(245, 255, 0);
                targetShadowOpacity = 0.6;
            }
            else if (IsMouseOver || NormalTextBox.IsMouseOver || SecuredPasswordBox.IsMouseOver)
            {
                // Hover: Semi-transparent Yellow glow
                targetBorderColor = Color.FromArgb(102, 245, 255, 0);
                targetShadowOpacity = 0.3;
            }
            else
            {
                // Normal state
                targetBorderColor = Color.FromArgb(34, 245, 255, 0);
                targetShadowOpacity = 0.0;
            }

            // Perform animation using the instantiated, non-frozen SolidColorBrush & DropShadowEffect
            BorderBrushInstance.BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation(targetBorderColor, TimeSpan.FromSeconds(0.2)));

            ShadowEffectInstance.BeginAnimation(DropShadowEffect.OpacityProperty,
                new DoubleAnimation(targetShadowOpacity, TimeSpan.FromSeconds(0.2)));
        }
    }
}

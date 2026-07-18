using System.Windows;
using System.Windows.Controls;

namespace Sayra.UI.Controls
{
    public class GameInfoCard : ContentControl
    {
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(GameInfoCard), new PropertyMetadata(new CornerRadius(20)));

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        static GameInfoCard()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(GameInfoCard), new FrameworkPropertyMetadata(typeof(GameInfoCard)));
        }
    }
}

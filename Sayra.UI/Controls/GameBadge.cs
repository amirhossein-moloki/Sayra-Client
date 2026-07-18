using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Sayra.UI.Controls
{
    public class GameBadge : ContentControl
    {
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(GameBadge), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string), typeof(GameBadge), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty HasLabelProperty =
            DependencyProperty.Register(nameof(HasLabel), typeof(bool), typeof(GameBadge), new PropertyMetadata(false));

        public static readonly DependencyProperty HasDotProperty =
            DependencyProperty.Register(nameof(HasDot), typeof(bool), typeof(GameBadge), new PropertyMetadata(false));

        public static readonly DependencyProperty DotBrushProperty =
            DependencyProperty.Register(nameof(DotBrush), typeof(Brush), typeof(GameBadge), new PropertyMetadata(null));

        public static readonly DependencyProperty TextForegroundProperty =
            DependencyProperty.Register(nameof(TextForeground), typeof(Brush), typeof(GameBadge), new PropertyMetadata(null));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public bool HasLabel
        {
            get => (bool)GetValue(HasLabelProperty);
            set => SetValue(HasLabelProperty, value);
        }

        public bool HasDot
        {
            get => (bool)GetValue(HasDotProperty);
            set => SetValue(HasDotProperty, value);
        }

        public Brush DotBrush
        {
            get => (Brush)GetValue(DotBrushProperty);
            set => SetValue(DotBrushProperty, value);
        }

        public Brush TextForeground
        {
            get => (Brush)GetValue(TextForegroundProperty);
            set => SetValue(TextForegroundProperty, value);
        }

        static GameBadge()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(GameBadge), new FrameworkPropertyMetadata(typeof(GameBadge)));
        }
    }
}

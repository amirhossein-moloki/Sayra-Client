using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Sayra.UI.Controls
{
    public partial class GameBadge : UserControl
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

        public static readonly DependencyProperty BadgeStyleProperty =
            DependencyProperty.Register(nameof(BadgeStyle), typeof(Style), typeof(GameBadge), new PropertyMetadata(null));

        public static readonly DependencyProperty BadgeBackgroundProperty =
            DependencyProperty.Register(nameof(BadgeBackground), typeof(Brush), typeof(GameBadge), new PropertyMetadata(null));

        public static readonly DependencyProperty BadgeBorderBrushProperty =
            DependencyProperty.Register(nameof(BadgeBorderBrush), typeof(Brush), typeof(GameBadge), new PropertyMetadata(null));

        public static readonly DependencyProperty BadgeBorderThicknessProperty =
            DependencyProperty.Register(nameof(BadgeBorderThickness), typeof(Thickness), typeof(GameBadge), new PropertyMetadata(new Thickness(1)));

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

        public Style BadgeStyle
        {
            get => (Style)GetValue(BadgeStyleProperty);
            set => SetValue(BadgeStyleProperty, value);
        }

        public Brush BadgeBackground
        {
            get => (Brush)GetValue(BadgeBackgroundProperty);
            set => SetValue(BadgeBackgroundProperty, value);
        }

        public Brush BadgeBorderBrush
        {
            get => (Brush)GetValue(BadgeBorderBrushProperty);
            set => SetValue(BadgeBorderBrushProperty, value);
        }

        public Thickness BadgeBorderThickness
        {
            get => (Thickness)GetValue(BadgeBorderThicknessProperty);
            set => SetValue(BadgeBorderThicknessProperty, value);
        }

        public GameBadge()
        {
            InitializeComponent();
        }
    }
}

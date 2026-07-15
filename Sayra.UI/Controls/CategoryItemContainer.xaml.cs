using System.Windows;
using System.Windows.Controls;

namespace Sayra.UI.Controls
{
    /// <summary>
    /// Interaction logic for CategoryItemContainer.xaml
    /// </summary>
    public partial class CategoryItemContainer : UserControl
    {
        public CategoryItemContainer()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty IconContentProperty =
            DependencyProperty.Register(
                nameof(IconContent),
                typeof(object),
                typeof(CategoryItemContainer),
                new PropertyMetadata(null));

        public object IconContent
        {
            get => GetValue(IconContentProperty);
            set => SetValue(IconContentProperty, value);
        }

        public static readonly DependencyProperty TextContentProperty =
            DependencyProperty.Register(
                nameof(TextContent),
                typeof(object),
                typeof(CategoryItemContainer),
                new PropertyMetadata(null));

        public object TextContent
        {
            get => GetValue(TextContentProperty);
            set => SetValue(TextContentProperty, value);
        }

        public static readonly DependencyProperty BadgeContentProperty =
            DependencyProperty.Register(
                nameof(BadgeContent),
                typeof(object),
                typeof(CategoryItemContainer),
                new PropertyMetadata(null));

        public object BadgeContent
        {
            get => GetValue(BadgeContentProperty);
            set => SetValue(BadgeContentProperty, value);
        }
    }
}

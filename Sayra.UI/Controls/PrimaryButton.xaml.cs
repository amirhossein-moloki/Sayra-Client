using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Sayra.UI.Controls
{
    public partial class PrimaryButton : UserControl
    {
        public static readonly DependencyProperty ButtonContentProperty =
            DependencyProperty.Register("ButtonContent", typeof(object), typeof(PrimaryButton),
                new PropertyMetadata(null));

        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register("Command", typeof(ICommand), typeof(PrimaryButton),
                new PropertyMetadata(null));

        public object ButtonContent
        {
            get => GetValue(ButtonContentProperty);
            set => SetValue(ButtonContentProperty, value);
        }

        public ICommand Command
        {
            get => (ICommand)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public PrimaryButton()
        {
            InitializeComponent();
        }
    }
}

using System.Windows.Controls;

namespace Sayra.Client.UI.Views
{
    public partial class LoginView : UserControl
    {
        public LoginView()
        {
            InitializeComponent();
        }

        private void PinBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is ViewModels.LoginViewModel vm && sender is PasswordBox pb)
            {
                vm.Pin = pb.Password;
            }
        }
    }
}

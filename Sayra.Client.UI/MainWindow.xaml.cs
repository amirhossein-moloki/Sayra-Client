using System.Windows;
using Sayra.Client.UI.ViewModels;

namespace Sayra.Client.UI
{
    public partial class MainWindow : Window
    {
        public MainWindow(ShellViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}

using System.Windows;
using Sayra.UI.Notifications.ViewModels;

namespace Sayra.UI.Notifications.Views
{
    public partial class NotificationHistoryWindow : Window
    {
        public NotificationHistoryWindow(NotificationHistoryViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}

using System;
using System.Windows;
using Sayra.UI.ViewModels;

namespace Sayra.UI.Views
{
    public partial class AdminWindow : Window
    {
        public AdminWorkspaceViewModel ViewModel { get; }

        public AdminWindow()
        {
            ViewModel = new AdminWorkspaceViewModel();
            this.DataContext = ViewModel;

            InitializeComponent();
            this.Closed += AdminWindow_Closed;
        }

        private void AdminWindow_Closed(object? sender, EventArgs e)
        {
            bool isLoginOpen = false;
            foreach (Window win in Application.Current.Windows)
            {
                if (win is LoginWindow)
                {
                    isLoginOpen = true;
                    break;
                }
            }
            if (!isLoginOpen)
            {
                Application.Current.Shutdown();
            }
        }
    }
}

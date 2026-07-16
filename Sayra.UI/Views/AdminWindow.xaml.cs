using System;
using System.Windows;

namespace Sayra.UI.Views
{
    public partial class AdminWindow : Window
    {
        public AdminWindow()
        {
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

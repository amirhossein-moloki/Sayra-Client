using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Sayra.UI.Notifications.ViewModels;

namespace Sayra.UI.Notifications.Views
{
    public partial class NotificationOverlayWindow : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public NotificationOverlayWindow(NotificationOverlayViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            PositionWindow();
        }

        private void PositionWindow()
        {
            // Position window at bottom right corner of primary working area
            double workAreaWidth = SystemParameters.WorkArea.Width;
            double workAreaHeight = SystemParameters.WorkArea.Height;

            this.Left = workAreaWidth - this.Width - 10;
            this.Top = workAreaHeight - this.Height - 10;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var helper = new WindowInteropHelper(this);
            int exStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
            SetWindowLong(helper.Handle, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
        }
    }
}

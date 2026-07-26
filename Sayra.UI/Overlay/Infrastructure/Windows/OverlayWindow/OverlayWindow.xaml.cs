using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Sayra.Client.Shared.Runtime.Overlay.Domain.Models;

namespace Sayra.UI.Overlay.Infrastructure.Windows.OverlayWindow
{
    /// <summary>
    /// Interaction logic for OverlayWindow.xaml. Implements low-level click-through support
    /// and positions itself at the top-right corner of the primary screen.
    /// </summary>
    public partial class OverlayWindow : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", CharSet = CharSet.Auto)]
        private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", CharSet = CharSet.Auto)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLongPtr32(hWnd, nIndex);
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", CharSet = CharSet.Auto)]
        private static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", CharSet = CharSet.Auto)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            return IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : SetWindowLongPtr32(hWnd, nIndex, dwNewLong);
        }

        public OverlayWindow()
        {
            InitializeComponent();
            this.SourceInitialized += OnSourceInitialized;
            this.Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdatePosition();
            // Subscribe to resolution changes / display setting changes
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }

        private void OnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            this.Dispatcher.BeginInvoke(new Action(UpdatePosition));
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            // Apply mouse click-through (WS_EX_TRANSPARENT) and keyboard pass-through/non-activating (WS_EX_NOACTIVATE) styles
            var helper = new WindowInteropHelper(this);
            var hwnd = helper.Handle;

            IntPtr exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
            IntPtr newExStyle = new IntPtr(exStyle.ToInt64() | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE);
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, newExStyle);
        }

        private void UpdatePosition()
        {
            // Default: Top Right Corner
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double offset = 20; // safe margin padding
            this.Left = screenWidth - this.Width - offset;
            this.Top = offset;
        }

        public void UpdateData(OverlayData data)
        {
            // Format remaining duration dynamically
            TimeSpan rem = data.RemainingTime;
            TimeTextBlock.Text = $"{((int)rem.TotalHours):D2}:{rem.Minutes:D2}:{rem.Seconds:D2}";

            // Set the update status message
            StatusTextBlock.Text = data.Message;

            // Apply warning level style modifications
            switch (data.WarningLevel)
            {
                case 1: // Warning Level 1 (e.g. 10m threshold)
                    OverlayBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E6252528"));
                    OverlayBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFD700")); // Gold
                    TimeTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFD700"));
                    StateIcon.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFD700"));
                    break;

                case 2: // Warning Level 2 (e.g. 5m threshold)
                    OverlayBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E63A2510")); // Dark Orange background hint
                    OverlayBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFF8C00")); // Dark Orange
                    TimeTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFF8C00"));
                    StateIcon.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFF8C00"));
                    break;

                case 3: // Expired State
                    OverlayBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E6421010")); // Dark Red background hint
                    OverlayBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFF0000")); // Red
                    TimeTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFF0000"));
                    StateIcon.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFF0000"));
                    break;

                default: // Active State (Normal)
                    OverlayBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DD1F1F23"));
                    OverlayBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3E3E42"));
                    TimeTextBlock.Foreground = new SolidColorBrush(Colors.White);
                    StateIcon.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFF3D"));
                    break;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            base.OnClosed(e);
        }
    }
}

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Sayra.UI.Controls
{
    public partial class TopBar : UserControl
    {
        private readonly DispatcherTimer _timer;
        private readonly PersianCalendar _persianCalendar;

        public TopBar()
        {
            InitializeComponent();

            _persianCalendar = new PersianCalendar();

            // Initialize and start timer for updating real-time time and date
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();

            // Initial update
            UpdateDateTime();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            UpdateDateTime();
        }

        private void UpdateDateTime()
        {
            try
            {
                DateTime now = DateTime.Now;

                // Format time as HH:mm
                TimeText.Text = now.ToString("HH:mm");

                // Format date as Persian/Solar Hijri (yyyy/MM/dd)
                int year = _persianCalendar.GetYear(now);
                int month = _persianCalendar.GetMonth(now);
                int day = _persianCalendar.GetDayOfMonth(now);

                DateText.Text = $"{year:D4}/{month:D2}/{day:D2}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TopBar] Date conversion error: {ex.Message}");
            }
        }

        private void PowerButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "آیا مطمئن هستید که می‌خواهید خارج شوید؟",
                "سیستم سایرا",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No,
                MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign
            );

            if (result == MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
            }
        }
    }
}

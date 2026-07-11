using System;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Sayra.UI.ViewModels
{
    public partial class SessionHeroViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _sessionTime = "00:58:16";

        [ObservableProperty]
        private string _currentCost = "110,000 تومان";

        [ObservableProperty]
        private string _hourlyRate = "120,000 تومان";

        private DispatcherTimer? _timer;
        private TimeSpan _elapsedTime = new TimeSpan(0, 58, 16);

        public SessionHeroViewModel()
        {
            Log("Constructor START");
            InitializeTimer();
            Log("Constructor END");
        }

        private void InitializeTimer()
        {
            try
            {
                _timer = new DispatcherTimer();
                _timer.Interval = TimeSpan.FromSeconds(1);
                _timer.Tick += Timer_Tick;
                _timer.Start();
                Log("DispatcherTimer started successfully");
            }
            catch (Exception ex)
            {
                Log($"Failed to initialize timer: {ex}");
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            _elapsedTime = _elapsedTime.Add(TimeSpan.FromSeconds(1));
            SessionTime = _elapsedTime.ToString(@"hh\:mm\:ss");
        }

        private void Log(string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string formatted = $"[TRACE][SessionHeroViewModel][{timestamp}] {message}";
            System.Diagnostics.Debug.WriteLine(formatted);
            Console.WriteLine(formatted);
        }
    }
}

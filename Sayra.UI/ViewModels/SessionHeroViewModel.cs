using System;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using SayraClient.Services;
using SayraClient.Models;

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

        [ObservableProperty]
        private string _startTime = "12:00";

        private DispatcherTimer? _timer;
        private TimeSpan _elapsedTime = new TimeSpan(0, 58, 16);
        private readonly SessionManager? _sessionManager;

        // Parameterless constructor for XAML support and design-time fallback
        public SessionHeroViewModel() : this(App.ServiceProvider?.GetService<SessionManager>())
        {
        }

        // DI constructor
        public SessionHeroViewModel(SessionManager? sessionManager)
        {
            _sessionManager = sessionManager;

            Log("Constructor START");
            InitializeTimer();
            Log("Constructor END");
        }

        private void InitializeTimer()
        {
            try
            {
                // Set initial values if a real session exists
                UpdateSessionStats();

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
            UpdateSessionStats();
        }

        private void UpdateSessionStats()
        {
            try
            {
                SessionModel? activeSession = _sessionManager?.GetCurrentSession();

                if (activeSession != null && activeSession.Status == "ACTIVE")
                {
                    // Map core properties
                    var elapsed = TimeSpan.FromSeconds(activeSession.ElapsedSeconds);
                    SessionTime = elapsed.ToString(@"hh\:mm\:ss");

                    // Calculate remaining or formatted costs
                    CurrentCost = $"{activeSession.CurrentCost:N0} تومان";
                    HourlyRate = $"{activeSession.RatePerHour:N0} تومان";
                    StartTime = activeSession.StartTime.ToLocalTime().ToString(@"HH\:mm");
                }
                else
                {
                    // Fallback mock increments for visual richness when no session is active in DB/Core
                    _elapsedTime = _elapsedTime.Add(TimeSpan.FromSeconds(1));
                    SessionTime = _elapsedTime.ToString(@"hh\:mm\:ss");

                    var start = DateTime.Now.Subtract(_elapsedTime);
                    StartTime = start.ToString(@"HH\:mm");
                }
            }
            catch (Exception ex)
            {
                Log($"Error updating session stats: {ex}");
            }
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

using System;
using System.Diagnostics;
using System.Windows;
using Sayra.UI.ViewModels;
using Sayra.UI.Models;

namespace Sayra.UI.Views
{
    public partial class DashboardWindow : Window
    {
        private readonly AdminWorkspaceViewModel _viewModel;

        // Backward compatibility stub
        public FrameworkElement? GameLib => null;

        public DashboardWindow()
        {
            var swConstructor = Stopwatch.StartNew();
            GlobalExceptionHandler.CurrentOperation = "InitializeComponent started";
            GlobalExceptionHandler.LogTrace("DASHBOARD", "InitializeComponent started");

            var swInit = Stopwatch.StartNew();
            try
            {
                InitializeComponent();
                swInit.Stop();
                GlobalExceptionHandler.LogTrace("DASHBOARD", $"InitializeComponent completed in {swInit.ElapsedMilliseconds} ms");
                GlobalExceptionHandler.CurrentOperation = "InitializeComponent completed";
                GlobalExceptionHandler.LogTrace("DASHBOARD", "InitializeComponent completed");
            }
            catch (Exception ex)
            {
                swInit.Stop();
                GlobalExceptionHandler.LogTrace("DASHBOARD", $"InitializeComponent failed after {swInit.ElapsedMilliseconds} ms: {ex}");
                GlobalExceptionHandler.HandleException(ex, "Dashboard InitializeComponent");
                throw;
            }

            GlobalExceptionHandler.CurrentOperation = "ViewModel creation started";
            GlobalExceptionHandler.LogTrace("DASHBOARD", "ViewModel creation started");

            _viewModel = new AdminWorkspaceViewModel();
            this.DataContext = _viewModel;

            GlobalExceptionHandler.CurrentOperation = "ViewModel assigned";
            GlobalExceptionHandler.LogTrace("DASHBOARD", "ViewModel assigned");

            this.Loaded += DashboardWindow_Loaded;
            this.Closed += DashboardWindow_Closed;

            swConstructor.Stop();
            GlobalExceptionHandler.LogTrace("DASHBOARD", $"DashboardWindow constructor completed in {swConstructor.ElapsedMilliseconds} ms");
        }

        private void DashboardWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var swLoaded = Stopwatch.StartNew();
            GlobalExceptionHandler.LogTrace("DASHBOARD", "Loaded Event START");

            Sayra.UI.Services.NotificationService.Instance.ShowSuccess("کنسول مدیریت یکپارچه سایرا با موفقیت بارگذاری شد.");

            swLoaded.Stop();
            GlobalExceptionHandler.LogTrace("DASHBOARD", $"Loaded Event completed in {swLoaded.ElapsedMilliseconds} ms");
        }

        private async void Exit_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "آیا مطمئن هستید که می‌خواهید از کنسول مدیریت خارج شوید؟",
                "سیستم سایرا",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No,
                MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign
            );

            if (result == MessageBoxResult.Yes)
            {
                Sayra.UI.Services.NotificationService.Instance.ShowLoading("در حال خروج از کنسول...");
                await System.Threading.Tasks.Task.Delay(1000);
                Application.Current.Shutdown();
            }
        }

        // Backward compatibility stub required by other elements
        public void OpenGameDetail(GameItem game)
        {
            GlobalExceptionHandler.LogTrace("DASHBOARD", $"OpenGameDetail stub called for {game.Title}");
        }

        private void DashboardWindow_Closed(object? sender, EventArgs e)
        {
            GlobalExceptionHandler.LogTrace("DASHBOARD", "Closed Event - Shutting down application...");
            Application.Current.Shutdown();
        }
    }
}

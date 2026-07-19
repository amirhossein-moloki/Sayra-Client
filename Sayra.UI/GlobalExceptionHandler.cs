using System;
using System.IO;
using System.Windows;
using System.Threading.Tasks;
using System.Linq;
using Serilog;

namespace Sayra.UI
{
    public static class GlobalExceptionHandler
    {
        private static string _currentOperation = "Application Startup";

        public static string CurrentOperation
        {
            get => _currentOperation;
            set => _currentOperation = value;
        }

        public static void Register()
        {
            // 1. Dispatcher Unhandled Exception
            if (Application.Current != null)
            {
                Application.Current.DispatcherUnhandledException += (s, e) =>
                {
                    HandleException(e.Exception, "DispatcherUnhandledException");
                    e.Handled = true; // Prevent silent/default crash if possible
                };
            }

            // 2. Unobserved Task Exception
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                HandleException(e.Exception, "UnobservedTaskException");
                e.SetObserved(); // Prevent the exception from tearing down the process
            };

            // 3. AppDomain Unhandled Exception
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                HandleException(e.ExceptionObject as Exception ?? new Exception($"Unknown exception object: {e.ExceptionObject}"), "AppDomain.CurrentDomain.UnhandledException");
            };
        }

        public static void LogTrace(string category, string message)
        {
            // Unify WPF tracing logs with Serilog
            Log.Information("[{Category}] {Message}", category, message);
        }

        public static void HandleException(Exception? exception, string source)
        {
            if (exception == null) return;

            string currentWindow = "Unknown";

            if (Application.Current != null)
            {
                try
                {
                    if (Application.Current.Dispatcher.CheckAccess())
                    {
                        var activeWindow = Application.Current.Windows.Cast<Window>().FirstOrDefault(w => w.IsActive);
                        if (activeWindow != null)
                        {
                            currentWindow = activeWindow.GetType().Name;
                        }
                        else if (Application.Current.MainWindow != null)
                        {
                            currentWindow = Application.Current.MainWindow.GetType().Name;
                        }
                    }
                    else
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var activeWindow = Application.Current.Windows.Cast<Window>().FirstOrDefault(w => w.IsActive);
                            if (activeWindow != null)
                            {
                                currentWindow = activeWindow.GetType().Name;
                            }
                            else if (Application.Current.MainWindow != null)
                            {
                                currentWindow = Application.Current.MainWindow.GetType().Name;
                            }
                        });
                    }
                }
                catch
                {
                    // Ignore UI thread access issues during crash handling
                }
            }

            // Log exception details using Serilog
            Log.Fatal(exception, "CRITICAL ERROR: Source={Source}, Window={Window}, Operation={Operation}",
                source, currentWindow, CurrentOperation);

            // Show error dialog
            ShowErrorDialog(exception, source);
        }

        private static void ShowErrorDialog(Exception exception, string source)
        {
            try
            {
                if (Application.Current != null)
                {
                    Action showAction = () =>
                    {
                        string message = $"Error Source: {source}\n\n" +
                                         $"Operation: {CurrentOperation}\n\n" +
                                         $"Message: {exception.Message}\n\n" +
                                         $"Stack Trace:\n{exception.StackTrace}";

                        MessageBox.Show(message, "Dashboard loading failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    };

                    if (Application.Current.Dispatcher.CheckAccess())
                    {
                        showAction();
                    }
                    else
                    {
                        Application.Current.Dispatcher.Invoke(showAction);
                    }
                }
            }
            catch
            {
                // Fallback to console/debug in case UI dispatch fails
                Console.WriteLine($"[CRITICAL] Dialog show failed: {exception.Message}");
            }
        }
    }
}

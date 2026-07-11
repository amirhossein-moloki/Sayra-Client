using System;
using System.IO;
using System.Windows;
using System.Threading.Tasks;
using System.Linq;

namespace Sayra.UI
{
    public static class GlobalExceptionHandler
    {
        private static readonly object _logLock = new object();
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
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string logText = $"{timestamp}\n[{category}]\n{message}\n\n";

            try
            {
                string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }
                string logPath = Path.Combine(logDir, "application.log");

                lock (_logLock)
                {
                    File.AppendAllText(logPath, logText);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GlobalExceptionHandler] Logging failed: {ex.Message}");
            }
        }

        public static void HandleException(Exception? exception, string source)
        {
            if (exception == null) return;

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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

            string innerMsg = exception.InnerException != null ? exception.InnerException.Message : "None";
            string innerTrace = exception.InnerException != null ? exception.InnerException.StackTrace ?? "" : "";

            string details = $"Date/Time: {timestamp}\n" +
                             $"Exception Type: {exception.GetType().FullName}\n" +
                             $"Source: {source}\n" +
                             $"Message: {exception.Message}\n" +
                             $"StackTrace: {exception.StackTrace}\n" +
                             $"InnerException: {innerMsg}\n" +
                             $"InnerStackTrace: {innerTrace}\n" +
                             $"Current Window: {currentWindow}\n" +
                             $"Current Operation: {CurrentOperation}";

            LogTrace("EXCEPTION", details);

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

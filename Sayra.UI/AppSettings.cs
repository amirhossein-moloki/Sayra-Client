using System;
using System.IO;
using System.Text.Json;

namespace Sayra.UI
{
    public static class AppSettings
    {
        private static bool _debugDashboardMode = true; // Default to true for safe diagnostic mode

        public static bool DebugDashboardMode
        {
            get => _debugDashboardMode;
            set => _debugDashboardMode = value;
        }

        static AppSettings()
        {
            LoadSettings();
        }

        public static void LoadSettings()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (File.Exists(configPath))
                {
                    string jsonString = File.ReadAllText(configPath);
                    using (JsonDocument doc = JsonDocument.Parse(jsonString))
                    {
                        if (doc.RootElement.TryGetProperty("AppSettings", out JsonElement settingsElement))
                        {
                            if (settingsElement.TryGetProperty("DebugDashboardMode", out JsonElement debugModeElement))
                            {
                                _debugDashboardMode = debugModeElement.GetBoolean();
                            }
                        }
                        else if (doc.RootElement.TryGetProperty("DebugDashboardMode", out JsonElement debugModeElement))
                        {
                            _debugDashboardMode = debugModeElement.GetBoolean();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppSettings] Error loading appsettings.json: {ex.Message}");
            }
        }
    }
}

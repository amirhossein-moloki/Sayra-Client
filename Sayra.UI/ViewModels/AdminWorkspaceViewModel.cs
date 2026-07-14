using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sayra.UI.Models;
using Sayra.UI.Services;

namespace Sayra.UI.ViewModels
{
    public partial class AdminWorkspaceViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private AdminAppItem? _selectedApp;

        private readonly List<AdminAppItem> _allApps = new();

        public ObservableCollection<AdminAppItem> Apps { get; } = new();

        public ObservableCollection<string> Categories { get; } = new() { "RPG", "Action RPG", "Tactical Shooter", "Action-Adventure", "Racing", "Battle Royale", "MOBA", "Survival", "Tools", "Communication" };
        public ObservableCollection<string> LauncherTypes { get; } = new() { "Manual", "Steam", "Epic Games", "Sayra Launcher", "EA App", "Ubisoft Connect" };
        public ObservableCollection<string> AppTypes { get; } = new() { "Game", "Application" };

        public AdminWorkspaceViewModel()
        {
            Log("AdminWorkspaceViewModel constructor started");
            _ = InitializeMockDataAsync();
        }

        private async Task InitializeMockDataAsync()
        {
            try
            {
                var service = new MockGameService();
                var mockGames = await service.GetGamesAsync();

                _allApps.Clear();
                int idx = 1;
                foreach (var g in mockGames)
                {
                    _allApps.Add(new AdminAppItem
                    {
                        Id = g.Id,
                        Name = g.Title,
                        Type = "Game",
                        Category = g.Genre,
                        Status = g.Status,
                        IsEnabled = g.IsAvailable,
                        Description = g.Description,
                        ExecutablePath = GetMockExecutablePath(g.Title),
                        LauncherType = (g.Title == "Counter-Strike 2" || g.Title == "Apex Legends" || g.Title == "Dota 2" || g.Title == "PUBG: BATTLEGROUNDS") ? "Steam" : "Manual",
                        ValidationState = "Unverified",
                        Source = (g.Title == "Counter-Strike 2" || g.Title == "Elden Ring") ? "Steam" : "Manual",
                        Arguments = "-novid -high",
                        WorkingDirectory = "C:\\Games\\" + g.Title
                    });
                    idx++;
                }

                ApplyFilter();
                if (Apps.Count > 0)
                {
                    SelectedApp = Apps[0];
                }
                Log("AdminWorkspaceViewModel pre-populated with mock entries");
            }
            catch (Exception ex)
            {
                Log($"Initialization failed: {ex}");
            }
        }

        private string GetMockExecutablePath(string title)
        {
            string sanitized = title.Replace(" ", "").Replace(":", "").Replace("'", "");
            return $"C:\\Games\\{title}\\{sanitized}.exe";
        }

        partial void OnSearchTextChanged(string value)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string query = SearchText.Trim().ToLower();
            Apps.Clear();

            foreach (var item in _allApps)
            {
                bool matchesSearch = string.IsNullOrEmpty(query) ||
                                     item.Name.ToLower().Contains(query) ||
                                     item.Category.ToLower().Contains(query) ||
                                     item.Id.Contains(query) ||
                                     item.ExecutablePath.ToLower().Contains(query);

                if (matchesSearch)
                {
                    Apps.Add(item);
                }
            }
        }

        [RelayCommand]
        private async Task ScanGamesAsync()
        {
            NotificationService.Instance.ShowLoading("در حال اسکن بازی‌های نصب شده سیستم...");
            await Task.Delay(1200);

            var newGames = new List<AdminAppItem>
            {
                new AdminAppItem
                {
                    Id = (_allApps.Count + 1).ToString(),
                    Name = "Hades II",
                    Type = "Game",
                    Category = "RPG",
                    ExecutablePath = "C:\\Games\\Hades2\\Hades2.exe",
                    LauncherType = "Steam",
                    ValidationState = "Valid Path",
                    Source = "Steam",
                    Description = "Battle beyond the Underworld in this rogue-like dungeon crawler.",
                    IsEnabled = true
                },
                new AdminAppItem
                {
                    Id = (_allApps.Count + 2).ToString(),
                    Name = "Valorant",
                    Type = "Game",
                    Category = "Tactical Shooter",
                    ExecutablePath = "C:\\Riot Games\\VALORANT\\live\\VALORANT.exe",
                    LauncherType = "Manual",
                    ValidationState = "Valid Path",
                    Source = "Manual",
                    Description = "A 5v5 character-based tactical shooter.",
                    IsEnabled = true
                }
            };

            foreach (var item in newGames)
            {
                if (!_allApps.Any(a => a.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    _allApps.Add(item);
                }
            }

            ApplyFilter();
            NotificationService.Instance.ShowSuccess("اسکن بازی‌ها با موفقیت انجام شد! ۲ بازی جدید شناسایی شدند.");
        }

        [RelayCommand]
        private async Task ScanAppsAsync()
        {
            NotificationService.Instance.ShowLoading("در حال اسکن برنامه‌های کاربردی نصب شده...");
            await Task.Delay(1000);

            var newApps = new List<AdminAppItem>
            {
                new AdminAppItem
                {
                    Id = (_allApps.Count + 1).ToString(),
                    Name = "Discord",
                    Type = "Application",
                    Category = "Communication",
                    ExecutablePath = "C:\\Users\\Admin\\AppData\\Local\\Discord\\Update.exe",
                    LauncherType = "Manual",
                    ValidationState = "Valid Path",
                    Source = "Manual",
                    Description = "Voice, video, and text chat application.",
                    IsEnabled = true
                },
                new AdminAppItem
                {
                    Id = (_allApps.Count + 2).ToString(),
                    Name = "Google Chrome",
                    Type = "Application",
                    Category = "Tools",
                    ExecutablePath = "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe",
                    LauncherType = "Manual",
                    ValidationState = "Valid Path",
                    Source = "Manual",
                    Description = "Web browser developed by Google.",
                    IsEnabled = true
                }
            };

            foreach (var item in newApps)
            {
                if (!_allApps.Any(a => a.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    _allApps.Add(item);
                }
            }

            ApplyFilter();
            NotificationService.Instance.ShowSuccess("اسکن برنامه‌ها کامل شد! برنامه‌های کاربردی جدید ثبت شدند.");
        }

        [RelayCommand]
        private void RegisterSoftware()
        {
            var newItem = new AdminAppItem
            {
                Id = (_allApps.Count + 1).ToString(),
                Name = "برنامه جدید " + (_allApps.Count + 1),
                Type = "Game",
                Category = "General",
                ExecutablePath = "C:\\Path\\To\\Executable.exe",
                LauncherType = "Manual",
                ValidationState = "Unverified",
                Source = "Manual",
                Description = "توضیحات نرم افزار جدید وارد شود.",
                IsEnabled = true
            };

            _allApps.Add(newItem);
            ApplyFilter();
            SelectedApp = newItem;
            NotificationService.Instance.ShowSuccess("نرم‌افزار جدید با موفقیت ایجاد شد. لطفاً مشخصات آن را تکمیل نمایید.");
        }

        [RelayCommand]
        private void RemoveSelected()
        {
            if (SelectedApp == null)
            {
                NotificationService.Instance.ShowWarning("نرم‌افزاری برای حذف انتخاب نشده است.");
                return;
            }

            var toRemove = SelectedApp;
            _allApps.Remove(toRemove);
            ApplyFilter();

            if (Apps.Count > 0)
            {
                SelectedApp = Apps[0];
            }
            else
            {
                SelectedApp = null;
            }

            NotificationService.Instance.ShowSuccess($"نرم‌افزار '{toRemove.Name}' از لیست مدیریت حذف گردید.");
        }

        [RelayCommand]
        private async Task ValidatePathsAsync()
        {
            NotificationService.Instance.ShowLoading("در حال اعتبارسنجی مسیر فایل‌های اجرایی...");
            await Task.Delay(1000);

            foreach (var app in _allApps)
            {
                // Simple mockup check: if path contains "C:\" and ends in ".exe"
                if (!string.IsNullOrWhiteSpace(app.ExecutablePath) &&
                    app.ExecutablePath.Contains(":\\") &&
                    app.ExecutablePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    app.ValidationState = "Valid Path";
                }
                else
                {
                    app.ValidationState = "Path Not Found";
                }
            }

            NotificationService.Instance.ShowSuccess("اعتبارسنجی تمام مسیرها به پایان رسید.");
        }

        [RelayCommand]
        private async Task ValidateSelectedPathAsync()
        {
            if (SelectedApp == null) return;

            NotificationService.Instance.ShowLoading($"در حال بررسی مسیر {SelectedApp.Name}...");
            await Task.Delay(400);

            if (!string.IsNullOrWhiteSpace(SelectedApp.ExecutablePath) &&
                SelectedApp.ExecutablePath.Contains(":\\") &&
                SelectedApp.ExecutablePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                SelectedApp.ValidationState = "Valid Path";
                NotificationService.Instance.ShowSuccess("مسیر فایل اجرایی معتبر است.");
            }
            else
            {
                SelectedApp.ValidationState = "Path Not Found";
                NotificationService.Instance.ShowError("مسیر فایل اجرایی یافت نشد یا نامعتبر است!");
            }
        }

        [RelayCommand]
        private async Task SyncServerAsync()
        {
            NotificationService.Instance.ShowLoading("در حال همگام‌سازی اطلاعات با سرور مرکزی سایرا...");
            await Task.Delay(1500);
            NotificationService.Instance.ShowSuccess("همگام‌سازی با موفقیت انجام شد. تمامی تغییرات با سرور یکپارچه گردید.");
        }

        [RelayCommand]
        private async Task ImportMetadataAsync()
        {
            if (SelectedApp == null)
            {
                NotificationService.Instance.ShowWarning("لطفاً ابتدا نرم‌افزاری را از لیست انتخاب کنید.");
                return;
            }

            NotificationService.Instance.ShowLoading($"در حال دریافت فراداده (Metadata) برای {SelectedApp.Name}...");
            await Task.Delay(800);

            SelectedApp.Description = "تصحیح توضیحات بصورت خودکار: این محتوا مستقیماً از مخزن اطلاعات مرکزی سایرا همگام‌سازی شده است.";
            if (SelectedApp.Type == "Game")
            {
                SelectedApp.Category = "Action RPG";
            }
            SelectedApp.Source = "Sayra Server";

            NotificationService.Instance.ShowSuccess($"فراداده جدید برای '{SelectedApp.Name}' بارگذاری و اعمال شد.");
        }

        [RelayCommand]
        private async Task ExportConfigAsync()
        {
            NotificationService.Instance.ShowLoading("در حال خروجی گرفتن از پیکربندی‌ها...");
            await Task.Delay(800);
            NotificationService.Instance.ShowSuccess("پیکربندی با موفقیت به فایل 'sayra_apps_config.json' صادر شد.");
        }

        [RelayCommand]
        private void SaveApp()
        {
            if (SelectedApp == null) return;
            NotificationService.Instance.ShowSuccess($"تنظیمات نرم‌افزار '{SelectedApp.Name}' ذخیره شد.");
        }

        private void Log(string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string formatted = $"[TRACE][AdminWorkspaceViewModel][{timestamp}] {message}";
            System.Diagnostics.Debug.WriteLine(formatted);
            Console.WriteLine(formatted);
        }
    }
}

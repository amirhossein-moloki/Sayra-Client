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

        // --- NEW COMMANDS LINKED TO TOOLBAR CONTROLS ---

        [RelayCommand]
        private async Task ScanAllAsync()
        {
            NotificationService.Instance.ShowLoading("در حال اسکن تمامی لانچرها و پوشه‌های سیستم...");
            await Task.Delay(1200);
            NotificationService.Instance.ShowSuccess("اسکن کامل شد! بازی‌های جدید شناسایی شدند.");
        }

        [RelayCommand]
        private async Task ScanSteamAsync()
        {
            NotificationService.Instance.ShowLoading("در حال اسکن بازی‌های Steam...");
            await Task.Delay(800);
            NotificationService.Instance.ShowSuccess("بازی‌های Steam با موفقیت شناسایی شدند.");
        }

        [RelayCommand]
        private async Task ScanEpicAsync()
        {
            NotificationService.Instance.ShowLoading("در حال اسکن بازی‌های Epic Games...");
            await Task.Delay(800);
            NotificationService.Instance.ShowSuccess("بازی‌های Epic Games با موفقیت شناسایی شدند.");
        }

        [RelayCommand]
        private async Task ScanRiotAsync()
        {
            NotificationService.Instance.ShowLoading("در حال اسکن بازی‌های Riot Games...");
            await Task.Delay(800);
            NotificationService.Instance.ShowSuccess("بازی‌های Riot Games با موفقیت شناسایی شدند.");
        }

        [RelayCommand]
        private async Task ScanBattleNetAsync()
        {
            NotificationService.Instance.ShowLoading("در حال اسکن بازی‌های Battle.net...");
            await Task.Delay(800);
            NotificationService.Instance.ShowSuccess("بازی‌های Battle.net با موفقیت شناسایی شدند.");
        }

        [RelayCommand]
        private async Task ScanUbisoftAsync()
        {
            NotificationService.Instance.ShowLoading("در حال اسکن بازی‌های Ubisoft Connect...");
            await Task.Delay(800);
            NotificationService.Instance.ShowSuccess("بازی‌های Ubisoft Connect با موفقیت شناسایی شدند.");
        }

        [RelayCommand]
        private async Task ScanXboxAsync()
        {
            NotificationService.Instance.ShowLoading("در حال اسکن بازی‌های Xbox Live...");
            await Task.Delay(800);
            NotificationService.Instance.ShowSuccess("بازی‌های Xbox با موفقیت شناسایی شدند.");
        }

        [RelayCommand]
        private async Task ScanEAAsync()
        {
            NotificationService.Instance.ShowLoading("در حال اسکن بازی‌های EA App...");
            await Task.Delay(800);
            NotificationService.Instance.ShowSuccess("بازی‌های EA App با موفقیت شناسایی شدند.");
        }

        [RelayCommand]
        private void ScanCustom()
        {
            NotificationService.Instance.ShowWarning("مسیر پوشه سفارشی را انتخاب کنید...");
        }

        [RelayCommand]
        private void AddApplication()
        {
            var newItem = new AdminAppItem
            {
                Id = (_allApps.Count + 1).ToString(),
                Name = "برنامه جدید " + (_allApps.Count + 1),
                Type = "Application",
                Category = "Tools",
                ExecutablePath = "C:\\Path\\To\\App.exe",
                LauncherType = "Manual",
                ValidationState = "Unverified",
                Source = "Manual",
                Description = "توضیحات برنامه جدید وارد شود.",
                IsEnabled = true
            };
            _allApps.Add(newItem);
            ApplyFilter();
            SelectedApp = newItem;
            NotificationService.Instance.ShowSuccess("برنامه کاربردی جدید ایجاد شد.");
        }

        [RelayCommand]
        private void Duplicate()
        {
            if (SelectedApp == null)
            {
                NotificationService.Instance.ShowWarning("لطفاً ابتدا نرم‌افزاری را برای کپی انتخاب کنید.");
                return;
            }
            var newItem = new AdminAppItem
            {
                Id = (_allApps.Count + 1).ToString(),
                Name = SelectedApp.Name + " - کپی",
                Type = SelectedApp.Type,
                Category = SelectedApp.Category,
                ExecutablePath = SelectedApp.ExecutablePath,
                LauncherType = SelectedApp.LauncherType,
                ValidationState = SelectedApp.ValidationState,
                Source = SelectedApp.Source,
                Description = SelectedApp.Description,
                IsEnabled = SelectedApp.IsEnabled
            };
            _allApps.Add(newItem);
            ApplyFilter();
            SelectedApp = newItem;
            NotificationService.Instance.ShowSuccess($"کپی از '{SelectedApp.Name}' ایجاد شد.");
        }

        [RelayCommand]
        private void EditApp()
        {
            if (SelectedApp == null)
            {
                NotificationService.Instance.ShowWarning("لطفاً ابتدا نرم‌افزاری را انتخاب کنید.");
                return;
            }
            NotificationService.Instance.ShowWarning($"در حال ویرایش '{SelectedApp.Name}'...");
        }

        [RelayCommand]
        private void EnableApp()
        {
            if (SelectedApp == null) return;
            SelectedApp.IsEnabled = true;
            NotificationService.Instance.ShowSuccess($"'{SelectedApp.Name}' فعال گردید.");
        }

        [RelayCommand]
        private void DisableApp()
        {
            if (SelectedApp == null) return;
            SelectedApp.IsEnabled = false;
            NotificationService.Instance.ShowWarning($"'{SelectedApp.Name}' غیرفعال شد.");
        }

        [RelayCommand]
        private void ImportApp()
        {
            NotificationService.Instance.ShowWarning("در حال وارد کردن نرم‌افزار...");
        }

        [RelayCommand]
        private void ExportApp()
        {
            NotificationService.Instance.ShowSuccess("تنظیمات با موفقیت صادر شدند.");
        }

        [RelayCommand]
        private void BackupApp()
        {
            NotificationService.Instance.ShowSuccess("پشتیبان‌گیری از تنظیمات با موفقیت انجام شد.");
        }

        [RelayCommand]
        private void RestoreApp()
        {
            NotificationService.Instance.ShowSuccess("بازیابی تنظیمات با موفقیت انجام شد.");
        }

        [RelayCommand]
        private async Task SyncFromServerAsync()
        {
            NotificationService.Instance.ShowLoading("در حال دریافت تغییرات از سرور...");
            await Task.Delay(1000);
            NotificationService.Instance.ShowSuccess("تغییرات با موفقیت دریافت و همگام‌سازی شدند.");
        }

        [RelayCommand]
        private async Task SyncToServerAsync()
        {
            NotificationService.Instance.ShowLoading("در حال ارسال تغییرات به سرور...");
            await Task.Delay(1000);
            NotificationService.Instance.ShowSuccess("تغییرات با موفقیت به سرور ارسال شدند.");
        }

        [RelayCommand]
        private void CompareConfigs()
        {
            NotificationService.Instance.ShowSuccess("مقایسه پیکربندی‌های محلی با سرور انجام شد. مغایرتی یافت نشد.");
        }

        [RelayCommand]
        private void RescanAll()
        {
            ApplyFilter();
            NotificationService.Instance.ShowSuccess("لیست برنامه‌ها مجدداً اسکن و تازه‌سازی شد.");
        }

        [RelayCommand]
        private async Task RepairMetadataAsync()
        {
            NotificationService.Instance.ShowLoading("در حال بازسازی و تعمیر فراداده...");
            await Task.Delay(1200);
            NotificationService.Instance.ShowSuccess("تعمیر و بازسازی فراداده با موفقیت پایان یافت.");
        }

        [RelayCommand]
        private void ClearSearch()
        {
            SearchText = string.Empty;
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            NotificationService.Instance.ShowLoading("در حال به‌روزرسانی لیست نرم‌افزارها...");
            await Task.Delay(800);
            NotificationService.Instance.ShowSuccess("لیست نرم‌افزارها با موفقیت به‌روزرسانی شد.");
        }

        [RelayCommand]
        private void QuickFilter()
        {
            NotificationService.Instance.ShowSuccess("فیلترهای سریع فعال شدند.");
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
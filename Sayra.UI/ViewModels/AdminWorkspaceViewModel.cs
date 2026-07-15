using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Sayra.UI.Models;
using Sayra.UI.Services;

namespace Sayra.UI.ViewModels
{
    public partial class AdminWorkspaceViewModel : ObservableObject
    {
        // View modes
        public List<string> ViewModes { get; } = new() { "List View", "Compact View", "Grid View" };
        public List<int> PageSizes { get; } = new() { 25, 50, 100 };
        public List<string> DemoStates { get; } = new() { "Normal", "Loading", "Empty" };

        [ObservableProperty]
        private string _selectedViewMode = "List View";

        [ObservableProperty]
        private int _selectedPageSize = 50;

        [ObservableProperty]
        private string _selectedDemoState = "Normal";

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private int _currentPage = 1;

        [ObservableProperty]
        private int _totalItemsCount;

        [ObservableProperty]
        private int _selectedCount;

        [ObservableProperty]
        private string _showingText = "Showing 0-0 of 0";

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private double _loadingProgress;

        public ObservableCollection<AdminAppItem> AllItems { get; } = new();
        public ObservableCollection<AdminAppItem> VisibleItems { get; } = new();

        private readonly List<AdminAppItem> _cachedAllItems = new();

        public AdminWorkspaceViewModel()
        {
            GenerateMockData();
            ApplyFilterAndPagination();
        }

        partial void OnSearchTextChanged(string value)
        {
            CurrentPage = 1;
            ApplyFilterAndPagination();
        }

        partial void OnSelectedPageSizeChanged(int value)
        {
            CurrentPage = 1;
            ApplyFilterAndPagination();
        }

        partial void OnCurrentPageChanged(int value)
        {
            ApplyFilterAndPagination();
        }

        partial void OnSelectedDemoStateChanged(string value)
        {
            if (value == "Loading")
            {
                TriggerLoadingDemo();
            }
            else if (value == "Empty")
            {
                VisibleItems.Clear();
                ShowingText = "Showing 0-0 of 0";
                TotalItemsCount = 0;
            }
            else
            {
                ApplyFilterAndPagination();
            }
        }

        private async void TriggerLoadingDemo()
        {
            IsLoading = true;
            LoadingProgress = 0;
            VisibleItems.Clear();

            for (int i = 0; i <= 100; i += 5)
            {
                LoadingProgress = i;
                await Task.Delay(50);
            }

            IsLoading = false;
            if (SelectedDemoState == "Loading")
            {
                SelectedDemoState = "Normal";
            }
        }

        private void GenerateMockData()
        {
            // Clean up old subscriptions
            foreach (var item in _cachedAllItems)
            {
                item.PropertyChanged -= Item_PropertyChanged;
            }
            _cachedAllItems.Clear();

            // Distinct applications & games with crisp vector icon paths
            var templates = new List<(string Name, string Exec, string Cat, string Launcher, string Ver, string Pub, string Path, string Src, string Status, string Size, string Lic, string Svg)>
            {
                ("Counter-Strike 2", "cs2.exe", "FPS / Action", "Steam", "2.4.1", "Valve", @"C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike 2", "Digital Download", "Installed", "35.8 GB", "Free-to-Play", "M9 5H7a2 2 0 00-2 2v2M5 15v2a2 2 0 002 2h2m10-14h-2a2 2 0 00-2 2v2m4 6v2a2 2 0 00-2 2h-2M9 11H7v2h2v-2zm8 0h-2v2h2v-2z"),
                ("Cyberpunk 2077", "Cyberpunk2077.exe", "RPG", "Custom", "2.12", "CD PROJEKT RED", @"D:\Games\Cyberpunk 2077", "Offline Installer", "Installed", "70.2 GB", "Commercial", "M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5"),
                ("World of Warcraft", "Wow.exe", "MMORPG", "Battle.net", "10.2.7", "Blizzard Entertainment", @"C:\Program Files (x86)\Battle.net\World of Warcraft", "Launcher Sync", "Updating", "82.4 GB", "Commercial", "M12 2a10 10 0 100 20 10 10 0 000-20zm0 2c1.66 0 3 3.58 3 8s-1.34 8-3 8-3-3.58-3-8 1.34-8 3-8zm-8 8h16"),
                ("VALORANT", "VALORANT.exe", "Shooter / Tactical", "Riot", "8.09", "Riot Games", @"C:\Riot Games\VALORANT\live", "Riot Client", "Validation Required", "28.5 GB", "Free-to-Play", "M12 2C6.48 2 2 4.24 2 7v10c0 2.76 4.48 5 10 5s10-2.24 10-5V7c0-2.76-4.48-5-10-5zm0 18c-4.42 0-8-1.79-8-4V9.82c1.78.73 4.7 1.18 8 1.18s6.22-.45 8-1.18V14c0 2.21-3.58 4-8 4z"),
                ("Grand Theft Auto V", "GTA5.exe", "Action-Adventure", "Epic", "1.0.3", "Rockstar Games", @"E:\EpicGames\GTAV", "Digital Store", "Missing", "105.0 GB", "Commercial", "M9 5H7a2 2 0 00-2 2v2M5 15v2a2 2 0 002 2h2m10-14h-2a2 2 0 00-2 2v2m4 6v2a2 2 0 00-2 2h-2M9 11H7v2h2v-2zm8 0h-2v2h2v-2z"),
                ("EA SPORTS FC 24", "FC24.exe", "Sports", "EA", "1.4.2", "Electronic Arts", @"C:\Program Files\EA Games\EA SPORTS FC 24", "EA App", "Disabled", "48.0 GB", "Commercial", "M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5"),
                ("Assassin's Creed Valhalla", "ACValhalla.exe", "Action / RPG", "Ubisoft", "1.7.0", "Ubisoft", @"C:\Program Files (x86)\Ubisoft\Assassin's Creed Valhalla", "Ubisoft Connect", "Corrupted", "75.0 GB", "Commercial", "M12 2a10 10 0 100 20 10 10 0 000-20zm0 2c1.66 0 3 3.58 3 8s-1.34 8-3 8-3-3.58-3-8 1.34-8 3-8zm-8 8h16"),
                ("Forza Horizon 5", "ForzaHorizon5.exe", "Racing", "Xbox", "1.624", "Xbox Game Studios", @"C:\Program Files\WindowsApps\Microsoft.Forza_x64", "Windows Store", "Installed", "110.0 GB", "Commercial", "M12 2C6.48 2 2 4.24 2 7v10c0 2.76 4.48 5 10 5s10-2.24 10-5V7c0-2.76-4.48-5-10-5zm0 18c-4.42 0-8-1.79-8-4V9.82c1.78.73 4.7 1.18 8 1.18s6.22-.45 8-1.18V14c0 2.21-3.58 4-8 4z"),
                ("Visual Studio Code", "Code.exe", "Developer Tools", "Custom", "1.89.1", "Microsoft", @"C:\Users\Admin\AppData\Local\Programs\VSCode", "Local Installer", "Installed", "450 MB", "Free", "M9 5H7a2 2 0 00-2 2v2M5 15v2a2 2 0 002 2h2m10-14h-2a2 2 0 00-2 2v2m4 6v2a2 2 0 00-2 2h-2M9 11H7v2h2v-2zm8 0h-2v2h2v-2z"),
                ("Google Chrome", "chrome.exe", "Web Browser", "Custom", "125.0", "Google LLC", @"C:\Program Files\Google\Chrome\Application", "Web Download", "Installed", "1.2 GB", "Free", "M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5"),
                ("Discord", "Discord.exe", "Social / Chat", "Custom", "1.0.9001", "Discord Inc.", @"C:\Users\Admin\AppData\Local\Discord", "Web Download", "Installed", "220 MB", "Free", "M12 2a10 10 0 100 20 10 10 0 000-20zm0 2c1.66 0 3 3.58 3 8s-1.34 8-3 8-3-3.58-3-8 1.34-8 3-8zm-8 8h16"),
                ("Docker Desktop", "DockerDesktop.exe", "Virtualization", "Custom", "4.29.0", "Docker Inc.", @"C:\Program Files\Docker\Docker", "Enterprise Sync", "Installed", "4.8 GB", "Commercial", "M12 2C6.48 2 2 4.24 2 7v10c0 2.76 4.48 5 10 5s10-2.24 10-5V7c0-2.76-4.48-5-10-5zm0 18c-4.42 0-8-1.79-8-4V9.82c1.78.73 4.7 1.18 8 1.18s6.22-.45 8-1.18V14c0 2.21-3.58 4-8 4z"),
                ("VMware Workstation", "vmware.exe", "Hypervisor", "Custom", "17.5.1", "VMware, Inc.", @"C:\Program Files (x86)\VMware\Workstation", "Enterprise Disk", "Installed", "3.2 GB", "Commercial", "M9 5H7a2 2 0 00-2 2v2M5 15v2a2 2 0 002 2h2m10-14h-2a2 2 0 00-2 2v2m4 6v2a2 2 0 00-2 2h-2M9 11H7v2h2v-2zm8 0h-2v2h2v-2z"),
                ("SQL Server Management Studio", "Ssms.exe", "Database Console", "Custom", "19.3", "Microsoft", @"C:\Program Files (x86)\SSMS", "SQL Server Disk", "Installed", "2.8 GB", "Free", "M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5"),
                ("Fortnite", "FortniteClient.exe", "Battle Royale", "Epic", "29.40", "Epic Games", @"D:\EpicGames\Fortnite", "Epic Games Launcher", "Installed", "55.4 GB", "Free-to-Play", "M12 2a10 10 0 100 20 10 10 0 000-20zm0 2c1.66 0 3 3.58 3 8s-1.34 8-3 8-3-3.58-3-8 1.34-8 3-8zm-8 8h16"),
                ("Hearthstone", "Hearthstone.exe", "Card Game", "Battle.net", "28.6", "Blizzard Entertainment", @"C:\Program Files (x86)\Hearthstone", "Launcher Sync", "Installed", "12.0 GB", "Free-to-Play", "M12 2C6.48 2 2 4.24 2 7v10c0 2.76 4.48 5 10 5s10-2.24 10-5V7c0-2.76-4.48-5-10-5zm0 18c-4.42 0-8-1.79-8-4V9.82c1.78.73 4.7 1.18 8 1.18s6.22-.45 8-1.18V14c0 2.21-3.58 4-8 4z"),
                ("League of Legends", "LeagueClient.exe", "MOBA", "Riot", "14.10", "Riot Games", @"C:\Riot Games\League of Legends", "Riot Client", "Installed", "16.5 GB", "Free-to-Play", "M9 5H7a2 2 0 00-2 2v2M5 15v2a2 2 0 002 2h2m10-14h-2a2 2 0 00-2 2v2m4 6v2a2 2 0 00-2 2h-2M9 11H7v2h2v-2zm8 0h-2v2h2v-2z"),
                ("Apex Legends", "r5apex.exe", "Battle Royale", "EA", "1.24.4", "Respawn Entertainment", @"C:\Program Files\EA Games\Apex Legends", "EA App", "Installed", "62.0 GB", "Free-to-Play", "M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5"),
                ("Minecraft Launcher", "MinecraftLauncher.exe", "Sandbox", "Xbox", "2.1.20", "Mojang Studios", @"C:\XboxGames\Minecraft", "Microsoft Store", "Installed", "500 MB", "Commercial", "M12 2a10 10 0 100 20 10 10 0 000-20zm0 2c1.66 0 3 3.58 3 8s-1.34 8-3 8-3-3.58-3-8 1.34-8 3-8zm-8 8h16")
            };

            // Loop and duplicate templates with distinct indexes to reach exactly 128 elements
            int totalRequired = 128;
            for (int i = 0; i < totalRequired; i++)
            {
                var temp = templates[i % templates.Count];
                int index = (i / templates.Count) + 1;

                string name = temp.Name;
                string exec = temp.Exec;
                string path = temp.Path;

                if (index > 1)
                {
                    name = $"{temp.Name} ({index})";
                    exec = $"{System.IO.Path.GetFileNameWithoutExtension(temp.Exec)}_{index}{System.IO.Path.GetExtension(temp.Exec)}";
                    path = $"{temp.Path} {index}";
                }

                // Varying status slightly
                string status = temp.Status;
                if (i % 15 == 0) status = "Corrupted";
                else if (i % 22 == 0) status = "Missing";
                else if (i % 29 == 0) status = "Updating";
                else if (i % 35 == 0) status = "Validation Required";
                else if (i % 41 == 0) status = "Disabled";

                var item = new AdminAppItem
                {
                    Id = $"APP-{1000 + i}",
                    Name = name,
                    Executable = exec,
                    Category = temp.Cat,
                    Launcher = temp.Launcher,
                    Version = temp.Ver,
                    Publisher = temp.Pub,
                    InstallationPath = path,
                    InstallationSource = temp.Src,
                    Status = status,
                    LastUpdated = DateTime.Now.AddDays(-i % 30).AddHours(-i % 24).ToString("yyyy-MM-dd HH:mm"),
                    ModifiedBy = (i % 3 == 0) ? "Administrator" : ((i % 3 == 1) ? "System Sync" : "Deploy Service"),
                    Size = temp.Size,
                    License = temp.Lic,
                    IconGeometry = temp.Svg
                };

                item.PropertyChanged += Item_PropertyChanged;
                _cachedAllItems.Add(item);
            }
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AdminAppItem.IsChecked))
            {
                UpdateSelectedCount();
            }
        }

        private void UpdateSelectedCount()
        {
            SelectedCount = _cachedAllItems.Count(x => x.IsChecked);
        }

        public void ApplyFilterAndPagination()
        {
            if (SelectedDemoState != "Normal") return;

            // Step 1: Filter
            IEnumerable<AdminAppItem> filtered = _cachedAllItems;
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string searchLower = SearchText.ToLower();
                filtered = filtered.Where(x =>
                    x.Name.ToLower().Contains(searchLower) ||
                    x.Executable.ToLower().Contains(searchLower) ||
                    x.Publisher.ToLower().Contains(searchLower) ||
                    x.Launcher.ToLower().Contains(searchLower) ||
                    x.Category.ToLower().Contains(searchLower) ||
                    x.InstallationPath.ToLower().Contains(searchLower)
                );
            }

            var filteredList = filtered.ToList();
            TotalItemsCount = filteredList.Count;

            // Keep track of total pages
            int totalPages = (int)Math.Ceiling((double)TotalItemsCount / SelectedPageSize);
            if (totalPages == 0) totalPages = 1;
            if (CurrentPage > totalPages) CurrentPage = totalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            // Step 2: Paginate
            var paged = filteredList
                .Skip((CurrentPage - 1) * SelectedPageSize)
                .Take(SelectedPageSize)
                .ToList();

            // Populate view collection
            VisibleItems.Clear();
            foreach (var item in paged)
            {
                VisibleItems.Add(item);
            }

            // Update footer text
            int startIdx = TotalItemsCount == 0 ? 0 : (CurrentPage - 1) * SelectedPageSize + 1;
            int endIdx = Math.Min(CurrentPage * SelectedPageSize, TotalItemsCount);
            ShowingText = $"Showing {startIdx}–{endIdx} of {TotalItemsCount}";
        }

        // Action Commands
        [RelayCommand]
        private void Launch(AdminAppItem item)
        {
            if (item == null) return;
            NotificationService.Instance.ShowSuccess($"در حال اجرای برنامه: {item.Name}");
        }

        [RelayCommand]
        private void Stop(AdminAppItem item)
        {
            if (item == null) return;
            NotificationService.Instance.ShowWarning($"پروسه برنامه متوقف شد: {item.Name}");
        }

        [RelayCommand]
        private void Restart(AdminAppItem item)
        {
            if (item == null) return;
            NotificationService.Instance.ShowLoading($"در حال راه‌اندازی مجدد {item.Name}...");
            Task.Delay(1000).ContinueWith(_ =>
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    NotificationService.Instance.ShowSuccess($"برنامه {item.Name} با موفقیت ری‌استارت شد.");
                });
            });
        }

        [RelayCommand]
        private void Edit(AdminAppItem item)
        {
            if (item == null) return;
            NotificationService.Instance.ShowLoading($"در حال ویرایش پیکربندی: {item.Name}");
        }

        [RelayCommand]
        private void OpenFolder(AdminAppItem item)
        {
            if (item == null) return;
            NotificationService.Instance.ShowSuccess($"پوشه برنامه باز شد: {item.InstallationPath}");
        }

        [RelayCommand]
        private void CopyPath(AdminAppItem item)
        {
            if (item == null) return;
            try
            {
                System.Windows.Clipboard.SetText(item.InstallationPath);
                NotificationService.Instance.ShowSuccess("مسیر نصب برنامه در حافظه کپی شد.");
            }
            catch
            {
                NotificationService.Instance.ShowError("خطا در دسترسی به Clipboard سیستم.");
            }
        }

        [RelayCommand]
        private void Validate(AdminAppItem item)
        {
            if (item == null) return;
            NotificationService.Instance.ShowLoading($"در حال اعتبارسنجی فایل‌ها: {item.Name}");
            Task.Delay(1500).ContinueWith(_ =>
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    item.Status = "Installed";
                    NotificationService.Instance.ShowSuccess($"اعتبارسنجی تکمیل شد. فایل‌های {item.Name} سالم هستند.");
                });
            });
        }

        [RelayCommand]
        private void ScanMetadata(AdminAppItem item)
        {
            if (item == null) return;
            NotificationService.Instance.ShowLoading($"در حال اسکن متادیتا: {item.Name}");
        }

        [RelayCommand]
        private void Export(AdminAppItem item)
        {
            if (item == null) return;
            NotificationService.Instance.ShowSuccess($"اطلاعات برنامه صادر شد: {item.Id}.json");
        }

        [RelayCommand]
        private void Delete(AdminAppItem item)
        {
            if (item == null) return;
            NotificationService.Instance.ShowError($"برنامه {item.Name} از لیست مدیریت حذف شد.");
            _cachedAllItems.Remove(item);
            item.PropertyChanged -= Item_PropertyChanged;
            UpdateSelectedCount();
            ApplyFilterAndPagination();
        }

        [RelayCommand]
        private void ScanComputer()
        {
            NotificationService.Instance.ShowLoading("در حال اسکن سیستم برای بازی‌ها و برنامه‌ها...");
            SelectedDemoState = "Loading";
        }

        [RelayCommand]
        private void Refresh()
        {
            SearchText = string.Empty;
            CurrentPage = 1;
            GenerateMockData();
            ApplyFilterAndPagination();
            NotificationService.Instance.ShowSuccess("لیست برنامه‌ها مجدداً بارگذاری شد.");
        }

        [RelayCommand]
        private void PrevPage()
        {
            if (CurrentPage > 1) CurrentPage--;
        }

        [RelayCommand]
        private void NextPage()
        {
            int totalPages = (int)Math.Ceiling((double)TotalItemsCount / SelectedPageSize);
            if (CurrentPage < totalPages) CurrentPage++;
        }

        [RelayCommand]
        private void SetPage(object pageNum)
        {
            if (pageNum is int page)
            {
                CurrentPage = page;
            }
        }
    }
}

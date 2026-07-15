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

        // Categories
        public ObservableCollection<AdminCategoryItem> Categories { get; } = new();

        [ObservableProperty]
        private AdminCategoryItem? _selectedCategory;

        public ObservableCollection<AdminAppItem> AllItems { get; } = new();
        public ObservableCollection<AdminAppItem> VisibleItems { get; } = new();

        private readonly List<AdminAppItem> _cachedAllItems = new();

        public AdminWorkspaceViewModel()
        {
            GenerateMockData();
            InitializeCategories();
            RecalculateCategoryCounts();
            ApplyFilterAndPagination();
        }

        private void InitializeCategories()
        {
            Categories.Clear();
            Categories.Add(new AdminCategoryItem { Name = "All", IconGeometry = "M3 12h18M3 6h18M3 18h18" });
            Categories.Add(new AdminCategoryItem { Name = "Installed Games", IconGeometry = "M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5" });
            Categories.Add(new AdminCategoryItem { Name = "Applications", IconGeometry = "M9 3H5a2 2 0 00-2 2v4a2 2 0 002 2h4a2 2 0 002-2V5a2 2 0 00-2-2zm10 0h-4a2 2 0 00-2 2v4a2 2 0 002 2h4a2 2 0 002-2V5a2 2 0 00-2-2zM9 13H5a2 2 0 00-2 2v4a2 2 0 002 2h4a2 2 0 002-2v-4a2 2 0 00-2-2zm10 0h-4a2 2 0 00-2 2v4a2 2 0 002 2h4a2 2 0 002-2v-4a2 2 0 00-2-2z" });
            Categories.Add(new AdminCategoryItem { Name = "Steam", IconGeometry = "M12 2a10 10 0 1010 10A10 10 0 0012 2zm-1.74 15.35a3.48 3.48 0 01-3.13-1.66l2.9-1.28c.18.23.47.34.78.34a1.13 1.13 0 001-.63L14.7 13a2.3 2.3 0 011.63-1.23L16 11.45a3.36 3.36 0 00-3.35 3.35c0 .35.05.69.15 1z" });
            Categories.Add(new AdminCategoryItem { Name = "Epic Games", IconGeometry = "M12 2L2 5v12l10 3 10-3V5zm0 3a1.5 1.5 0 110 3 1.5 1.5 0 010-3zm5 9H7v-1h10z" });
            Categories.Add(new AdminCategoryItem { Name = "Battle.net", IconGeometry = "M12 2C6.5 2 2 6.5 2 12s4.5 10 10 10 10-4.5 10-10S17.5 2 12 2zm2 14H10v-2h4zm1-4H9v-2h6z" });
            Categories.Add(new AdminCategoryItem { Name = "Riot Games", IconGeometry = "M2 5l2-1h14l2 1v12l-2 1H4l-2-1zm14 3H6v2h10zm0 4H6v2h10z" });
            Categories.Add(new AdminCategoryItem { Name = "Ubisoft Connect", IconGeometry = "M12 2a10 10 0 1010 10A10 10 0 0012 2zm3.5 11.5a3.5 3.5 0 110-3 3.5 3.5 0 010 3z" });
            Categories.Add(new AdminCategoryItem { Name = "EA App", IconGeometry = "M12 2a10 10 0 1010 10A10 10 0 0012 2zM8 14h8v-1H8zm0-3h8V10H8z" });
            Categories.Add(new AdminCategoryItem { Name = "Xbox", IconGeometry = "M12 2a10 10 0 1010 10A10 10 0 0012 2zM6 12c2.5-4 5.5-4 8 0m-8 2c2-3 4-3 6 0" });
            Categories.Add(new AdminCategoryItem { Name = "Custom", IconGeometry = "M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z" });
            Categories.Add(new AdminCategoryItem { Name = "Recently Added", IconGeometry = "M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" });
            Categories.Add(new AdminCategoryItem { Name = "Favorites", IconGeometry = "M11.48 3.499c.3-.921 1.603-.921 1.902 0l1.07 3.292a1 1 0 00.95.69h3.462c.969 0 1.371 1.24.588 1.81l-2.8 2.034a1 1 0 00-.364 1.118l1.07 3.292c.3.921-.755 1.688-1.54 1.118l-2.8-2.034a1 1 0 00-1.175 0l-2.8 2.034c-.784.57-1.838-.197-1.539-1.118l1.07-3.292a1 1 0 00-.364-1.118L2.98 8.72c-.783-.57-.38-1.81.588-1.81h3.461a1 1 0 00.951-.69l1.07-3.292z" });
            Categories.Add(new AdminCategoryItem { Name = "Hidden", IconGeometry = "M3.98 8.223A10.477 10.477 0 001.934 12C3.226 16.338 7.244 19.5 12 19.5c.993 0 1.953-.138 2.863-.395M6.228 6.228A10.451 10.451 0 0112 4.5c4.756 0 8.773 3.162 10.065 7.498a10.523 10.523 0 01-4.293 5.774M6.228 6.228L3 3m3.228 3.228l3.65 3.65m7.894 7.894L21 21m-3.228-3.228l-3.65-3.65m0 0a3 3 0 10-4.243-4.243m4.242 4.242L9.88 9.88" });
            Categories.Add(new AdminCategoryItem { Name = "Disabled", IconGeometry = "M18.364 18.364A9 9 0 005.636 5.636m12.728 12.728A9 9 0 015.636 5.636m12.728 12.728L5.636 5.636" });
            Categories.Add(new AdminCategoryItem { Name = "Broken", IconGeometry = "M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" });
            Categories.Add(new AdminCategoryItem { Name = "Needs Validation", IconGeometry = "M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" });

            // Set default selected category to All
            SelectedCategory = Categories[0];
        }

        private bool IsApplication(AdminAppItem item)
        {
            return item.Category == "Developer Tools" ||
                   item.Category == "Web Browser" ||
                   item.Category == "Social / Chat" ||
                   item.Category == "Virtualization" ||
                   item.Category == "Hypervisor" ||
                   item.Category == "Database Console";
        }

        private bool MatchesCategory(AdminAppItem item, int index, string categoryName)
        {
            return categoryName switch
            {
                "All" => true,
                "Installed Games" => !IsApplication(item),
                "Applications" => IsApplication(item),
                "Steam" => item.Launcher == "Steam",
                "Epic Games" => item.Launcher == "Epic",
                "Battle.net" => item.Launcher == "Battle.net",
                "Riot Games" => item.Launcher == "Riot",
                "Ubisoft Connect" => item.Launcher == "Ubisoft",
                "EA App" => item.Launcher == "EA",
                "Xbox" => item.Launcher == "Xbox",
                "Custom" => item.Launcher == "Custom",
                "Recently Added" => (index % 3 == 0),
                "Favorites" => (index % 7 == 0),
                "Hidden" => (index % 19 == 0),
                "Disabled" => item.Status == "Disabled",
                "Broken" => item.Status == "Corrupted" || item.Status == "Missing",
                "Needs Validation" => item.Status == "Validation Required",
                _ => true
            };
        }

        private void RecalculateCategoryCounts()
        {
            for (int i = 0; i < Categories.Count; i++)
            {
                var category = Categories[i];
                int count = 0;
                for (int itemIdx = 0; itemIdx < _cachedAllItems.Count; itemIdx++)
                {
                    if (MatchesCategory(_cachedAllItems[itemIdx], itemIdx, category.Name))
                    {
                        count++;
                    }
                }
                category.Count = count;
            }
        }

        partial void OnSelectedCategoryChanged(AdminCategoryItem? value)
        {
            CurrentPage = 1;
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
            if (e.PropertyName == nameof(AdminAppItem.Status))
            {
                RecalculateCategoryCounts();
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

            // Category Filter
            if (SelectedCategory != null && SelectedCategory.Name != "All")
            {
                filtered = filtered.Where((x, idx) => MatchesCategory(x, idx, SelectedCategory.Name));
            }

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

        // Left Nav Bottom Actions Commands
        [RelayCommand]
        private void ManageCategories()
        {
            NotificationService.Instance.ShowLoading("در حال بارگذاری بخش مدیریت دسته‌بندی‌ها...");
        }

        [RelayCommand]
        private void RefreshCategories()
        {
            RecalculateCategoryCounts();
            NotificationService.Instance.ShowSuccess("تعداد دسته‌بندی‌ها با موفقیت به‌روزرسانی شد.");
        }

        [RelayCommand]
        private void CollapseAll()
        {
            NotificationService.Instance.ShowSuccess("تمامی دسته‌بندی‌ها جمع شدند.");
        }

        [RelayCommand]
        private void Settings()
        {
            NotificationService.Instance.ShowLoading("در حال بارگذاری بخش تنظیمات پنل مدیریت...");
        }

        [RelayCommand]
        private void Refresh()
        {
            SearchText = string.Empty;
            CurrentPage = 1;
            GenerateMockData();
            RecalculateCategoryCounts();
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

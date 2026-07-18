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

        // Extensive predefined lists of Categories and Game Types (Genres) so admins do not need to add manually
        public List<string> ExtensiveCategories { get; } = new()
        {
            "Games",
            "Applications",
            "Developer Tools",
            "Web Browsers",
            "Social & Chat",
            "Virtualization & Hypervisors",
            "Databases",
            "Office & Productivity",
            "Media Players",
            "Design & Graphics",
            "Utilities"
        };

        public List<string> ExtensiveGameTypes { get; } = new()
        {
            "Action",
            "Adventure",
            "RPG",
            "Strategy",
            "Simulation",
            "Shooter",
            "Survival",
            "Racing",
            "Sports",
            "Indie",
            "Fighting",
            "Platformer",
            "Puzzle",
            "MMORPG",
            "MOBA",
            "Horror",
            "Battle Royale",
            "Arcade",
            "Family",
            "Sandbox",
            "Virtual Reality",
            "Co-op",
            "Tactical"
        };

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

            // Standard Categories (Using ultra-premium Lucide outline icons)
            Categories.Add(new AdminCategoryItem { Name = "All", IconGeometry = "M 4 3 h 5 a 1 1 0 0 1 1 1 v 5 a 1 1 0 0 1 -1 1 h -5 a 1 1 0 0 1 -1 -1 v -5 a 1 1 0 0 1 1 -1 z M 15 3 h 5 a 1 1 0 0 1 1 1 v 5 a 1 1 0 0 1 -1 1 h -5 a 1 1 0 0 1 -1 -1 v -5 a 1 1 0 0 1 1 -1 z M 15 14 h 5 a 1 1 0 0 1 1 1 v 5 a 1 1 0 0 1 -1 1 h -5 a 1 1 0 0 1 -1 -1 v -5 a 1 1 0 0 1 1 -1 z M 4 14 h 5 a 1 1 0 0 1 1 1 v 5 a 1 1 0 0 1 -1 1 h -5 a 1 1 0 0 1 -1 -1 v -5 a 1 1 0 0 1 1 -1 z", IsBrandIcon = false });
            Categories.Add(new AdminCategoryItem { Name = "Installed Games", IconGeometry = "M 6 11 L 10 11 M 8 9 L 8 13 M 15 12 L 15.01 12 M 18 10 L 18.01 10 M17.32 5H6.68a4 4 0 0 0-3.978 3.59c-.006.052-.01.101-.017.152C2.604 9.416 2 14.456 2 16a3 3 0 0 0 3 3c1 0 1.5-.5 2-1l1.414-1.414A2 2 0 0 1 9.828 16h4.344a2 2 0 0 1 1.414.586L17 18c.5.5 1 1 2 1a3 3 0 0 0 3-3c0-1.545-.604-6.584-.685-7.258-.007-.05-.011-.1-.017-.151A4 4 0 0 0 17.32 5z", IsBrandIcon = false });
            Categories.Add(new AdminCategoryItem { Name = "Applications", IconGeometry = "M 4 3 h 16 a 2 2 0 0 1 2 2 v 10 a 2 2 0 0 1 -2 2 h -16 a 2 2 0 0 1 -2 -2 v -10 a 2 2 0 0 1 2 -2 z M 8 21 h 8 M 12 17 v 4", IsBrandIcon = false });

            // Brand Launcher Categories (Using exact brand geometries with solid fill style enabled)
            Categories.Add(new AdminCategoryItem { Name = "Steam", IconGeometry = "M11.979 0C5.678 0 .511 4.86.022 11.037l6.432 2.658c.545-.371 1.203-.59 1.912-.59.063 0 .125.004.188.006l2.861-4.142V8.91c0-2.495 2.028-4.524 4.524-4.524 2.494 0 4.524 2.031 4.524 4.527s-2.03 4.525-4.524 4.525h-.105l-4.076 2.911c0 .052.004.105.004.159 0 1.875-1.515 3.396-3.39 3.396-1.635 0-3.016-1.173-3.331-2.727L.436 15.27C1.862 20.307 6.486 24 11.979 24c6.627 0 11.999-5.373 11.999-12S18.605 0 11.979 0zM7.54 18.21l-1.473-.61c.262.543.714.999 1.314 1.25 1.297.539 2.793-.076 3.332-1.375.263-.63.264-1.319.005-1.949s-.75-1.121-1.377-1.383c-.624-.26-1.29-.249-1.878-.03l1.523.63c.956.4 1.409 1.5 1.009 2.455-.397.957-1.497 1.41-2.454 1.012H7.54zm11.415-9.303c0-1.662-1.353-3.015-3.015-3.015-1.665 0-3.015 1.353-3.015 3.015 0 1.665 1.35 3.015 3.015 3.015 1.663 0 3.015-1.35 3.015-3.015zm-5.273-.005c0-1.252 1.013-2.266 2.265-2.266 1.249 0 2.266 1.014 2.266 2.266 0 1.251-1.017 2.265-2.266 2.265-1.253 0-2.265-1.014-2.265-2.265", IsBrandIcon = true });
            Categories.Add(new AdminCategoryItem { Name = "Epic Games", IconGeometry = "M3.537 0C2.165 0 1.66.506 1.66 1.879V18.44a4.262 4.262 0 00.02.433c.031.3.037.59.316.92.027.033.311.245.311.245.153.075.258.13.43.2l8.335 3.491c.433.199.614.276.928.27h.002c.314.006.495-.071.928-.27l8.335-3.492c.172-.07.277-.124.43-.2 0 0 .284-.211.311-.243.28-.33.285-.621.316-.92a4.261 4.261 0 00.02-.434V1.879c0-1.373-.506-1.88-1.878-1.88zm13.366 3.11h.68c1.138 0 1.688.553 1.688 1.696v1.88h-1.374v-1.8c0-.369-.17-.54-.523-.54h-.235c-.367 0-.537.17-.537.539v5.81c0 .369.17.54.537.54h.262c.353 0 .523-.171.523-.54V8.619h1.373v2.143c0 1.144-.562 1.71-1.7 1.71h-.694c-1.138 0-1.7-.566-1.7-1.71V4.82c0-1.144.562-1.709 1.7-1.709zm-12.186.08h3.114v1.274H6.117v2.603h1.648v1.275H6.117v2.774h1.74v1.275h-3.14zm3.816 0h2.198c1.138 0 1.7.564 1.7 1.708v2.445c0 1.144-.562 1.71-1.7 1.71h-.799v3.338h-1.4zm4.53 0h1.4v9.201h-1.4zm-3.13 1.235v3.392h.575c.354 0 .523-.171.523-.54V4.965c0-.368-.17-.54-.523-.54zm-3.74 10.147a1.708 1.708 0 01.591.108 1.745 1.745 0 01.49.299l-.452.546a1.247 1.247 0 00-.308-.195.91.91 0 00-.363-.068.658.658 0 00-.28.06.703.703 0 00-.224.163.783.783 0 00-.151.243.799.799 0 00-.056.299v.008a.852.852 0 00.056.31.7.7 0 00.157.245.736.736 0 00.238.16.774.774 0 00.303.058.79.79 0 00.445-.116v-.339h-.548v-.565H7.37v1.255a2.019 2.019 0 01-.524.307 1.789 1.789 0 01-.683.123 1.642 1.642 0 01-.602-.107 1.46 1.46 0 01-.478-.3 1.371 1.371 0 01-.318-.455 1.438 1.438 0 01-.115-.58v-.008a1.426 1.426 0 01.113-.57 1.449 1.449 0 01.312-.46 1.418 1.418 0 01.474-.309 1.58 1.58 0 01.598-.111 1.708 1.708 0 01.045 0zm11.963.008a2.006 2.006 0 01.612.094 1.61 1.61 0 01.507.277l-.386.546a1.562 1.562 0 00-.39-.205 1.178 1.178 0 00-.388-.07.347.347 0 00-.208.052.154.154 0 00-.07.127v.008a.158.158 0 00.022.084.198.198 0 00.076.066.831.831 0 00.147.06c.062.02.14.04.236.061a3.389 3.389 0 01.43.122 1.292 1.292 0 01.328.17.678.678 0 01.207.24.739.739 0 01.071.337v.008a.865.865 0 01-.081.382.82.82 0 01-.229.285 1.032 1.032 0 01-.353.18 1.606 1.606 0 01-.46.061 2.16 2.16 0 01-.71-.116 1.718 1.718 0 01-.593-.346l.43-.514c.277.223.578.335.9.335a.457.457 0 00.236-.05.157.157 0 00.082-.142v-.008a.15.15 0 00-.02-.077.204.204 0 00-.073-.066.753.753 0 00-.143-.062 2.45 2.45 0 00-.233-.062 5.036 5.036 0 01-.413-.113 1.26 1.26 0 01-.331-.16.72.72 0 01-.222-.243.73.73 0 01-.082-.36v-.008a.863.863 0 01.074-.359.794.794 0 01.214-.283 1.007 1.007 0 01.34-.185 1.423 1.423 0 01.448-.066 2.006 2.006 0 01.025 0zm-9.358.025h.742l1.183 2.81h-.825l-.203-.499H8.623l-.198.498h-.81zm2.197.02h.814l.663 1.08.663-1.08h.814v2.79h-.766v-1.602l-.711 1.091h-.016l-.707-1.083v1.593h-.754zm3.469 0h2.235v.658h-1.473v.422h1.334v.61h-1.334v.442h1.493v.658h-2.255zm-5.3.897l-.315.793h.624zm-1.145 5.19h8.014l-4.09 1.348z", IsBrandIcon = true });
            Categories.Add(new AdminCategoryItem { Name = "Battle.net", IconGeometry = "M18.94 8.296C15.9 6.892 11.534 6 7.426 6.332c.206-1.36.714-2.308 1.548-2.508 1.148-.275 2.4.48 3.594 1.854.782.102 1.71.28 2.355.429C12.747 2.013 9.828-.282 7.607.565c-1.688.644-2.553 2.97-2.448 6.094-2.2.468-3.915 1.3-5.013 2.495-.056.065-.181.227-.137.305.034.058.146-.008.194-.04 1.274-.89 2.904-1.373 5.027-1.676.303 3.333 1.713 7.56 4.055 10.952-1.28.502-2.356.536-2.946-.087-.812-.856-.784-2.318-.19-4.04a26.764 26.764 0 0 1-.807-2.254c-2.459 3.934-2.986 7.61-1.143 9.11 1.402 1.14 3.847.725 6.502-.926 1.505 1.672 3.083 2.74 4.667 3.094.084.015.287.043.332-.034.034-.06-.08-.124-.131-.149-1.408-.657-2.64-1.828-3.964-3.515 2.735-1.929 5.691-5.263 7.457-8.988 1.076.86 1.64 1.773 1.398 2.595-.336 1.131-1.615 1.84-3.403 2.185a27.697 27.697 0 0 1-1.548 1.826c4.634.16 8.08-1.22 8.458-3.565.286-1.786-1.295-3.696-4.053-5.17.696-2.139.832-4.04.346-5.588-.029-.08-.106-.27-.196-.27-.068 0-.067.13-.063.187.135 1.547-.263 3.2-1.062 5.19zm-8.533 9.869c-1.96-3.145-3.09-6.849-3.082-10.594 3.702-.124 7.474.748 10.714 2.627-1.743 3.269-4.385 6.1-7.633 7.966h.001z", IsBrandIcon = true });
            Categories.Add(new AdminCategoryItem { Name = "Riot Games", IconGeometry = "M13.458.86 0 7.093l3.353 12.761 2.552-.313-.701-8.024.838-.373 1.447 8.202 4.361-.535-.775-8.857.83-.37 1.591 9.025 4.412-.542-.849-9.708.84-.374 1.74 9.87L24 17.318V3.5Zm.316 19.356.222 1.256L24 23.14v-4.18l-10.22 1.256Z", IsBrandIcon = true });
            Categories.Add(new AdminCategoryItem { Name = "Ubisoft Connect", IconGeometry = "M23.561 11.988C23.301-.304 6.954-4.89.656 6.634c.282.206.661.477.943.672a11.747 11.747 0 00-.976 3.067 11.885 11.885 0 00-.184 2.071C.439 18.818 5.621 24 12.005 24c6.385 0 11.556-5.17 11.556-11.556v-.455zm-20.27 2.06c-.152 1.246-.054 1.636-.054 1.788l-.282.098c-.108-.206-.37-.932-.488-1.908C2.163 10.308 4.7 6.96 8.57 6.33c3.544-.52 6.937 1.68 7.728 4.758l-.282.098c-.087-.087-.228-.336-.77-.878-4.281-4.281-11.002-2.32-11.956 3.74zm11.002 2.081a3.145 3.145 0 01-2.59 1.355 3.15 3.15 0 01-3.155-3.155 3.159 3.159 0 012.927-3.144c1.018-.043 1.972.51 2.416 1.398a2.58 2.58 0 01-.455 2.95c.293.205.575.4.856.595zm6.58.12c-1.669 3.782-5.106 5.766-8.77 5.712-7.034-.347-9.083-8.466-4.38-11.393l.207.206c-.076.108-.358.325-.791 1.182-.51 1.041-.672 2.081-.607 2.732.369 5.67 8.314 6.83 11.045 1.214C21.057 8.217 11.822.401 3.626 6.374l-.184-.184C5.599 2.808 9.816 1.3 13.837 2.309c6.147 1.55 9.453 7.956 7.035 13.94z", IsBrandIcon = true });
            Categories.Add(new AdminCategoryItem { Name = "EA App", IconGeometry = "M16.635 6.162l-5.928 9.377H4.24l1.508-2.3h4.024l1.474-2.335H2.264L.79 13.239h2.156L0 17.84h12.072l4.563-7.259 1.652 2.66h-1.401l-1.473 2.299h4.347l1.473 2.3H24zm-11.461.107L3.7 8.604l9.52-.035 1.474-2.3z", IsBrandIcon = true });
            Categories.Add(new AdminCategoryItem { Name = "Xbox", IconGeometry = "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1.07 14.65c-1.84-.25-3.37-1.12-4.56-2.58.59-.4 1.34-.69 2.22-.86.91-.18 1.95-.27 3.01-.27 1.06 0 2.1.09 3.01.27.88.17 1.63.46 2.22.86-1.19 1.46-2.72 2.33-4.56 2.58-.45.06-.92.1-1.4.1s-.95-.04-1.4-.1zm6.98-4.22c-.68-.34-1.63-.6-2.74-.75 1.5-1.57 2.65-3.41 3.23-5.32 1.3 1.33 2.19 3.1 2.45 5.06-1.11.23-2.19.61-2.94 1.01zM12 8.42c-.59.7-1.29 1.58-1.93 2.55-1.01.12-1.94.34-2.71.65-.63-.35-1.47-.65-2.42-.84.22-1.81.99-3.45 2.14-4.7 1.4 1.25 3.2 2.01 5.17 2.34-.14.28-.27.56-.25.02zm1.93 2.55c-.64-.97-1.34-1.85-1.93-2.55.02-.28.14-.56.25-.02 1.97-.33 3.77-1.09 5.17-2.34 1.15 1.25 1.92 2.89 2.14 4.7-.95.19-1.79.49-2.42.84-.77-.31-1.7-.53-2.71-.65zm-8.83 2.46c-.75-.4-1.83-.78-2.94-1.01.26-1.96 1.15-3.73 2.45-5.06.58 1.91 1.73 3.75 3.23 5.32-1.11.15-2.06.41-2.74.75z", IsBrandIcon = true });
            Categories.Add(new AdminCategoryItem { Name = "Custom", IconGeometry = "M9.594 3.94c.09-.542.56-.94 1.11-.94h2.593c.55 0 1.02.398 1.11.94l.213 1.281c.063.374.313.686.645.87.074.04.147.083.22.127.323.196.72.222 1.062.067l1.175-.53c.5-.226 1.1-.035 1.373.438l1.296 2.247c.273.472.115 1.077-.354 1.378l-1.076.69c-.312.2-.494.55-.494.924v.266c0 .373.182.724.494.924l1.076.69c.469.301.627.906.354 1.378l-1.296 2.247c-.273.473-.873.664-1.373.438l-1.175-.53a1.125 1.125 0 00-1.062.067c-.073.044-.146.087-.22.128-.332.183-.582.495-.645.869l-.213 1.28c-.09.543-.56.94-1.11.94h-2.593c-.55 0-1.02-.398-1.11-.94l-.213-1.28a1.125 1.125 0 00-.645-.869c-.074-.04-.147-.084-.22-.128a1.125 1.125 0 00-1.062-.067l-1.175.53c-.5.226-1.1.035-1.373-.438l-1.296-2.247c-.273-.472-.115-1.077.354-1.378l1.076-.69c.312-.2.494-.55.494-.924v-.266c0-.374-.182-.724-.494-.924l-1.076-.69c-.469-.301-.627-.906-.354-1.378l1.296-2.247c.273-.473.873-.664 1.373-.438l1.175.53c.343.155.74.129 1.062-.067.073-.044.146-.087.22-.128.332-.184.582-.496.645-.87l.213-1.281z", IsBrandIcon = true });

            // Standard Categories continued (Using downloaded high-quality Lucide outline icons)
            Categories.Add(new AdminCategoryItem { Name = "Recently Added", IconGeometry = "M 12 2 a 10 10 0 1 0 0 20 a 10 10 0 1 0 0 -20 z M 12 6 v 6 l 4 2", IsBrandIcon = false });
            Categories.Add(new AdminCategoryItem { Name = "Favorites", IconGeometry = "M11.525 2.295a.53.53 0 0 1 .95 0l2.31 4.679a2.123 2.123 0 0 0 1.595 1.16l5.166.756a.53.53 0 0 1 .294.904l-3.736 3.638a2.123 2.123 0 0 0-.611 1.878l.882 5.14a.53.53 0 0 1-.771.56l-4.618-2.428a2.122 2.122 0 0 0-1.973 0L6.396 21.01a.53.53 0 0 1-.77-.56l.881-5.139a2.122 2.122 0 0 0-.611-1.879L2.16 9.795a.53.53 0 0 1 .294-.906l5.165-.755a2.122 2.122 0 0 0 1.597-1.16z", IsBrandIcon = false });
            Categories.Add(new AdminCategoryItem { Name = "Hidden", IconGeometry = "M10.733 5.076a10.744 10.744 0 0 1 11.205 6.575 1 1 0 0 1 0 .696 10.747 10.747 0 0 1-1.444 2.49 M14.084 14.158a3 3 0 0 1-4.242-4.242 M17.479 17.499a10.75 10.75 0 0 1-15.417-5.151 1 1 0 0 1 0-.696 10.75 10.75 0 0 1 4.446-5.143 M2 2 l 20 20", IsBrandIcon = false });
            Categories.Add(new AdminCategoryItem { Name = "Disabled", IconGeometry = "M 12 2 a 10 10 0 1 0 0 20 a 10 10 0 1 0 0 -20 z M4.929 4.929 L 19.07 19.071", IsBrandIcon = false });
            Categories.Add(new AdminCategoryItem { Name = "Broken", IconGeometry = "m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3 M12 9 v4 M12 17 h0.01", IsBrandIcon = false });
            Categories.Add(new AdminCategoryItem { Name = "Needs Validation", IconGeometry = "M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z M12 8 v 4 M12 16 h0.01", IsBrandIcon = false });

            // Set default selected category to All
            SelectedCategory = Categories[0];
        }

        private bool IsApplication(AdminAppItem item)
        {
            return item.Category == "Applications" ||
                   item.Category == "Developer Tools" ||
                   item.Category == "Web Browser" ||
                   item.Category == "Web Browsers" ||
                   item.Category == "Social / Chat" ||
                   item.Category == "Social & Chat" ||
                   item.Category == "Virtualization" ||
                   item.Category == "Virtualization & Hypervisors" ||
                   item.Category == "Hypervisor" ||
                   item.Category == "Database Console" ||
                   item.Category == "Databases";
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

        partial void OnSelectedViewModeChanged(string value)
        {
            ApplyFilterAndPagination();
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

            // Load 61 popular games from single source of truth MockGameService
            var games = MockGameService.GetStaticGames();
            var templates = new List<(string Name, string Exec, string Cat, string Launcher, string Ver, string Pub, string Path, string Src, string Status, string Size, string Lic, string Svg, string Cover, string Logo, string Bg, string Year)>();

            foreach (var g in games)
            {
                // Map status from game status
                string status = "Installed";
                if (g.Status == "Currently Playing") status = "Installed";
                else if (g.Status == "Locked") status = "Disabled";
                else if (g.Status == "Unavailable") status = "Missing";

                // Generate consistent executables
                string exec = g.Title.Replace(" ", "").Replace(":", "").Replace("-", "").Replace("'", "") + ".exe";

                // Generate clean install path
                string path = $@"C:\Program Files\Games\{g.Title}";
                if (g.Launcher == "Steam")
                    path = $@"C:\Program Files (x86)\Steam\steamapps\common\{g.Title}";
                else if (g.Launcher == "Epic")
                    path = $@"E:\EpicGames\{g.Title}";
                else if (g.Launcher == "Battle.net")
                    path = $@"C:\Program Files (x86)\Battle.net\{g.Title}";

                string license = g.Title.Contains("Counter-Strike") || g.Title.Contains("Valorant") || g.Title.Contains("Apex") || g.Title.Contains("Overwatch") || g.Title.Contains("Dota") ? "Free-to-Play" : "Commercial";

                templates.Add((
                    g.Title,
                    exec,
                    g.Genre,
                    g.Launcher,
                    "1.0.0",
                    g.Developer,
                    path,
                    g.Launcher + " Client",
                    status,
                    "45.0 GB",
                    license,
                    "M12 2H2v10h10V2z", // generic vector path if cover is missing
                    g.ImagePath,
                    g.LogoImage,
                    g.BackgroundImage,
                    g.ReleaseYear
                ));
            }

            // Distinct non-game applications with crisp vector icon paths
            var apps = new List<(string Name, string Exec, string Cat, string Launcher, string Ver, string Pub, string Path, string Src, string Status, string Size, string Lic, string Svg, string Cover, string Logo, string Bg, string Year)>
            {
                ("Visual Studio Code", "Code.exe", "Developer Tools", "Custom", "1.89.1", "Microsoft", @"C:\Users\Admin\AppData\Local\Programs\VSCode", "Local Installer", "Installed", "450 MB", "Free", "M9 5H7a2 2 0 00-2 2v2M5 15v2a2 2 0 002 2h2m10-14h-2a2 2 0 00-2 2v2m4 6v2a2 2 0 00-2 2h-2M9 11H7v2h2v-2zm8 0h-2v2h2v-2z", "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/105600/library_600x900_2x.jpg", "", "", "2015"),
                ("Google Chrome", "chrome.exe", "Web Browser", "Custom", "125.0", "Google LLC", @"C:\Program Files\Google\Chrome\Application", "Web Download", "Installed", "1.2 GB", "Free", "M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5", "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/105600/library_600x900_2x.jpg", "", "", "2008"),
                ("Discord", "Discord.exe", "Social / Chat", "Custom", "1.0.9001", "Discord Inc.", @"C:\Users\Admin\AppData\Local\Discord", "Web Download", "Installed", "220 MB", "Free", "M12 2a10 10 0 100 20 10 10 0 000-20zm0 2c1.66 0 3 3.58 3 8s-1.34 8-3 8-3-3.58-3-8 1.34-8 3-8zm-8 8h16", "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/105600/library_600x900_2x.jpg", "", "", "2015"),
                ("Docker Desktop", "DockerDesktop.exe", "Virtualization", "Custom", "4.29.0", "Docker Inc.", @"C:\Program Files\Docker\Docker", "Enterprise Sync", "Installed", "4.8 GB", "Commercial", "M12 2C6.48 2 2 4.24 2 7v10c0 2.76 4.48 5 10 5s10-2.24 10-5V7c0-2.76-4.48-5-10-5zm0 18c-4.42 0-8-1.79-8-4V9.82c1.78.73 4.7 1.18 8 1.18s6.22-.45 8-1.18V14c0 2.21-3.58 4-8 4z", "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/105600/library_600x900_2x.jpg", "", "", "2013"),
                ("VMware Workstation", "vmware.exe", "Hypervisor", "Custom", "17.5.1", "VMware, Inc.", @"C:\Program Files (x86)\VMware\Workstation", "Enterprise Disk", "Installed", "3.2 GB", "Commercial", "M9 5H7a2 2 0 00-2 2v2M5 15v2a2 2 0 002 2h2m10-14h-2a2 2 0 00-2 2v2m4 6v2a2 2 0 00-2 2h-2M9 11H7v2h2v-2zm8 0h-2v2h2v-2z", "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/105600/library_600x900_2x.jpg", "", "", "1998"),
                ("SQL Server Management Studio", "Ssms.exe", "Database Console", "Custom", "19.3", "Microsoft", @"C:\Program Files (x86)\SSMS", "SQL Server Disk", "Installed", "2.8 GB", "Free", "M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5", "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/105600/library_600x900_2x.jpg", "", "", "2005")
            };

            templates.AddRange(apps);

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

                var isApp = (temp.Cat == "Developer Tools" || temp.Cat == "Web Browser" || temp.Cat == "Social / Chat" || temp.Cat == "Virtualization" || temp.Cat == "Hypervisor" || temp.Cat == "Database Console");
                var resolvedCategory = isApp ? "Applications" : "Games";
                var item = new AdminAppItem
                {
                    Id = $"APP-{1000 + i}",
                    Name = name,
                    Executable = exec,
                    Category = resolvedCategory,
                    GameType = temp.Cat,
                    Launcher = temp.Launcher,
                    Version = temp.Ver,
                    Publisher = temp.Pub,
                    InstallationPath = path,
                    InstallationSource = temp.Src,
                    Status = status,
                    IsEnabled = (status != "Disabled"),
                    LastUpdated = DateTime.Now.AddDays(-i % 30).AddHours(-i % 24).ToString("yyyy-MM-dd HH:mm"),
                    ModifiedBy = (i % 3 == 0) ? "Administrator" : ((i % 3 == 1) ? "System Sync" : "Deploy Service"),
                    Size = temp.Size,
                    License = temp.Lic,
                    IconGeometry = temp.Svg,
                    CoverImage = temp.Cover,
                    LogoImage = temp.Logo,
                    BackgroundImage = temp.Bg,
                    ReleaseYear = temp.Year
                };

                item.PropertyChanged += Item_PropertyChanged;
                _cachedAllItems.Add(item);
            }
        }

        private bool _isSyncingProperties = false;

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not AdminAppItem item) return;

            if (e.PropertyName == nameof(AdminAppItem.IsChecked))
            {
                UpdateSelectedCount();
            }

            if (_isSyncingProperties) return;

            if (e.PropertyName == nameof(AdminAppItem.IsEnabled))
            {
                _isSyncingProperties = true;
                try
                {
                    if (item.IsEnabled && item.Status == "Disabled")
                    {
                        item.Status = "Installed";
                    }
                    else if (!item.IsEnabled && item.Status != "Disabled")
                    {
                        item.Status = "Disabled";
                    }
                }
                finally
                {
                    _isSyncingProperties = false;
                }
                RecalculateCategoryCounts();
                ApplyFilterAndPagination();
            }
            else if (e.PropertyName == nameof(AdminAppItem.Status))
            {
                _isSyncingProperties = true;
                try
                {
                    if (item.Status == "Disabled" && item.IsEnabled)
                    {
                        item.IsEnabled = false;
                    }
                    else if (item.Status != "Disabled" && !item.IsEnabled)
                    {
                        item.IsEnabled = true;
                    }
                }
                finally
                {
                    _isSyncingProperties = false;
                }
                RecalculateCategoryCounts();
                ApplyFilterAndPagination();
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
                    x.GameType.ToLower().Contains(searchLower) ||
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

        private List<AdminAppItem> GetItemsFromParameter(object? parameter)
        {
            var list = new List<AdminAppItem>();
            if (parameter == null) return list;

            if (parameter is System.Collections.IList listParam)
            {
                foreach (var item in listParam)
                {
                    if (item is AdminAppItem appItem)
                    {
                        list.Add(appItem);
                    }
                }
            }
            else if (parameter is AdminAppItem singleItem)
            {
                list.Add(singleItem);
            }

            return list;
        }

        // Action Commands
        [RelayCommand]
        private void Launch(object? parameter)
        {
            var items = GetItemsFromParameter(parameter);
            if (items.Count == 0) return;

            if (items.Count == 1)
            {
                NotificationService.Instance.ShowSuccess($"در حال اجرای برنامه: {items[0].Name}");
            }
            else
            {
                NotificationService.Instance.ShowSuccess($"در حال اجرای {items.Count} برنامه انتخاب شده...");
            }
        }

        [RelayCommand]
        private void Stop(object? parameter)
        {
            var items = GetItemsFromParameter(parameter);
            if (items.Count == 0) return;

            if (items.Count == 1)
            {
                NotificationService.Instance.ShowWarning($"پروسه برنامه متوقف شد: {items[0].Name}");
            }
            else
            {
                NotificationService.Instance.ShowWarning($"پروسه {items.Count} برنامه متوقف شد.");
            }
        }

        [RelayCommand]
        private void Restart(object? parameter)
        {
            var items = GetItemsFromParameter(parameter);
            if (items.Count == 0) return;

            if (items.Count == 1)
            {
                var item = items[0];
                NotificationService.Instance.ShowLoading($"در حال راه‌اندازی مجدد {item.Name}...");
                Task.Delay(1000).ContinueWith(_ =>
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        NotificationService.Instance.ShowSuccess($"برنامه {item.Name} با موفقیت ری‌استارت شد.");
                    });
                });
            }
            else
            {
                NotificationService.Instance.ShowLoading($"در حال راه‌اندازی مجدد {items.Count} برنامه انتخاب شده...");
                Task.Delay(1000).ContinueWith(_ =>
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        NotificationService.Instance.ShowSuccess($"تعداد {items.Count} برنامه با موفقیت ری‌استارت شدند.");
                    });
                });
            }
        }

        [RelayCommand]
        private void Edit(object? parameter)
        {
            var items = GetItemsFromParameter(parameter);
            if (items.Count == 0) return;
            if (items.Count == 1)
            {
                NotificationService.Instance.ShowLoading($"در حال ویرایش پیکربندی: {items[0].Name}");
            }
            else
            {
                NotificationService.Instance.ShowLoading($"در حال ویرایش پیکربندی {items.Count} برنامه...");
            }
        }

        [RelayCommand]
        private void OpenFolder(object? parameter)
        {
            var items = GetItemsFromParameter(parameter);
            if (items.Count == 0) return;
            if (items.Count == 1)
            {
                NotificationService.Instance.ShowSuccess($"پوشه برنامه باز شد: {items[0].InstallationPath}");
            }
            else
            {
                NotificationService.Instance.ShowSuccess($"پوشه {items.Count} برنامه باز شد.");
            }
        }

        [RelayCommand]
        private void CopyPath(object? parameter)
        {
            var items = GetItemsFromParameter(parameter);
            if (items.Count == 0) return;
            if (items.Count == 1)
            {
                try
                {
                    System.Windows.Clipboard.SetText(items[0].InstallationPath);
                    NotificationService.Instance.ShowSuccess("مسیر نصب برنامه در حافظه کپی شد.");
                }
                catch
                {
                    NotificationService.Instance.ShowError("خطا در دسترسی به Clipboard سیستم.");
                }
            }
            else
            {
                try
                {
                    var paths = string.Join(Environment.NewLine, items.Select(x => x.InstallationPath));
                    System.Windows.Clipboard.SetText(paths);
                    NotificationService.Instance.ShowSuccess($"مسیر نصب {items.Count} برنامه در حافظه کپی شد.");
                }
                catch
                {
                    NotificationService.Instance.ShowError("خطا در دسترسی به Clipboard سیستم.");
                }
            }
        }

        [RelayCommand]
        private void Validate(object? parameter)
        {
            var items = GetItemsFromParameter(parameter);
            if (items.Count == 0) return;
            if (items.Count == 1)
            {
                var item = items[0];
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
            else
            {
                NotificationService.Instance.ShowLoading($"در حال اعتبارسنجی فایل‌های {items.Count} برنامه...");
                Task.Delay(1500).ContinueWith(_ =>
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var item in items)
                        {
                            item.Status = "Installed";
                        }
                        NotificationService.Instance.ShowSuccess($"اعتبارسنجی {items.Count} برنامه انتخاب شده تکمیل شد.");
                    });
                });
            }
        }

        [RelayCommand]
        private void ScanMetadata(object? parameter)
        {
            var items = GetItemsFromParameter(parameter);
            if (items.Count == 0) return;
            if (items.Count == 1)
            {
                NotificationService.Instance.ShowLoading($"در حال اسکن متادیتا: {items[0].Name}");
            }
            else
            {
                NotificationService.Instance.ShowLoading($"در حال اسکن متادیتا برای {items.Count} برنامه...");
            }
        }

        [RelayCommand]
        private void Rescan(object? parameter)
        {
            var items = GetItemsFromParameter(parameter);
            if (items.Count == 0) return;
            if (items.Count == 1)
            {
                var item = items[0];
                NotificationService.Instance.ShowLoading($"در حال اسکن مجدد: {item.Name}");
                Task.Delay(1000).ContinueWith(_ =>
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        NotificationService.Instance.ShowSuccess($"اسکن مجدد برنامه {item.Name} با موفقیت انجام شد.");
                    });
                });
            }
            else
            {
                NotificationService.Instance.ShowLoading($"در حال اسکن مجدد {items.Count} برنامه...");
                Task.Delay(1000).ContinueWith(_ =>
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        NotificationService.Instance.ShowSuccess($"اسکن مجدد {items.Count} برنامه با موفقیت انجام شد.");
                    });
                });
            }
        }

        [RelayCommand]
        private void Export(object? parameter)
        {
            var items = GetItemsFromParameter(parameter);
            if (items.Count == 0) return;
            if (items.Count == 1)
            {
                NotificationService.Instance.ShowSuccess($"اطلاعات برنامه صادر شد: {items[0].Id}.json");
            }
            else
            {
                NotificationService.Instance.ShowSuccess($"اطلاعات {items.Count} برنامه با موفقیت صادر شد.");
            }
        }

        [RelayCommand]
        private void Delete(object? parameter)
        {
            var items = GetItemsFromParameter(parameter);
            if (items.Count == 0) return;
            if (items.Count == 1)
            {
                var item = items[0];
                NotificationService.Instance.ShowError($"برنامه {item.Name} از لیست مدیریت حذف شد.");
                _cachedAllItems.Remove(item);
                item.PropertyChanged -= Item_PropertyChanged;
            }
            else
            {
                NotificationService.Instance.ShowError($"تعداد {items.Count} برنامه از لیست مدیریت حذف شدند.");
                foreach (var item in items)
                {
                    _cachedAllItems.Remove(item);
                    item.PropertyChanged -= Item_PropertyChanged;
                }
            }
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

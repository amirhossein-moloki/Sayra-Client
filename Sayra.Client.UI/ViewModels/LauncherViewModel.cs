using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sayra.Client.UI.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Sayra.Client.UI.ViewModels
{
    public partial class LauncherViewModel : ObservableObject
    {
        private readonly IClientBridge _clientBridge;
        private List<AppModel> _allApps = new();

        [ObservableProperty]
        private ObservableCollection<AppModel> _filteredApps = new();

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        public LauncherViewModel(IClientBridge clientBridge)
        {
            _clientBridge = clientBridge;
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                var apps = await _clientBridge.GetApplications();
                _allApps = apps.ToList();
                FilterApps();
            }
            catch (System.Exception ex)
            {
                // In a production app, we'd log this or show a message
                System.Diagnostics.Debug.WriteLine($"Failed to load apps: {ex.Message}");
            }
        }

        partial void OnSearchQueryChanged(string value)
        {
            FilterApps();
        }

        private void FilterApps()
        {
            var filtered = _allApps.Where(a =>
                string.IsNullOrEmpty(SearchQuery) ||
                a.Name.Contains(SearchQuery, System.StringComparison.OrdinalIgnoreCase) ||
                a.Category.Contains(SearchQuery, System.StringComparison.OrdinalIgnoreCase));

            FilteredApps = new ObservableCollection<AppModel>(filtered);
        }

        [RelayCommand]
        private async Task LaunchApp(AppModel app)
        {
            if (app == null) return;
            await _clientBridge.SendCommand("RUN_APP", new { AppId = app.Id });
        }
    }
}

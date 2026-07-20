using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sayra.UI.Models;
using Sayra.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Sayra.Client.GameLibrary.Services;
using Sayra.Client.Launcher.Services;

namespace Sayra.UI.ViewModels
{
    public partial class GameLibraryViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _selectedCategory = "All";

        private readonly List<GameItem> _allGames = new();
        private readonly IGameLibraryService? _gameLibraryService;
        private readonly IGameLauncherService? _launcherService;
        private readonly IGameValidationService? _validationService;

        public ObservableCollection<GameItem> Games { get; } = new();

        // Parameterless constructor for XAML support and design-time fallback
        public GameLibraryViewModel() : this(
            App.ServiceProvider?.GetService<IGameLibraryService>(),
            App.ServiceProvider?.GetService<IGameLauncherService>(),
            App.ServiceProvider?.GetService<IGameValidationService>())
        {
        }

        // DI-friendly constructor
        public GameLibraryViewModel(
            IGameLibraryService? gameLibraryService,
            IGameLauncherService? launcherService,
            IGameValidationService? validationService)
        {
            _gameLibraryService = gameLibraryService;
            _launcherService = launcherService;
            _validationService = validationService;

            Log("Constructor START");
            _ = LoadGamesAsync();
            SubscribeToLauncherEvents();
            Log("Constructor END");
        }

        private void SubscribeToLauncherEvents()
        {
            if (_launcherService == null) return;

            try
            {
                _launcherService.GameStarted += LauncherService_GameStarted;
                _launcherService.GameExited += LauncherService_GameExited;
                _launcherService.GameCrashed += LauncherService_GameCrashed;
                _launcherService.LaunchFailed += LauncherService_LaunchFailed;
                Log("Successfully subscribed to core launcher lifecycle events.");
            }
            catch (Exception ex)
            {
                Log($"Failed to subscribe to launcher events: {ex}");
            }
        }

        private void LauncherService_GameStarted(object? sender, Sayra.Client.Launcher.Events.GameStartedEventArgs e)
        {
            UpdateGameStatusInUI(e.GameId, "Currently Playing", isSelected: true);
        }

        private void LauncherService_GameExited(object? sender, Sayra.Client.Launcher.Events.GameExitedEventArgs e)
        {
            UpdateGameStatusInUI(e.GameId, "Installed", isSelected: false);
            ShowNotification("سیستم سایرا", $"بازی با موفقیت بسته شد.");
        }

        private void LauncherService_GameCrashed(object? sender, Sayra.Client.Launcher.Events.GameCrashedEventArgs e)
        {
            UpdateGameStatusInUI(e.GameId, "Installed", isSelected: false);
            ShowNotification("خطای بازی", $"بازی به طور غیرمنتظره‌ای متوقف شد.");
        }

        private void LauncherService_LaunchFailed(object? sender, Sayra.Client.Launcher.Events.LaunchFailedEventArgs e)
        {
            UpdateGameStatusInUI(e.GameId, "Installed", isSelected: false);
            ShowNotification("خطای اجرا", $"خطا در اجرای بازی: {e.Reason}");
        }

        private void UpdateGameStatusInUI(string gameId, string status, bool isSelected)
        {
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                try
                {
                    foreach (var g in _allGames)
                    {
                        if (g.Id == gameId)
                        {
                            g.Status = status;
                            g.IsSelected = isSelected;
                        }
                        else if (isSelected)
                        {
                            g.IsSelected = false;
                            if (g.Status == "Currently Playing")
                            {
                                g.Status = "Installed";
                            }
                        }
                    }

                    ApplyFilter();
                }
                catch (Exception ex)
                {
                    Log($"Error updating game status in UI: {ex}");
                }
            });
        }

        private void ShowNotification(string title, string message)
        {
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                try
                {
                    Sayra.UI.Services.NotificationService.Instance.ShowSuccess(message);
                }
                catch
                {
                    // Ignore fallback errors
                }
            });
        }

        private async Task LoadGamesAsync()
        {
            try
            {
                List<GameItem>? list = null;

                if (_gameLibraryService != null)
                {
                    Log("Attempting to load games from core IGameLibraryService...");
                    var coreGames = await _gameLibraryService.GetGames();
                    if (coreGames != null && coreGames.Any())
                    {
                        list = new List<GameItem>();
                        foreach (var g in coreGames)
                        {
                            string displayStatus = "Installed";
                            bool isAvailable = g.Enabled;

                            if (_validationService != null)
                            {
                                var valResult = await _validationService.ValidateGameAsync(g);
                                isAvailable = valResult.IsPlayable;
                                switch (valResult.Status)
                                {
                                    case GameValidationStatus.Installed:
                                        displayStatus = "Installed";
                                        break;
                                    case GameValidationStatus.Missing:
                                        displayStatus = "Missing";
                                        break;
                                    case GameValidationStatus.Corrupted:
                                        displayStatus = "Corrupted";
                                        break;
                                    case GameValidationStatus.Disabled:
                                        displayStatus = "Disabled";
                                        break;
                                    case GameValidationStatus.NeedsVerification:
                                        displayStatus = "Validation Required";
                                        break;
                                    case GameValidationStatus.Unsupported:
                                        displayStatus = "Unsupported";
                                        break;
                                    default:
                                        displayStatus = "Unknown";
                                        break;
                                }
                            }
                            else
                            {
                                displayStatus = g.Enabled ? "Installed" : "Locked";
                            }

                            list.Add(new GameItem
                            {
                                Id = g.Id,
                                Title = g.Title,
                                Genre = g.Genre,
                                ImagePath = g.CoverImage,
                                Status = displayStatus,
                                IsAvailable = isAvailable,
                                Description = g.Description,
                                LogoImage = g.LogoImage,
                                BackgroundImage = g.BackgroundImage,
                                Launcher = g.Launcher,
                                Developer = g.Developer,
                                ReleaseYear = g.ReleaseYear,
                                ExecutablePath = g.ExecutablePath
                            });
                        }
                        Log($"Successfully loaded {list.Count} games from core database.");
                    }
                }

                if (list == null || list.Count == 0)
                {
                    Log("Core library returned zero items. Loading games from rich static MockGameService fallback...");
                    var service = new MockGameService();
                    var collection = await service.GetGamesAsync();
                    list = collection.ToList();
                }

                _allGames.Clear();
                foreach (var game in list)
                {
                    _allGames.Add(game);
                }

                ApplyFilter();
                Log("Games list successfully populated and filtered.");
            }
            catch (Exception ex)
            {
                Log($"Population failed: {ex}");
            }
        }

        partial void OnSearchTextChanged(string value)
        {
            ApplyFilter();
        }

        partial void OnSelectedCategoryChanged(string value)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string query = SearchText.Trim().ToLower();
            string category = SelectedCategory.Trim().ToLower();

            Games.Clear();
            foreach (var game in _allGames)
            {
                // Match search query if present
                bool matchesSearch = string.IsNullOrEmpty(query) ||
                                     game.Title.ToLower().Contains(query) ||
                                     game.Genre.ToLower().Contains(query);

                // Match category if selected is not "all"
                bool matchesCategory = true;
                if (category != "all")
                {
                    matchesCategory = game.Genre.ToLower().Contains(category) ||
                                      game.Status.ToLower().Contains(category);
                }

                if (matchesSearch && matchesCategory)
                {
                    Games.Add(game);
                }
            }
        }

        [RelayCommand]
        private async Task PlayGameAsync(GameItem? game)
        {
            if (game == null) return;

            if (game.Status == "Currently Playing")
            {
                ShowNotification("سیستم سایرا", "این بازی در حال حاضر در حال اجراست.");
                return;
            }

            if (_launcherService != null)
            {
                Log($"Invoking core launcher service for game: {game.Title} (Id: {game.Id})");
                bool success = await _launcherService.LaunchGameAsync(game.Id);
                if (!success)
                {
                    Log($"Failed to launch game: {game.Title}");
                }
            }
            else
            {
                // Fallback simulation for visual demonstration if running without Core
                Log($"Mock launch for game: {game.Title}");
                UpdateGameStatusInUI(game.Id, "Currently Playing", isSelected: true);

                // Simulate exit after 10 seconds
                _ = Task.Delay(10000).ContinueWith(_ =>
                {
                    UpdateGameStatusInUI(game.Id, "Installed", isSelected: false);
                    ShowNotification("سیستم سایرا", $"بازی {game.Title} بسته شد.");
                });
            }
        }

        private void Log(string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string formatted = $"[TRACE][GameLibraryViewModel][{timestamp}] {message}";
            System.Diagnostics.Debug.WriteLine(formatted);
            Console.WriteLine(formatted);
        }
    }
}

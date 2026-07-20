using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Sayra.UI.Models;
using Sayra.UI.ViewModels;

namespace Sayra.UI.Views
{
    public partial class GameDetailWindow : Window
    {
        private readonly GameDetailViewModel _viewModel;

        public GameDetailWindow(GameItem game, HomeWindow dashboard)
        {
            InitializeComponent();

            // Resolve or instantiate the GameDetailViewModel
            var viewModel = App.ServiceProvider?.GetService<GameDetailViewModel>();
            if (viewModel == null)
            {
                viewModel = new GameDetailViewModel();
            }

            _viewModel = viewModel;
            _viewModel.Initialize(game);
            this.DataContext = _viewModel;

            // Wire up CloseRequested event to close the window cleanly
            _viewModel.CloseRequested += () =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        this.Close();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[GameDetailWindow] Error during Close: {ex.Message}");
                    }
                });
            };

            // Unsubscribe and dispose viewmodel to prevent memory leaks
            this.Closed += (s, e) =>
            {
                _viewModel.Dispose();
            };
        }
    }
}
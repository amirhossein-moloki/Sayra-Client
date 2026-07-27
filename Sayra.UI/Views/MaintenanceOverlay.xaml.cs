using System;
using System.Windows;
using Sayra.Client.Shared.Models;

namespace Sayra.UI.Views
{
    public partial class MaintenanceOverlay : Window
    {
        public MaintenanceOverlay()
        {
            InitializeComponent();
            this.Loaded += MaintenanceOverlay_Loaded;
            this.Unloaded += MaintenanceOverlay_Unloaded;
        }

        private void MaintenanceOverlay_Loaded(object sender, RoutedEventArgs e)
        {
            // Lock window focus or prevent activation if needed
            this.Focus();
        }

        private void MaintenanceOverlay_Unloaded(object sender, RoutedEventArgs e)
        {
            MaintenanceCarousel.StopCurrentPlayback();
        }

        public void PlayAd(AdCampaign campaign)
        {
            Dispatcher.Invoke(() =>
            {
                MaintenanceCarousel.PlayCampaign(campaign);
            });
        }

        public void StopAd()
        {
            Dispatcher.Invoke(() =>
            {
                MaintenanceCarousel.StopCurrentPlayback();
            });
        }
    }
}

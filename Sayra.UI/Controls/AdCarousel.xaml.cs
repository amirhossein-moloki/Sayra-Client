using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Sayra.Client.Shared.Models;

namespace Sayra.UI.Controls
{
    public partial class AdCarousel : UserControl
    {
        private AdCampaign? _currentCampaign;
        private DispatcherTimer? _playbackTimer;
        private int _secondsRemaining;
        private DateTime _playbackStartTime;

        public event Action<string, ImpressionType, double>? OnAdInteraction;

        public AdCarousel()
        {
            InitializeComponent();
            this.MouseDown += AdCarousel_MouseDown;
        }

        public void PlayCampaign(AdCampaign campaign)
        {
            if (campaign == null) return;

            // Fade out current
            var fadeOut = (Storyboard)MainContainer.Resources["FadeOutStoryboard"];
            fadeOut.Completed += (s, e) =>
            {
                SetupCampaignView(campaign);
                // Fade back in
                var fadeIn = (Storyboard)MainContainer.Resources["FadeInStoryboard"];
                fadeIn.Begin(this);
            };
            fadeOut.Begin(this);
        }

        private void SetupCampaignView(AdCampaign campaign)
        {
            StopCurrentPlayback();

            _currentCampaign = campaign;
            _playbackStartTime = DateTime.UtcNow;
            _secondsRemaining = campaign.DisplayDurationSeconds <= 0 ? 10 : campaign.DisplayDurationSeconds;
            CampaignNameText.Text = campaign.Name;
            TimerText.Text = $"{_secondsRemaining}s";

            // Hide all
            AdImage.Visibility = Visibility.Collapsed;
            AdVideo.Visibility = Visibility.Collapsed;
            AdWeb.Visibility = Visibility.Collapsed;

            try
            {
                switch (campaign.Type)
                {
                    case CampaignType.IMAGE:
                        if (File.Exists(campaign.MediaLocalPath))
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.UriSource = new Uri(Path.GetFullPath(campaign.MediaLocalPath));
                            bitmap.EndInit();
                            AdImage.Source = bitmap;
                        }
                        else
                        {
                            // Fallback inside application resources if file not found
                            AdImage.Source = null;
                        }
                        AdImage.Visibility = Visibility.Visible;
                        break;

                    case CampaignType.VIDEO:
                        if (File.Exists(campaign.MediaLocalPath))
                        {
                            AdVideo.Source = new Uri(Path.GetFullPath(campaign.MediaLocalPath));
                            AdVideo.Play();
                        }
                        AdVideo.Visibility = Visibility.Visible;
                        break;

                    case CampaignType.HTML:
                        if (!string.IsNullOrEmpty(campaign.TargetUrl))
                        {
                            AdWeb.Navigate(campaign.TargetUrl);
                        }
                        else if (File.Exists(campaign.MediaLocalPath))
                        {
                            AdWeb.Navigate(new Uri(Path.GetFullPath(campaign.MediaLocalPath)));
                        }
                        AdWeb.Visibility = Visibility.Visible;
                        break;
                }

                // Track VIEW impression
                OnAdInteraction?.Invoke(campaign.CampaignId, ImpressionType.VIEW, 0);

                // Start countdown timer
                _playbackTimer = new DispatcherTimer();
                _playbackTimer.Interval = TimeSpan.FromSeconds(1);
                _playbackTimer.Tick += PlaybackTimer_Tick;
                _playbackTimer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error playing ad: {ex.Message}");
            }
        }

        private void PlaybackTimer_Tick(object? sender, EventArgs e)
        {
            _secondsRemaining--;
            if (_secondsRemaining <= 0)
            {
                StopCurrentPlayback();
                TimerText.Text = "0s";
                if (_currentCampaign != null)
                {
                    double duration = (DateTime.UtcNow - _playbackStartTime).TotalSeconds;
                    // Trigger interaction track (duration completed)
                    OnAdInteraction?.Invoke(_currentCampaign.CampaignId, ImpressionType.VIEW, duration);
                }
            }
            else
            {
                TimerText.Text = $"{_secondsRemaining}s";
            }
        }

        public void StopCurrentPlayback()
        {
            if (_playbackTimer != null)
            {
                _playbackTimer.Stop();
                _playbackTimer = null;
            }

            try
            {
                AdVideo.Stop();
                AdVideo.Source = null;
            }
            catch { }

            _currentCampaign = null;
        }

        private void AdCarousel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_currentCampaign != null)
            {
                double duration = (DateTime.UtcNow - _playbackStartTime).TotalSeconds;
                // Track CLICK impression
                OnAdInteraction?.Invoke(_currentCampaign.CampaignId, ImpressionType.CLICK, duration);

                if (!string.IsNullOrEmpty(_currentCampaign.TargetUrl))
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = _currentCampaign.TargetUrl,
                            UseShellExecute = true
                        });
                    }
                    catch { }
                }
            }
        }

        public void SkipAd()
        {
            if (_currentCampaign != null)
            {
                double duration = (DateTime.UtcNow - _playbackStartTime).TotalSeconds;
                OnAdInteraction?.Invoke(_currentCampaign.CampaignId, ImpressionType.SKIP, duration);
                StopCurrentPlayback();
            }
        }
    }
}

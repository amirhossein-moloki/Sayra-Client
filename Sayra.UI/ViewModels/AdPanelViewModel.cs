using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Sayra.Client.LocalAdmin.Models;
using Sayra.Client.LocalAdmin.Services;

namespace Sayra.UI.ViewModels
{
    public partial class AdPanelViewModel : ObservableObject
    {
        private readonly IAdvertisementService? _adService;
        private List<Advertisement> _ads = new();
        private int _currentIndex = -1;
        private DispatcherTimer? _rotationTimer;

        [ObservableProperty]
        private string _title = "پیشنهاد ویژه سایرا";

        [ObservableProperty]
        private string _promoTitle = "شارژ کیف پول با ۲۰٪ هدیه";

        [ObservableProperty]
        private string _subtitle = "به مدت محدود";

        [ObservableProperty]
        private string _description = "با شارژ حساب خود در جشنواره تابستانه سایرا، از ۲۰٪ اعتبار هدیه بدون سقف بهره‌مند شوید. این اعتبار بلافاصله فعال شده و برای تمام بازی‌ها قابل استفاده است.";

        [ObservableProperty]
        private string _buttonText = "شارژ آنلاین حساب";

        [ObservableProperty]
        private string _illustrationTitle = "سفارش آنلاین بوفه گیم‌نت";

        [ObservableProperty]
        private string _illustrationSubtitle = "انواع نوشیدنی و خوراکی درب سیستم شما";

        [ObservableProperty]
        private string _illustrationIconPath = "M11 9H9V2H7V9H5V2H3V9C3 11.12 4.66 12.84 6.75 12.97V22H9.25V12.97C11.34 12.84 13 11.12 13 9M16 6V14H18.5V22H21V2C18.24 2 16 4.24 16 6Z";

        [ObservableProperty]
        private bool _hasAds;

        public AdPanelViewModel() : this(App.ServiceProvider?.GetService<IAdvertisementService>())
        {
        }

        public AdPanelViewModel(IAdvertisementService? adService)
        {
            _adService = adService;
            _ = LoadAdsAsync();
        }

        private async Task LoadAdsAsync()
        {
            if (_adService == null) return;

            try
            {
                var activeAds = await _adService.GetActiveAdvertisementsAsync();
                _ads = activeAds.OrderByDescending(a => a.Priority).ToList();

                if (_ads.Any())
                {
                    HasAds = true;
                    _currentIndex = 0;
                    DisplayAd(_ads[_currentIndex]);

                    if (_ads.Count > 1)
                    {
                        _rotationTimer = new DispatcherTimer
                        {
                            Interval = TimeSpan.FromSeconds(10)
                        };
                        _rotationTimer.Tick += RotationTimer_Tick;
                        _rotationTimer.Start();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load advertisements: {ex.Message}");
            }
        }

        private void RotationTimer_Tick(object? sender, EventArgs e)
        {
            if (!_ads.Any()) return;
            _currentIndex = (_currentIndex + 1) % _ads.Count;
            DisplayAd(_ads[_currentIndex]);
        }

        private void DisplayAd(Advertisement ad)
        {
            PromoTitle = ad.Title;
            Description = ad.Description;
            ButtonText = !string.IsNullOrWhiteSpace(ad.ButtonText) ? ad.ButtonText : "مشاهده جزئیات";
            Subtitle = ad.EndTime.HasValue ? $"معتبر تا {ad.EndTime.Value.ToLocalTime():yyyy/MM/dd}" : "به مدت محدود";
        }

        [RelayCommand]
        private void ExecuteCta()
        {
            if (_currentIndex >= 0 && _currentIndex < _ads.Count)
            {
                var ad = _ads[_currentIndex];
                if (!string.IsNullOrWhiteSpace(ad.ActionUrl))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(ad.ActionUrl) { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to open CTA URL: {ex.Message}");
                    }
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.LocalAdmin.Models;

namespace Sayra.Client.LocalAdmin.Services
{
    public class AdvertisementService : IAdvertisementService
    {
        private readonly string _basePath;
        private readonly string _filePath;
        private readonly ILogger<AdvertisementService>? _logger;
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };

        public AdvertisementService(ILogger<AdvertisementService>? logger = null)
        {
            _basePath = Path.Combine(AppContext.BaseDirectory, "Data", "Configuration");
            _filePath = Path.Combine(_basePath, "advertisements.json");
            _logger = logger;
        }

        public async Task<IEnumerable<Advertisement>> GetActiveAdvertisementsAsync()
        {
            EnsureDirectoryExists();

            var ads = new List<Advertisement>();
            if (File.Exists(_filePath))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(_filePath);
                    var deserialized = JsonSerializer.Deserialize<List<Advertisement>>(json, JsonOptions);
                    if (deserialized != null)
                    {
                        ads = deserialized;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to load advertisements from {FilePath}", _filePath);
                }
            }

            // Populate high-quality default advertisements if file is empty/non-existent
            if (ads.Count == 0)
            {
                ads.Add(new Advertisement
                {
                    Id = "promo-summer-wallet",
                    Title = "شارژ کیف پول با ۲۰٪ هدیه",
                    Description = "با شارژ حساب خود در جشنواره تابستانه سایرا، از ۲۰٪ اعتبار هدیه بدون سقف بهره‌مند شوید. این اعتبار بلافاصله فعال شده و برای تمام بازی‌ها قابل استفاده است.",
                    ButtonText = "شارژ آنلاین حساب",
                    ActionUrl = "https://sayragaming.ir/wallet",
                    Priority = 10,
                    StartTime = DateTime.UtcNow.AddDays(-1),
                    EndTime = DateTime.UtcNow.AddDays(30),
                    IsActive = true
                });

                ads.Add(new Advertisement
                {
                    Id = "promo-online-buffet",
                    Title = "سفارش آنلاین بوفه گیم‌نت",
                    Description = "دیگر نیازی به ترک صندلی خود ندارید! انواع نوشیدنی، میان‌وعده و تنقلات را به صورت آنلاین سفارش دهید و درب سیستم خود تحویل بگیرید.",
                    ButtonText = "سفارش آنلاین بوفه",
                    ActionUrl = "https://sayragaming.ir/buffet",
                    Priority = 5,
                    StartTime = DateTime.UtcNow.AddDays(-1),
                    EndTime = DateTime.UtcNow.AddDays(60),
                    IsActive = true
                });

                await SaveAdvertisementsAsync(ads);
            }

            // Filter active, scheduled and non-expired ads
            var now = DateTime.UtcNow;
            return ads.FindAll(ad =>
                ad.IsActive &&
                (!ad.StartTime.HasValue || ad.StartTime.Value <= now) &&
                (!ad.EndTime.HasValue || ad.EndTime.Value >= now)
            );
        }

        public async Task AddAdvertisementAsync(Advertisement ad)
        {
            if (ad == null) throw new ArgumentNullException(nameof(ad));
            EnsureDirectoryExists();

            var ads = new List<Advertisement>();
            if (File.Exists(_filePath))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(_filePath);
                    ads = JsonSerializer.Deserialize<List<Advertisement>>(json, JsonOptions) ?? new List<Advertisement>();
                }
                catch { }
            }

            ads.RemoveAll(a => a.Id == ad.Id);
            ads.Add(ad);
            await SaveAdvertisementsAsync(ads);
        }

        public async Task RemoveAdvertisementAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            EnsureDirectoryExists();

            if (File.Exists(_filePath))
            {
                var ads = new List<Advertisement>();
                try
                {
                    string json = await File.ReadAllTextAsync(_filePath);
                    ads = JsonSerializer.Deserialize<List<Advertisement>>(json, JsonOptions) ?? new List<Advertisement>();
                }
                catch { }

                if (ads.RemoveAll(a => a.Id == id) > 0)
                {
                    await SaveAdvertisementsAsync(ads);
                }
            }
        }

        public Task TriggerServerSyncHookAsync()
        {
            _logger?.LogInformation("Advertisement Server Sync hook triggered. Future implementation will pull from API.");
            return Task.CompletedTask;
        }

        private async Task SaveAdvertisementsAsync(List<Advertisement> ads)
        {
            try
            {
                string json = JsonSerializer.Serialize(ads, JsonOptions);
                await File.WriteAllTextAsync(_filePath, json);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to save advertisements to {FilePath}", _filePath);
            }
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_basePath))
            {
                Directory.CreateDirectory(_basePath);
            }
        }
    }
}

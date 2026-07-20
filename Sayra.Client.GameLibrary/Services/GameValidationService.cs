using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.GameLibrary.Models;

namespace Sayra.Client.GameLibrary.Services
{
    public class GameValidationService : IGameValidationService
    {
        private readonly ILogger<GameValidationService>? _logger;

        public GameValidationService(ILogger<GameValidationService>? logger = null)
        {
            _logger = logger;
        }

        public Task<GameValidationResult> ValidateGameAsync(Game game)
        {
            if (game == null)
            {
                return Task.FromResult(new GameValidationResult
                {
                    Status = GameValidationStatus.Unknown,
                    Message = "Game object is null",
                    IsPlayable = false
                });
            }

            _logger?.LogInformation("Validating game: {GameName} (ID: {GameId})", game.Name, game.Id);

            // 1. Check if game is disabled
            if (!game.Enabled)
            {
                return Task.FromResult(new GameValidationResult
                {
                    Status = GameValidationStatus.Disabled,
                    Message = "نشست بازی غیرفعال شده است.",
                    IsPlayable = false
                });
            }

            // 2. Validate metadata
            if (string.IsNullOrWhiteSpace(game.Name) || string.IsNullOrWhiteSpace(game.ExecutablePath))
            {
                return Task.FromResult(new GameValidationResult
                {
                    Status = GameValidationStatus.Corrupted,
                    Message = "اطلاعات متادیتای بازی نامعتبر است.",
                    IsPlayable = false
                });
            }

            // 3. Check installation folder exists
            string? workingDir = game.WorkingDirectory;
            if (string.IsNullOrWhiteSpace(workingDir))
            {
                try
                {
                    workingDir = Path.GetDirectoryName(game.ExecutablePath);
                }
                catch
                {
                    // Invalid path format
                }
            }

            if (string.IsNullOrWhiteSpace(workingDir) || !Directory.Exists(workingDir))
            {
                return Task.FromResult(new GameValidationResult
                {
                    Status = GameValidationStatus.Missing,
                    Message = "پوشه نصب بازی یافت نشد.",
                    IsPlayable = false
                });
            }

            // 4. Check if launcher is unsupported or doesn't exist
            if (!string.IsNullOrWhiteSpace(game.Launcher))
            {
                string launcherLower = game.Launcher.ToLowerInvariant();
                if (launcherLower == "steam" || launcherLower == "epic" || launcherLower == "riot" || launcherLower == "uplay" || launcherLower == "ea" || launcherLower == "battlenet")
                {
                    // Supported launcher
                }
                else if (launcherLower != "custom" && launcherLower != "manual" && launcherLower != "sayra")
                {
                    return Task.FromResult(new GameValidationResult
                    {
                        Status = GameValidationStatus.Unsupported,
                        Message = "لانچر بازی پشتیبانی نمی‌شود.",
                        IsPlayable = false
                    });
                }
            }

            // 5. Check if executable exists and is accessible
            if (!File.Exists(game.ExecutablePath))
            {
                return Task.FromResult(new GameValidationResult
                {
                    Status = GameValidationStatus.Missing,
                    Message = "فایل اجرایی بازی یافت نشد.",
                    IsPlayable = false
                });
            }

            // Check executable access / permissions
            try
            {
                using (var fs = File.Open(game.ExecutablePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    // Accessible
                }
            }
            catch (UnauthorizedAccessException)
            {
                return Task.FromResult(new GameValidationResult
                {
                    Status = GameValidationStatus.NeedsVerification,
                    Message = "عدم دسترسی کافی برای اجرای فایل بازی.",
                    IsPlayable = false
                });
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to open executable stream for {Path}", game.ExecutablePath);
                return Task.FromResult(new GameValidationResult
                {
                    Status = GameValidationStatus.Corrupted,
                    Message = "فایل اجرایی بازی خراب یا آسیب‌دیده است.",
                    IsPlayable = false
                });
            }

            // 6. Validation Succeeded
            return Task.FromResult(new GameValidationResult
            {
                Status = GameValidationStatus.Installed,
                Message = "بازی با موفقیت تایید شد.",
                IsPlayable = true
            });
        }
    }
}

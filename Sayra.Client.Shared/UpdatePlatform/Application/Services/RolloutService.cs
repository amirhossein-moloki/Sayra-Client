using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Service governing staged rollout percentage, pause/resume/cancel states, and deterministic client selection.
    /// </summary>
    public class RolloutService : IRolloutService
    {
        public bool IsDeviceEligibleForRollout(string deviceId, RolloutConfiguration config)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                throw new ArgumentException("Device ID cannot be null or empty.", nameof(deviceId));
            }
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            // Check if cancelled
            if (config.IsCancelled)
            {
                throw new RolloutRejectedException("The rollout has been cancelled.");
            }

            // Check if paused
            if (config.IsPaused)
            {
                throw new RolloutRejectedException("The rollout is currently paused.");
            }

            // Check if device is excluded
            if (config.ExcludedDeviceIds != null && config.ExcludedDeviceIds.Contains(deviceId, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            // Check rollout percentage using workstation-bound deterministic hash seeding
            int scale = GetDeterministicScore(deviceId, config.CampaignId);
            return scale < config.RolloutPercentage;
        }

        public void PauseRollout(RolloutConfiguration config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.IsPaused = true;
        }

        public void ResumeRollout(RolloutConfiguration config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.IsPaused = false;
        }

        public void CancelRollout(RolloutConfiguration config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.IsCancelled = true;
        }

        private int GetDeterministicScore(string deviceId, Guid campaignId)
        {
            // Combine deviceId and campaignId to form a unique string
            string input = $"{campaignId}:{deviceId.ToLowerInvariant()}";
            using (var sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                // Convert first 4 bytes to int
                int hashInt = BitConverter.ToInt32(hashBytes, 0);
                // Get absolute value and mod by 100 to get a score between 0 and 99
                return Math.Abs(hashInt % 100);
            }
        }
    }
}

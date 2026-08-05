using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Fleet.Assets.Interfaces;
using Sayra.Client.Shared.Interfaces.Phase9;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Fleet.Assets.Services
{
    /// <summary>
    /// Implements <see cref="IAssetManagementService"/> to handle asset tracking, compliance, and license allocation.
    /// </summary>
    public class AssetManagementService : IAssetManagementService
    {
        private readonly ILogger<AssetManagementService> _logger;
        private readonly IAssetRepository _assetRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssetManagementService"/> class.
        /// </summary>
        public AssetManagementService(ILogger<AssetManagementService> logger, IAssetRepository assetRepository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _assetRepository = assetRepository ?? throw new ArgumentNullException(nameof(assetRepository));
        }

        /// <inheritdoc />
        public async Task<bool> TrackAssetAsync(AssetRecord asset, CancellationToken ct = default)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));

            _logger.LogInformation("Tracking and saving asset record '{AssetId}' on machine '{MachineId}'...", asset.AssetId, asset.MachineId);

            // Audit Validation and Integrity Checks (Part 8 Asset Security)
            if (string.IsNullOrEmpty(asset.AssetId) || string.IsNullOrEmpty(asset.MachineId))
            {
                _logger.LogWarning("Asset record validation failed during tracking.");
                return false;
            }

            // Save to secure SQLCipher repository
            bool success = await _assetRepository.SaveAssetAsync(asset, ct);
            if (success)
            {
                // Record History Event: Added / Updated
                await _assetRepository.RecordHistoryAsync(new AssetHistory
                {
                    HistoryId = Guid.NewGuid().ToString(),
                    AssetId = asset.AssetId,
                    MachineId = asset.MachineId,
                    TimestampUtc = DateTime.UtcNow,
                    EventType = "Tracked",
                    Description = $"Asset '{asset.Name}' was tracked manually or programmatically.",
                    OperatorId = "Operator"
                }, ct);
            }

            return success;
        }

        /// <inheritdoc />
        public async Task<bool> CheckoutLicenseSeatAsync(string licenseId, string machineId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(licenseId) || string.IsNullOrEmpty(machineId)) return false;

            _logger.LogInformation("Checking out license seat '{LicenseId}' for machine '{MachineId}'...", licenseId, machineId);

            // Attempt to retrieve active license asset or create a dummy tracked asset for seat allocation
            var asset = await _assetRepository.GetAssetAsync(licenseId, ct);
            int capacity = 100;
            int used = 0;

            if (asset != null)
            {
                int.TryParse(asset.Specifications.GetValueOrDefault("SeatCapacity", "100"), out capacity);
                int.TryParse(asset.Specifications.GetValueOrDefault("ActiveSeatsUsed", "0"), out used);
            }

            if (used >= capacity)
            {
                _logger.LogWarning("Cannot checkout license seat '{LicenseId}'; capacity reached ({Used}/{Capacity}).", licenseId, used, capacity);
                return false;
            }

            var updatedSpecs = asset != null ? new Dictionary<string, string>(asset.Specifications) : new Dictionary<string, string>();
            updatedSpecs["SeatCapacity"] = capacity.ToString();
            updatedSpecs["ActiveSeatsUsed"] = (used + 1).ToString();
            updatedSpecs[$"SeatHolder-{machineId}"] = DateTime.UtcNow.ToString("O");

            var updatedAsset = new AssetRecord
            {
                AssetId = licenseId,
                MachineId = machineId,
                Name = asset?.Name ?? $"Floating License {licenseId}",
                SerialOrSignature = asset?.SerialOrSignature ?? licenseId,
                Category = AssetType.License,
                Status = AssetStatus.Active,
                Specifications = updatedSpecs
            };

            bool success = await _assetRepository.SaveAssetAsync(updatedAsset, ct);
            if (success)
            {
                await _assetRepository.RecordHistoryAsync(new AssetHistory
                {
                    HistoryId = Guid.NewGuid().ToString(),
                    AssetId = licenseId,
                    MachineId = machineId,
                    TimestampUtc = DateTime.UtcNow,
                    EventType = "LicenseCheckout",
                    Description = $"License seat '{licenseId}' successfully checked out by workstation '{machineId}'.",
                    OperatorId = "System"
                }, ct);
            }

            return success;
        }

        /// <inheritdoc />
        public async Task<bool> ReleaseLicenseSeatAsync(string licenseId, string machineId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(licenseId) || string.IsNullOrEmpty(machineId)) return false;

            _logger.LogInformation("Releasing license seat '{LicenseId}' held by machine '{MachineId}'...", licenseId, machineId);

            var asset = await _assetRepository.GetAssetAsync(licenseId, ct);
            if (asset == null)
            {
                _logger.LogWarning("License asset '{LicenseId}' not found for releasing.", licenseId);
                return false;
            }

            int.TryParse(asset.Specifications.GetValueOrDefault("ActiveSeatsUsed", "0"), out int used);
            if (used <= 0)
            {
                _logger.LogWarning("No active seats are checked out for license '{LicenseId}'.", licenseId);
                return false;
            }

            var updatedSpecs = new Dictionary<string, string>(asset.Specifications);
            updatedSpecs["ActiveSeatsUsed"] = Math.Max(0, used - 1).ToString();
            updatedSpecs.Remove($"SeatHolder-{machineId}");

            var updatedAsset = asset with { Specifications = updatedSpecs };
            bool success = await _assetRepository.SaveAssetAsync(updatedAsset, ct);
            if (success)
            {
                await _assetRepository.RecordHistoryAsync(new AssetHistory
                {
                    HistoryId = Guid.NewGuid().ToString(),
                    AssetId = licenseId,
                    MachineId = machineId,
                    TimestampUtc = DateTime.UtcNow,
                    EventType = "LicenseRelease",
                    Description = $"License seat '{licenseId}' was released back to pool by workstation '{machineId}'.",
                    OperatorId = "System"
                }, ct);
            }

            return success;
        }
    }
}

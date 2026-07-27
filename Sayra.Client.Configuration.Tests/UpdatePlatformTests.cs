using System;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;
using Sayra.Client.Shared.UpdatePlatform.Domain.Enums;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;
using Sayra.Client.Shared.UpdatePlatform.Domain.Options;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;
using Sayra.Client.Shared.UpdatePlatform.Application.Dtos;
using Sayra.Client.Shared.UpdatePlatform.Application.Validation;

namespace Sayra.Client.Configuration.Tests
{
    /// <summary>
    /// Comprehensive test suite verifying Update Platform Foundation contracts, DTOs, enums, options, and validations.
    /// </summary>
    public class UpdatePlatformTests
    {
        #region Enum Serialization Tests

        [Theory]
        [InlineData(UpdateState.Idle, "0")]
        [InlineData(UpdateState.Checking, "1")]
        [InlineData(UpdateState.Available, "2")]
        [InlineData(UpdateState.Downloading, "3")]
        [InlineData(UpdateState.Verifying, "4")]
        [InlineData(UpdateState.Installing, "5")]
        [InlineData(UpdateState.Completed, "6")]
        [InlineData(UpdateState.Failed, "7")]
        [InlineData(UpdateState.RollingBack, "8")]
        [InlineData(UpdateState.RolledBack, "9")]
        [InlineData(UpdateState.Cancelled, "10")]
        public void UpdateState_ShouldSerializeAndDeserializeCorrectly(UpdateState state, string expectedValue)
        {
            // Act
            string json = JsonSerializer.Serialize(state);
            UpdateState deserialized = JsonSerializer.Deserialize<UpdateState>(json);

            // Assert
            Assert.Equal(expectedValue, json);
            Assert.Equal(state, deserialized);
        }

        [Fact]
        public void UpdateType_ShouldSerializeAndDeserializeCorrectly()
        {
            foreach (UpdateType type in Enum.GetValues(typeof(UpdateType)))
            {
                string json = JsonSerializer.Serialize(type);
                UpdateType deserialized = JsonSerializer.Deserialize<UpdateType>(json);
                Assert.Equal(type, deserialized);
            }
        }

        [Fact]
        public void UpdatePriority_ShouldSerializeAndDeserializeCorrectly()
        {
            foreach (UpdatePriority priority in Enum.GetValues(typeof(UpdatePriority)))
            {
                string json = JsonSerializer.Serialize(priority);
                UpdatePriority deserialized = JsonSerializer.Deserialize<UpdatePriority>(json);
                Assert.Equal(priority, deserialized);
            }
        }

        [Fact]
        public void DeploymentRing_ShouldSerializeAndDeserializeCorrectly()
        {
            foreach (DeploymentRing ring in Enum.GetValues(typeof(DeploymentRing)))
            {
                string json = JsonSerializer.Serialize(ring);
                DeploymentRing deserialized = JsonSerializer.Deserialize<DeploymentRing>(json);
                Assert.Equal(ring, deserialized);
            }
        }

        [Fact]
        public void UpdateChannel_ShouldSerializeAndDeserializeCorrectly()
        {
            foreach (UpdateChannel channel in Enum.GetValues(typeof(UpdateChannel)))
            {
                string json = JsonSerializer.Serialize(channel);
                UpdateChannel deserialized = JsonSerializer.Deserialize<UpdateChannel>(json);
                Assert.Equal(channel, deserialized);
            }
        }

        [Fact]
        public void RollbackStatus_ShouldSerializeAndDeserializeCorrectly()
        {
            foreach (RollbackStatus status in Enum.GetValues(typeof(RollbackStatus)))
            {
                string json = JsonSerializer.Serialize(status);
                RollbackStatus deserialized = JsonSerializer.Deserialize<RollbackStatus>(json);
                Assert.Equal(status, deserialized);
            }
        }

        [Fact]
        public void PackageType_ShouldSerializeAndDeserializeCorrectly()
        {
            foreach (PackageType pType in Enum.GetValues(typeof(PackageType)))
            {
                string json = JsonSerializer.Serialize(pType);
                PackageType deserialized = JsonSerializer.Deserialize<PackageType>(json);
                Assert.Equal(pType, deserialized);
            }
        }

        #endregion

        #region DTO Serialization Tests

        [Fact]
        public void UpdateCheckRequestDto_ShouldSupportFullSerializationCycle()
        {
            // Arrange
            var dto = new UpdateCheckRequestDto
            {
                WorkstationId = "WS-CYBER-09",
                CurrentVersion = "1.0.4",
                Channel = UpdateChannel.Stable,
                DeploymentRing = DeploymentRing.Production
            };

            // Act
            string json = JsonSerializer.Serialize(dto);
            var deserialized = JsonSerializer.Deserialize<UpdateCheckRequestDto>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(dto.WorkstationId, deserialized.WorkstationId);
            Assert.Equal(dto.CurrentVersion, deserialized.CurrentVersion);
            Assert.Equal(dto.Channel, deserialized.Channel);
            Assert.Equal(dto.DeploymentRing, deserialized.DeploymentRing);
        }

        [Fact]
        public void UpdateCheckResponseDto_ShouldSupportFullSerializationCycle()
        {
            // Arrange
            var dto = new UpdateCheckResponseDto
            {
                UpdateAvailable = true,
                Manifest = new UpdateManifestDto
                {
                    Id = Guid.NewGuid(),
                    Version = "2.0.0-rc.1",
                    ProductName = "SAYRA Client",
                    Description = "Major enterprise deployment",
                    PackageType = PackageType.FullPackage,
                    UpdateType = UpdateType.Full,
                    RequiredVersion = "1.0.0",
                    MinimumClientVersion = "1.0.0",
                    ReleaseDate = DateTime.UtcNow,
                    Priority = UpdatePriority.Critical,
                    Channel = UpdateChannel.Stable,
                    SignatureMetadata = "base64SignatureHashString"
                }
            };

            // Act
            string json = JsonSerializer.Serialize(dto);
            var deserialized = JsonSerializer.Deserialize<UpdateCheckResponseDto>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.True(deserialized.UpdateAvailable);
            Assert.NotNull(deserialized.Manifest);
            Assert.Equal(dto.Manifest.Id, deserialized.Manifest.Id);
            Assert.Equal(dto.Manifest.Version, deserialized.Manifest.Version);
            Assert.Equal(dto.Manifest.ProductName, deserialized.Manifest.ProductName);
            Assert.Equal(dto.Manifest.Description, deserialized.Manifest.Description);
            Assert.Equal(dto.Manifest.PackageType, deserialized.Manifest.PackageType);
            Assert.Equal(dto.Manifest.UpdateType, deserialized.Manifest.UpdateType);
            Assert.Equal(dto.Manifest.RequiredVersion, deserialized.Manifest.RequiredVersion);
            Assert.Equal(dto.Manifest.MinimumClientVersion, deserialized.Manifest.MinimumClientVersion);
            Assert.Equal(dto.Manifest.Priority, deserialized.Manifest.Priority);
            Assert.Equal(dto.Manifest.Channel, deserialized.Manifest.Channel);
            Assert.Equal(dto.Manifest.SignatureMetadata, deserialized.Manifest.SignatureMetadata);
        }

        [Fact]
        public void UpdateStatusDto_ShouldSupportFullSerializationCycle()
        {
            // Arrange
            var dto = new UpdateStatusDto
            {
                CurrentState = UpdateState.Downloading,
                ProgressPercentage = 45.5,
                CurrentAction = "Fetching package chunk index 14...",
                ErrorMessage = "Network connection dropped transiently"
            };

            // Act
            string json = JsonSerializer.Serialize(dto);
            var deserialized = JsonSerializer.Deserialize<UpdateStatusDto>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(dto.CurrentState, deserialized.CurrentState);
            Assert.Equal(dto.ProgressPercentage, deserialized.ProgressPercentage);
            Assert.Equal(dto.CurrentAction, deserialized.CurrentAction);
            Assert.Equal(dto.ErrorMessage, deserialized.ErrorMessage);
        }

        [Fact]
        public void RollbackRequestDto_ShouldSupportFullSerializationCycle()
        {
            // Arrange
            var dto = new RollbackRequestDto
            {
                RollbackVersion = "1.9.5",
                Reason = "Anomalous workstation power cycles detected post deployment",
                Force = true
            };

            // Act
            string json = JsonSerializer.Serialize(dto);
            var deserialized = JsonSerializer.Deserialize<RollbackRequestDto>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(dto.RollbackVersion, deserialized.RollbackVersion);
            Assert.Equal(dto.Reason, deserialized.Reason);
            Assert.Equal(dto.Force, deserialized.Force);
        }

        [Fact]
        public void UpdateHistoryDto_ShouldSupportFullSerializationCycle()
        {
            // Arrange
            var dto = new UpdateHistoryDto
            {
                Id = Guid.NewGuid(),
                Version = "1.2.3-beta+build12",
                State = UpdateState.Completed,
                StartedAt = DateTime.UtcNow.AddMinutes(-5),
                CompletedAt = DateTime.UtcNow,
                ErrorMessage = null
            };

            // Act
            string json = JsonSerializer.Serialize(dto);
            var deserialized = JsonSerializer.Deserialize<UpdateHistoryDto>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(dto.Id, deserialized.Id);
            Assert.Equal(dto.Version, deserialized.Version);
            Assert.Equal(dto.State, deserialized.State);
            Assert.Equal(dto.ErrorMessage, deserialized.ErrorMessage);
        }

        #endregion

        #region Validation Layer Tests

        [Theory]
        [InlineData("1.0.0")]
        [InlineData("2.14.3")]
        [InlineData("10.0.103-alpha.1")]
        [InlineData("2.0.0-rc.3+build.123")]
        [InlineData("0.0.1")]
        public void VersionValidator_ShouldAllowValidSemVerStrings(string version)
        {
            // Arrange
            var validator = new VersionValidator();

            // Act & Assert
            Assert.True(validator.IsValid(version));
            validator.Validate(version); // Should not throw
        }

        [Theory]
        [InlineData("1")]
        [InlineData("1.0")]
        [InlineData("invalid-semver")]
        [InlineData("1.0.0.0")]
        [InlineData("a.b.c")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void VersionValidator_ShouldRejectInvalidVersionStrings(string? version)
        {
            // Arrange
            var validator = new VersionValidator();

            // Act & Assert
            Assert.False(validator.IsValid(version!));
            Assert.Throws<UpdateValidationException>(() => validator.Validate(version!));
        }

        [Fact]
        public void DependencyValidator_ShouldValidateDependenciesCorrectly()
        {
            // Arrange
            var versionValidator = new VersionValidator();
            var validator = new DependencyValidator(versionValidator);

            var validDep = new UpdateDependency
            {
                Name = "DotNetRuntime8",
                MinimumVersion = "8.0.0",
                Required = true
            };

            var invalidDepNoName = new UpdateDependency
            {
                Name = "",
                MinimumVersion = "8.0.0",
                Required = true
            };

            var invalidDepBadVersion = new UpdateDependency
            {
                Name = "VC++Redist",
                MinimumVersion = "invalid-version",
                Required = false
            };

            // Act & Assert
            validator.Validate(validDep); // Should not throw

            var ex1 = Assert.Throws<UpdateValidationException>(() => validator.Validate(invalidDepNoName));
            Assert.Contains("Name is required", ex1.Message);

            var ex2 = Assert.Throws<UpdateValidationException>(() => validator.Validate(invalidDepBadVersion));
            Assert.Contains("minimum version", ex2.Message);
        }

        [Fact]
        public void ManifestValidator_ShouldValidateManifestsCorrectly()
        {
            // Arrange
            var versionValidator = new VersionValidator();
            var validator = new ManifestValidator(versionValidator);

            var validManifest = new UpdateManifest
            {
                Id = Guid.NewGuid(),
                Version = "2.0.0",
                ProductName = "SAYRA Client",
                Description = "Critical security rollouts",
                PackageType = PackageType.FullPackage,
                UpdateType = UpdateType.Security,
                RequiredVersion = "",
                MinimumClientVersion = "1.0.0",
                ReleaseDate = DateTime.UtcNow,
                Priority = UpdatePriority.Critical,
                Channel = UpdateChannel.Stable,
                SignatureMetadata = "signedHashValueString"
            };

            var invalidManifestEmptyGuid = new UpdateManifest
            {
                Id = Guid.Empty,
                Version = "2.0.0",
                ProductName = "SAYRA Client",
                MinimumClientVersion = "1.0.0",
                ReleaseDate = DateTime.UtcNow
            };

            var invalidManifestNoName = new UpdateManifest
            {
                Id = Guid.NewGuid(),
                Version = "2.0.0",
                ProductName = " ",
                MinimumClientVersion = "1.0.0",
                ReleaseDate = DateTime.UtcNow
            };

            var invalidManifestBadVersion = new UpdateManifest
            {
                Id = Guid.NewGuid(),
                Version = "1.0",
                ProductName = "SAYRA Client",
                MinimumClientVersion = "1.0.0",
                ReleaseDate = DateTime.UtcNow
            };

            var invalidManifestBadMinVersion = new UpdateManifest
            {
                Id = Guid.NewGuid(),
                Version = "2.0.0",
                ProductName = "SAYRA Client",
                MinimumClientVersion = "bad-min-ver",
                ReleaseDate = DateTime.UtcNow
            };

            var invalidManifestDeltaNoRequiredVersion = new UpdateManifest
            {
                Id = Guid.NewGuid(),
                Version = "2.1.0",
                ProductName = "SAYRA Client",
                PackageType = PackageType.DeltaPackage,
                RequiredVersion = "bad-required-version",
                MinimumClientVersion = "1.0.0",
                ReleaseDate = DateTime.UtcNow
            };

            var invalidManifestDefaultDate = new UpdateManifest
            {
                Id = Guid.NewGuid(),
                Version = "2.0.0",
                ProductName = "SAYRA Client",
                MinimumClientVersion = "1.0.0",
                ReleaseDate = default
            };

            // Act & Assert
            validator.Validate(validManifest); // Should not throw

            Assert.Throws<UpdateValidationException>(() => validator.Validate(invalidManifestEmptyGuid));
            Assert.Throws<UpdateValidationException>(() => validator.Validate(invalidManifestNoName));
            Assert.Throws<UpdateValidationException>(() => validator.Validate(invalidManifestBadVersion));
            Assert.Throws<UpdateValidationException>(() => validator.Validate(invalidManifestBadMinVersion));
            Assert.Throws<UpdateValidationException>(() => validator.Validate(invalidManifestDeltaNoRequiredVersion));
            Assert.Throws<UpdateValidationException>(() => validator.Validate(invalidManifestDefaultDate));
        }

        [Fact]
        public void AggregateUpdateValidator_ShouldCoordinateValidations()
        {
            // Arrange
            var versionValidator = new VersionValidator();
            var dependencyValidator = new DependencyValidator(versionValidator);
            var manifestValidator = new ManifestValidator(versionValidator);
            var aggregateValidator = new UpdateValidator(manifestValidator, versionValidator, dependencyValidator);

            var validDep = new UpdateDependency { Name = "VC++", MinimumVersion = "14.0.0" };
            var invalidDep = new UpdateDependency { Name = "", MinimumVersion = "1.0.0" };

            // Act & Assert
            aggregateValidator.ValidateVersion("1.0.0");
            Assert.Throws<UpdateValidationException>(() => aggregateValidator.ValidateVersion("invalid-version"));

            aggregateValidator.ValidateDependency(validDep);
            Assert.Throws<UpdateValidationException>(() => aggregateValidator.ValidateDependency(invalidDep));
        }

        #endregion

        #region Options Configuration and Default Value Tests

        [Fact]
        public void UpdateOptions_ShouldHaveSensibleDefaultValues()
        {
            // Arrange & Act
            var options = new UpdateOptions();

            // Assert
            Assert.True(options.Enabled);
            Assert.Empty(options.UpdateServerUrl);
            Assert.Equal(180, options.CheckIntervalMinutes);
            Assert.False(options.AllowBetaChannel);
            Assert.True(options.AutoInstall);
            Assert.Empty(options.MaintenanceWindow);
        }

        [Fact]
        public void RollbackOptions_ShouldHaveSensibleDefaultValues()
        {
            // Arrange & Act
            var options = new RollbackOptions();

            // Assert
            Assert.True(options.Enabled);
            Assert.Equal(3, options.MaxRollbackVersions);
            Assert.Equal(30, options.SnapshotRetentionDays);
        }

        [Fact]
        public void DownloadOptions_ShouldHaveSensibleDefaultValues()
        {
            // Arrange & Act
            var options = new DownloadOptions();

            // Assert
            Assert.Equal(2, options.MaxParallelDownloads);
            Assert.Equal(10.0, options.MaxBandwidthMbps);
            Assert.Equal(3, options.RetryCount);
        }

        #endregion

        #region Exception Hierarchy Tests

        [Fact]
        public void UpdateExceptions_ShouldSetProperMessageAndInnerException()
        {
            // Arrange
            string msg = "Test Error context";
            var inner = new InvalidOperationException("Inner failure");

            // Act & Assert
            var exc = new UpdateException(msg, inner);
            Assert.Equal(msg, exc.Message);
            Assert.Same(inner, exc.InnerException);

            var validationExc = new UpdateValidationException(msg, inner);
            Assert.IsAssignableFrom<UpdateException>(validationExc);
            Assert.Equal(msg, validationExc.Message);
            Assert.Same(inner, validationExc.InnerException);

            var packageExc = new PackageException(msg, inner);
            Assert.IsAssignableFrom<UpdateException>(packageExc);
            Assert.Equal(msg, packageExc.Message);
            Assert.Same(inner, packageExc.InnerException);

            var installationExc = new InstallationException(msg, inner);
            Assert.IsAssignableFrom<UpdateException>(installationExc);
            Assert.Equal(msg, installationExc.Message);
            Assert.Same(inner, installationExc.InnerException);

            var rollbackExc = new RollbackException(msg, inner);
            Assert.IsAssignableFrom<UpdateException>(rollbackExc);
            Assert.Equal(msg, rollbackExc.Message);
            Assert.Same(inner, rollbackExc.InnerException);
        }

        #endregion
    }
}

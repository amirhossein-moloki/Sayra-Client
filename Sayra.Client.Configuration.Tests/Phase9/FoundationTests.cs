using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using FluentValidation;
using Sayra.Client.Shared.DependencyInjection;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Dtos;
using Sayra.Client.Shared.Models.Phase9.Enums;
using Sayra.Client.Shared.Models.Phase9.Mapping;
using Sayra.Client.Shared.Models.Phase9.Options;
using Sayra.Client.Shared.Models.Phase9.Validation;
using Xunit;

namespace Sayra.Client.Configuration.Tests.Phase9
{
    public class FoundationTests
    {
        [Fact]
        public void AddPhase9Foundation_Registers_Required_Options_And_Validators()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddPhase9Foundation();
            var serviceProvider = services.BuildServiceProvider();

            // Assert Options
            var fleetOptions = serviceProvider.GetService<IOptions<FleetOptions>>();
            Assert.NotNull(fleetOptions);
            Assert.Equal(30, fleetOptions.Value.SyncIntervalSeconds);

            var monitoringOptions = serviceProvider.GetService<IOptions<MonitoringOptions>>();
            Assert.NotNull(monitoringOptions);
            Assert.Equal(1000, monitoringOptions.Value.SamplingIntervalMs);

            var diagnosticsOptions = serviceProvider.GetService<IOptions<DiagnosticsOptions>>();
            Assert.NotNull(diagnosticsOptions);
            Assert.Equal("Optimal", diagnosticsOptions.Value.CompressionLevel);

            var transferOptions = serviceProvider.GetService<IOptions<TransferOptions>>();
            Assert.NotNull(transferOptions);
            Assert.Equal(65536, transferOptions.Value.DefaultChunkSizeBytes);

            var bulkOperationOptions = serviceProvider.GetService<IOptions<BulkOperationOptions>>();
            Assert.NotNull(bulkOperationOptions);
            Assert.Equal(50, bulkOperationOptions.Value.DefaultConcurrencyLimit);

            // Assert Validators
            var commandValidator = serviceProvider.GetService<IValidator<RemoteCommandRequest>>();
            Assert.NotNull(commandValidator);

            var queryValidator = serviceProvider.GetService<IValidator<MachineQueryRequest>>();
            Assert.NotNull(queryValidator);
        }

        [Fact]
        public void MachineInfo_Supports_Value_Equality_And_Immutability()
        {
            // Arrange
            var fixedDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var version1 = new MachineVersion { SemVer = "1.0.0", BuildHash = "abc", BuildDate = fixedDate };
            var version2 = new MachineVersion { SemVer = "1.0.0", BuildHash = "abc", BuildDate = fixedDate };

            // Assert Record Equality
            Assert.Equal(version1, version2);

            var inventory1 = new MachineInventory { CpuName = "Core i9", RamGb = 32 };
            var inventory2 = new MachineInventory { CpuName = "Core i9", RamGb = 32 };
            Assert.Equal(inventory1, inventory2);

            var machineId = "WS-99";
            var machine1 = new MachineInfo
            {
                MachineId = machineId,
                Hostname = "PC-99",
                IpAddress = "127.0.0.1",
                Status = MachineStatus.Online,
                Version = version1,
                Inventory = inventory1,
                LastSeenUtc = fixedDate
            };

            var machine2 = new MachineInfo
            {
                MachineId = machineId,
                Hostname = "PC-99",
                IpAddress = "127.0.0.1",
                Status = MachineStatus.Online,
                Version = version2,
                Inventory = inventory2,
                LastSeenUtc = fixedDate
            };

            // Act & Assert
            Assert.Equal(machine1, machine2);

            // Verify with-expression (Immutability check)
            var modifiedMachine = machine1 with { IpAddress = "192.168.1.50" };
            Assert.NotEqual(machine1, modifiedMachine);
            Assert.Equal("192.168.1.50", modifiedMachine.IpAddress);
            Assert.Equal("127.0.0.1", machine1.IpAddress); // Original unchanged
        }

        [Fact]
        public void Request_Validators_Properly_Detect_Invalid_Payloads()
        {
            // Arrange
            var validator = new RemoteCommandRequestValidator();
            var invalidRequest = new RemoteCommandRequest
            {
                MachineId = "", // Invalid
                Action = "", // Invalid
                Priority = "SuperHigh", // Invalid priority string
                Signature = "", // Invalid
                OperatorId = "" // Invalid
            };

            // Act
            var result = validator.Validate(invalidRequest);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.PropertyName == nameof(RemoteCommandRequest.MachineId));
            Assert.Contains(result.Errors, e => e.PropertyName == nameof(RemoteCommandRequest.Action));
            Assert.Contains(result.Errors, e => e.PropertyName == nameof(RemoteCommandRequest.Priority));
            Assert.Contains(result.Errors, e => e.PropertyName == nameof(RemoteCommandRequest.Signature));
            Assert.Contains(result.Errors, e => e.PropertyName == nameof(RemoteCommandRequest.OperatorId));
        }

        [Fact]
        public void Request_Validators_Accept_Valid_Payloads()
        {
            // Arrange
            var validator = new RemoteCommandRequestValidator();
            var validRequest = new RemoteCommandRequest
            {
                MachineId = "PC-01",
                Action = "UNLOCK_PC",
                Priority = "Critical",
                Signature = "ValidSignatureBytes",
                OperatorId = "Admin-01",
                Parameters = new Dictionary<string, string> { { "duration", "3600" } }
            };

            // Act
            var result = validator.Validate(validRequest);

            // Assert
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Mapper_Correctly_Maps_Between_DTO_And_Domain()
        {
            // Arrange
            var request = new RemoteCommandRequest
            {
                MachineId = "WS-77",
                Action = "LOCK",
                Priority = "High",
                Signature = "CryptographicSignatureStr",
                OperatorId = "Operator-99",
                Parameters = new Dictionary<string, string>
                {
                    { "sec_token", "SecretValue123" },
                    { "standard_param", "NormalValue" }
                }
            };

            // Act
            var domainCommand = request.MapToDomain();

            // Assert mapping
            Assert.NotNull(domainCommand);
            Assert.NotEmpty(domainCommand.CommandId);
            Assert.Equal("WS-77", domainCommand.TargetMachineId);
            Assert.Equal("LOCK", domainCommand.Action);
            Assert.Equal(CommandPriority.High, domainCommand.Priority);
            Assert.Equal("CryptographicSignatureStr", domainCommand.Signature);
            Assert.Equal("Operator-99", domainCommand.CreatorOperatorId);

            // Secure detection validation in mapper
            var secureParam = domainCommand.Parameters.Find(p => p.Name == "sec_token");
            Assert.NotNull(secureParam);
            Assert.True(secureParam.IsSecure);

            var normalParam = domainCommand.Parameters.Find(p => p.Name == "standard_param");
            Assert.NotNull(normalParam);
            Assert.False(normalParam.IsSecure);
        }

        [Fact]
        public void Models_Support_SystemTextJson_Serialization_And_Deserialization()
        {
            // Arrange
            var originalCommand = new Sayra.Client.Shared.Models.Phase9.Domain.RemoteCommand
            {
                CommandId = Guid.NewGuid().ToString(),
                Action = "RESTART",
                TargetMachineId = "WS-01",
                Priority = CommandPriority.Critical,
                Signature = "Signature_123",
                CreatorOperatorId = "Admin_99",
                ExpiresAtUtc = DateTime.UtcNow,
                Parameters = new List<CommandParameter>
                {
                    new CommandParameter { Name = "Delay", Value = "10", IsSecure = false }
                }
            };

            // Act
            var json = JsonSerializer.Serialize(originalCommand);
            var deserialized = JsonSerializer.Deserialize<Sayra.Client.Shared.Models.Phase9.Domain.RemoteCommand>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(originalCommand.CommandId, deserialized.CommandId);
            Assert.Equal(originalCommand.Action, deserialized.Action);
            Assert.Equal(originalCommand.TargetMachineId, deserialized.TargetMachineId);
            Assert.Equal(originalCommand.Priority, deserialized.Priority);
            Assert.Equal(originalCommand.Signature, deserialized.Signature);
            Assert.Equal(originalCommand.CreatorOperatorId, deserialized.CreatorOperatorId);
            Assert.Single(deserialized.Parameters);
            Assert.Equal("Delay", deserialized.Parameters[0].Name);
        }
    }
}

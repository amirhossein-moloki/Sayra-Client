using System;
using System.Collections.Generic;
using System.Linq;
using Sayra.Client.Shared.Models.Phase9.Dtos;
using Sayra.Client.Shared.Models.Phase9.Enums;

using DomainRemoteCommand = Sayra.Client.Shared.Models.Phase9.Domain.RemoteCommand;
using DomainCommandResult = Sayra.Client.Shared.Models.Phase9.Domain.CommandResult;
using DomainBulkOperation = Sayra.Client.Shared.Models.Phase9.Domain.BulkOperation;
using DomainBulkOperationResult = Sayra.Client.Shared.Models.Phase9.Domain.BulkOperationResult;
using DomainTransferJob = Sayra.Client.Shared.Models.Phase9.Domain.TransferJob;
using DomainTransferChunk = Sayra.Client.Shared.Models.Phase9.Domain.TransferChunk;
using DomainCommandParameter = Sayra.Client.Shared.Models.Phase9.Domain.CommandParameter;

namespace Sayra.Client.Shared.Models.Phase9.Mapping
{
    using CommandStatus = Sayra.Client.Shared.Models.Phase9.Enums.CommandStatus;
    /// <summary>
    /// Bidirectional mapper utility for mapping between Phase 9 Core Domain Models and Request/Response DTOs.
    /// This establishes clean mappings without depending on external mapping frameworks, preserving high performance and full JSON/MessagePack transparency.
    /// </summary>
    public static class Phase9Mapper
    {
        /// <summary>
        /// Maps a <see cref="RemoteCommandRequest"/> DTO to a <see cref="DomainRemoteCommand"/> Domain Model.
        /// </summary>
        public static DomainRemoteCommand MapToDomain(this RemoteCommandRequest dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            Enum.TryParse<CommandPriority>(dto.Priority, out var priority);

            return new DomainRemoteCommand
            {
                CommandId = Guid.NewGuid().ToString(),
                Action = dto.Action,
                TargetMachineId = dto.MachineId,
                Priority = priority,
                Signature = dto.Signature,
                CreatorOperatorId = dto.OperatorId,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
                Parameters = dto.Parameters?.Select(p => new DomainCommandParameter
                {
                    Name = p.Key,
                    Value = p.Value,
                    IsSecure = p.Key.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                               p.Key.Contains("token", StringComparison.OrdinalIgnoreCase) ||
                               p.Key.Contains("secret", StringComparison.OrdinalIgnoreCase)
                }).ToList() ?? new List<DomainCommandParameter>()
            };
        }

        /// <summary>
        /// Maps a <see cref="DomainRemoteCommand"/> Domain Model to a <see cref="RemoteCommandResponse"/> DTO.
        /// </summary>
        public static RemoteCommandResponse MapToResponse(this DomainRemoteCommand domain, CommandStatus status, OperationResult outcome, string message)
        {
            if (domain == null) throw new ArgumentNullException(nameof(domain));

            return new RemoteCommandResponse
            {
                CommandId = domain.CommandId,
                Status = status.ToString(),
                Outcome = outcome.ToString(),
                OutputMessage = message
            };
        }

        /// <summary>
        /// Maps a <see cref="BulkOperationRequest"/> DTO to a <see cref="DomainBulkOperation"/> Domain Model.
        /// </summary>
        public static DomainBulkOperation MapToDomain(this BulkOperationRequest dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            return new DomainBulkOperation
            {
                BulkOperationId = Guid.NewGuid().ToString(),
                Action = dto.Action,
                TargetMachineIds = dto.MachineIds ?? new List<string>(),
                Status = OperationStatus.Pending,
                OperatorId = dto.OperatorId,
                CreatedAtUtc = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Maps a <see cref="DomainBulkOperation"/> Domain Model to a <see cref="BulkOperationResponse"/> DTO.
        /// </summary>
        public static BulkOperationResponse MapToResponse(this DomainBulkOperation domain, Domain.BulkOperationProgress progress)
        {
            if (domain == null) throw new ArgumentNullException(nameof(domain));
            if (progress == null) throw new ArgumentNullException(nameof(progress));

            return new BulkOperationResponse
            {
                BulkOperationId = domain.BulkOperationId,
                Status = progress.ActiveStatus.ToString(),
                TotalTargets = progress.TotalTargets,
                SucceededCount = progress.SucceededCount,
                FailedCount = progress.FailedCount
            };
        }

        /// <summary>
        /// Maps a <see cref="TransferRequest"/> DTO to a <see cref="DomainTransferJob"/> Domain Model.
        /// </summary>
        public static DomainTransferJob MapToDomain(this TransferRequest dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            Enum.TryParse<TransferDirection>(dto.Direction, out var direction);
            Enum.TryParse<TransferType>(dto.Category, out var category);

            return new DomainTransferJob
            {
                JobId = Guid.NewGuid().ToString(),
                FilePath = dto.FilePath,
                Direction = direction,
                Category = category,
                Status = TransferStatus.Pending,
                TotalFileSizeBytes = dto.TotalFileSizeBytes,
                StartedAtUtc = DateTime.UtcNow,
                Chunks = new List<DomainTransferChunk>()
            };
        }

        /// <summary>
        /// Maps a <see cref="DomainTransferJob"/> Domain Model to a <see cref="TransferResponse"/> DTO.
        /// </summary>
        public static TransferResponse MapToResponse(this DomainTransferJob domain)
        {
            if (domain == null) throw new ArgumentNullException(nameof(domain));

            return new TransferResponse
            {
                JobId = domain.JobId,
                Status = domain.Status.ToString(),
                TotalChunks = domain.Chunks?.Count ?? 0,
                CompletedChunks = domain.Status == TransferStatus.Completed ? (domain.Chunks?.Count ?? 0) : 0
            };
        }
    }
}

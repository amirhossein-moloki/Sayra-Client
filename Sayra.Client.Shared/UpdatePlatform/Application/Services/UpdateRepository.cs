using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Enums;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Bridges the existing legacy IUpdateRepository interface to the new SQLCipher-backed IUpdateHistoryRepository.
    /// </summary>
    public class UpdateRepository : IUpdateRepository
    {
        private readonly IUpdateHistoryRepository _historyRepository;

        public UpdateRepository(IUpdateHistoryRepository historyRepository)
        {
            _historyRepository = historyRepository ?? throw new ArgumentNullException(nameof(historyRepository));
        }

        public async Task SaveAsync(UpdateHistoryEntry entry, CancellationToken cancellationToken = default)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            var existing = await _historyRepository.GetByIdAsync(entry.Id, cancellationToken);
            if (existing != null)
            {
                // Update
                existing.Version = entry.Version;
                existing.Status = entry.State.ToString().ToUpper();
                existing.InstallationTime = entry.StartedAt;
                existing.CompletionTime = entry.CompletedAt;
                existing.ErrorCode = entry.ErrorMessage ?? string.Empty;
                if (entry.CompletedAt.HasValue)
                {
                    existing.Duration = entry.CompletedAt.Value - entry.StartedAt;
                }
                await _historyRepository.UpdateAsync(existing, cancellationToken);
            }
            else
            {
                // Insert
                var record = new UpdateHistoryRecord
                {
                    Id = entry.Id,
                    PackageId = Guid.Empty,
                    Version = entry.Version,
                    PreviousVersion = "UNKNOWN",
                    InstallationTime = entry.StartedAt,
                    CompletionTime = entry.CompletedAt,
                    Status = entry.State.ToString().ToUpper(),
                    ErrorCode = entry.ErrorMessage ?? string.Empty,
                    Result = entry.State == UpdateState.Completed ? "SUCCESS" : "IN_PROGRESS",
                    DeviceIdentifier = "WORKSTATION",
                    Duration = entry.CompletedAt.HasValue ? (entry.CompletedAt.Value - entry.StartedAt) : TimeSpan.Zero
                };
                await _historyRepository.InsertAsync(record, cancellationToken);
            }
        }

        public async Task<IEnumerable<UpdateHistoryEntry>> GetHistoryAsync(CancellationToken cancellationToken = default)
        {
            var records = await _historyRepository.GetAllAsync(cancellationToken);
            return records.Select(MapToEntry).ToList();
        }

        public async Task<UpdateHistoryEntry?> GetLatestAsync(CancellationToken cancellationToken = default)
        {
            var record = await _historyRepository.GetLatestAsync(cancellationToken);
            return record != null ? MapToEntry(record) : null;
        }

        private static UpdateHistoryEntry MapToEntry(UpdateHistoryRecord record)
        {
            UpdateState state = UpdateState.Idle;
            if (Enum.TryParse<UpdateState>(record.Status, true, out var parsedState))
            {
                state = parsedState;
            }
            else if (record.Status == "STAGED")
            {
                state = UpdateState.Available;
            }
            else if (record.Status == "COMPLETED")
            {
                state = UpdateState.Completed;
            }
            else if (record.Status == "FAILED")
            {
                state = UpdateState.Failed;
            }
            else if (record.Status == "ROLLED_BACK")
            {
                state = UpdateState.RolledBack;
            }

            return new UpdateHistoryEntry
            {
                Id = record.Id,
                Version = record.Version,
                State = state,
                StartedAt = record.InstallationTime,
                CompletedAt = record.CompletionTime,
                ErrorMessage = string.IsNullOrEmpty(record.ErrorCode) ? null : record.ErrorCode
            };
        }
    }
}

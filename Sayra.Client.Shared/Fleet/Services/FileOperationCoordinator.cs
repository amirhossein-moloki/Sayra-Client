using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Fleet.Queues;
using Sayra.Client.Shared.Fleet.Security;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Fleet.Services
{
    /// <summary>
    /// Coordinates lower-level secure file system operations with authorization and validation.
    /// </summary>
    public interface IFileOperationCoordinator
    {
        /// <summary>
        /// Initiates a secure file upload transfer.
        /// </summary>
        Task<string> UploadFileAsync(string operatorId, string sourcePath, string targetPath, CancellationToken ct = default);

        /// <summary>
        /// Initiates a secure file download transfer.
        /// </summary>
        Task<string> DownloadFileAsync(string operatorId, string sourcePath, string targetPath, CancellationToken ct = default);

        /// <summary>
        /// Securely deletes a file.
        /// </summary>
        Task<bool> DeleteFileAsync(string operatorId, string filePath, CancellationToken ct = default);

        /// <summary>
        /// Securely moves a file.
        /// </summary>
        Task<bool> MoveFileAsync(string operatorId, string sourcePath, string targetPath, CancellationToken ct = default);

        /// <summary>
        /// Securely copies a file.
        /// </summary>
        Task<bool> CopyFileAsync(string operatorId, string sourcePath, string targetPath, CancellationToken ct = default);

        /// <summary>
        /// Securely renames a file.
        /// </summary>
        Task<bool> RenameFileAsync(string operatorId, string sourcePath, string newName, CancellationToken ct = default);

        /// <summary>
        /// Securely creates a directory.
        /// </summary>
        Task<bool> CreateDirectoryAsync(string operatorId, string dirPath, CancellationToken ct = default);

        /// <summary>
        /// Securely deletes a directory.
        /// </summary>
        Task<bool> DeleteDirectoryAsync(string operatorId, string dirPath, bool recursive, CancellationToken ct = default);

        /// <summary>
        /// Securely lists a directory.
        /// </summary>
        Task<DirectoryEntry> ListDirectoryAsync(string operatorId, string dirPath, CancellationToken ct = default);

        /// <summary>
        /// Securely gets file metadata.
        /// </summary>
        Task<FileMetadata> GetFileMetadataAsync(string operatorId, string filePath, CancellationToken ct = default);
    }

    /// <summary>
    /// Coordinates high-reliability file system operations, validation, authorization, and audit logs.
    /// </summary>
    public class FileOperationCoordinator : IFileOperationCoordinator
    {
        private readonly ISecurePathValidator _pathValidator;
        private readonly IFileAuthorizationService _authService;
        private readonly ITransferQueue _transferQueue;
        private readonly IChecksumService _checksumService;
        private readonly ILogger<FileOperationCoordinator> _logger;

        /// <summary>
        /// Initializes a new instance of FileOperationCoordinator.
        /// </summary>
        public FileOperationCoordinator(
            ISecurePathValidator pathValidator,
            IFileAuthorizationService authService,
            ITransferQueue transferQueue,
            IChecksumService checksumService,
            ILogger<FileOperationCoordinator> logger)
        {
            _pathValidator = pathValidator ?? throw new ArgumentNullException(nameof(pathValidator));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _transferQueue = transferQueue ?? throw new ArgumentNullException(nameof(transferQueue));
            _checksumService = checksumService ?? throw new ArgumentNullException(nameof(checksumService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Initiates a secure file upload.
        /// </summary>
        public async Task<string> UploadFileAsync(string operatorId, string sourcePath, string targetPath, CancellationToken ct = default)
        {
            // Set up 15-second operation timeout limit for validation & queuing
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(15));

            string validatedPath = _pathValidator.ValidateAndCanonicalize(targetPath);
            await _authService.ValidateAndAuditAsync(operatorId, validatedPath, "Upload", FilePermissionScope.Write, cts.Token).ConfigureAwait(false);

            var jobId = Guid.NewGuid().ToString();
            var job = new TransferJob
            {
                JobId = jobId,
                FilePath = validatedPath,
                Direction = TransferDirection.Upload,
                Category = TransferType.File,
                Status = TransferStatus.Pending,
                TotalFileSizeBytes = 0, // Will be computed or updated during run
                FullFileIntegrityHash = string.Empty
            };

            bool enqueued = await _transferQueue.EnqueueAsync(job, cts.Token).ConfigureAwait(false);
            if (!enqueued)
            {
                throw new InvalidOperationException("Upload job cannot be enqueued. It might be a duplicate active transfer.");
            }

            return jobId;
        }

        /// <summary>
        /// Initiates a secure file download.
        /// </summary>
        public async Task<string> DownloadFileAsync(string operatorId, string sourcePath, string targetPath, CancellationToken ct = default)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(15));

            string validatedPath = _pathValidator.ValidateAndCanonicalize(sourcePath);
            await _authService.ValidateAndAuditAsync(operatorId, validatedPath, "Download", FilePermissionScope.Read, cts.Token).ConfigureAwait(false);

            var jobId = Guid.NewGuid().ToString();
            var job = new TransferJob
            {
                JobId = jobId,
                FilePath = validatedPath,
                Direction = TransferDirection.Download,
                Category = TransferType.File,
                Status = TransferStatus.Pending,
                TotalFileSizeBytes = File.Exists(validatedPath) ? new FileInfo(validatedPath).Length : 0,
                FullFileIntegrityHash = string.Empty
            };

            bool enqueued = await _transferQueue.EnqueueAsync(job, cts.Token).ConfigureAwait(false);
            if (!enqueued)
            {
                throw new InvalidOperationException("Download job cannot be enqueued. It might be a duplicate active transfer.");
            }

            return jobId;
        }

        /// <summary>
        /// Securely deletes a file.
        /// </summary>
        public async Task<bool> DeleteFileAsync(string operatorId, string filePath, CancellationToken ct = default)
        {
            string validatedPath = _pathValidator.ValidateAndCanonicalize(filePath);
            await _authService.ValidateAndAuditAsync(operatorId, validatedPath, "DeleteFile", FilePermissionScope.Delete, ct).ConfigureAwait(false);

            if (!File.Exists(validatedPath))
            {
                return false;
            }

            File.Delete(validatedPath);
            return true;
        }

        /// <summary>
        /// Securely moves a file from source to destination.
        /// </summary>
        public async Task<bool> MoveFileAsync(string operatorId, string sourcePath, string targetPath, CancellationToken ct = default)
        {
            string valSource = _pathValidator.ValidateAndCanonicalize(sourcePath);
            string valTarget = _pathValidator.ValidateAndCanonicalize(targetPath);

            await _authService.ValidateAndAuditAsync(operatorId, valSource, "MoveFile-Source", FilePermissionScope.Delete, ct).ConfigureAwait(false);
            await _authService.ValidateAndAuditAsync(operatorId, valTarget, "MoveFile-Target", FilePermissionScope.Write, ct).ConfigureAwait(false);

            if (!File.Exists(valSource))
            {
                return false;
            }

            var dir = Path.GetDirectoryName(valTarget);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (File.Exists(valTarget))
            {
                File.Delete(valTarget);
            }

            File.Move(valSource, valTarget);
            return true;
        }

        /// <summary>
        /// Securely copies a file from source to destination.
        /// </summary>
        public async Task<bool> CopyFileAsync(string operatorId, string sourcePath, string targetPath, CancellationToken ct = default)
        {
            string valSource = _pathValidator.ValidateAndCanonicalize(sourcePath);
            string valTarget = _pathValidator.ValidateAndCanonicalize(targetPath);

            await _authService.ValidateAndAuditAsync(operatorId, valSource, "CopyFile-Source", FilePermissionScope.Read, ct).ConfigureAwait(false);
            await _authService.ValidateAndAuditAsync(operatorId, valTarget, "CopyFile-Target", FilePermissionScope.Write, ct).ConfigureAwait(false);

            if (!File.Exists(valSource))
            {
                return false;
            }

            var dir = Path.GetDirectoryName(valTarget);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.Copy(valSource, valTarget, overwrite: true);
            return true;
        }

        /// <summary>
        /// Securely renames a file.
        /// </summary>
        public async Task<bool> RenameFileAsync(string operatorId, string sourcePath, string newName, CancellationToken ct = default)
        {
            string valSource = _pathValidator.ValidateAndCanonicalize(sourcePath);
            if (string.IsNullOrWhiteSpace(newName) || newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException("Invalid destination file name.", nameof(newName));
            }

            string valTarget = Path.Combine(Path.GetDirectoryName(valSource) ?? string.Empty, newName);
            valTarget = _pathValidator.ValidateAndCanonicalize(valTarget);

            await _authService.ValidateAndAuditAsync(operatorId, valSource, "RenameFile-Source", FilePermissionScope.Delete, ct).ConfigureAwait(false);
            await _authService.ValidateAndAuditAsync(operatorId, valTarget, "RenameFile-Target", FilePermissionScope.Write, ct).ConfigureAwait(false);

            if (!File.Exists(valSource))
            {
                return false;
            }

            if (File.Exists(valTarget))
            {
                File.Delete(valTarget);
            }

            File.Move(valSource, valTarget);
            return true;
        }

        /// <summary>
        /// Securely creates a directory.
        /// </summary>
        public async Task<bool> CreateDirectoryAsync(string operatorId, string dirPath, CancellationToken ct = default)
        {
            string valPath = _pathValidator.ValidateAndCanonicalize(dirPath);
            await _authService.ValidateAndAuditAsync(operatorId, valPath, "CreateDirectory", FilePermissionScope.Write, ct).ConfigureAwait(false);

            if (Directory.Exists(valPath))
            {
                return false;
            }

            Directory.CreateDirectory(valPath);
            return true;
        }

        /// <summary>
        /// Securely deletes a directory.
        /// </summary>
        public async Task<bool> DeleteDirectoryAsync(string operatorId, string dirPath, bool recursive, CancellationToken ct = default)
        {
            string valPath = _pathValidator.ValidateAndCanonicalize(dirPath);
            await _authService.ValidateAndAuditAsync(operatorId, valPath, "DeleteDirectory", FilePermissionScope.Delete, ct).ConfigureAwait(false);

            if (!Directory.Exists(valPath))
            {
                return false;
            }

            Directory.Delete(valPath, recursive);
            return true;
        }

        /// <summary>
        /// Retrieves directory listings.
        /// </summary>
        public async Task<DirectoryEntry> ListDirectoryAsync(string operatorId, string dirPath, CancellationToken ct = default)
        {
            string valPath = _pathValidator.ValidateAndCanonicalize(dirPath);
            await _authService.ValidateAndAuditAsync(operatorId, valPath, "ListDirectory", FilePermissionScope.Read, ct).ConfigureAwait(false);

            if (!Directory.Exists(valPath))
            {
                throw new DirectoryNotFoundException($"Target directory not found: {dirPath}");
            }

            var dirInfo = new DirectoryInfo(valPath);

            var subDirs = dirInfo.GetDirectories()
                .Where(d => (d.Attributes & FileAttributes.ReparsePoint) != FileAttributes.ReparsePoint) // Security Check: ignore symlinks!
                .Select(d => new DirectoryEntry
                {
                    Name = d.Name,
                    FullPath = d.FullName,
                    LastWriteTimeUtc = d.LastWriteTimeUtc,
                    SubDirectories = new List<DirectoryEntry>(),
                    Files = new List<FileEntry>()
                }).ToList();

            var files = dirInfo.GetFiles()
                .Where(f => (f.Attributes & FileAttributes.ReparsePoint) != FileAttributes.ReparsePoint) // Security Check: ignore symlinks!
                .Select(f => new FileEntry
                {
                    Name = f.Name,
                    FullPath = f.FullName,
                    SizeBytes = f.Length,
                    LastWriteTimeUtc = f.LastWriteTimeUtc,
                    IsReadOnly = f.IsReadOnly
                }).ToList();

            return new DirectoryEntry
            {
                Name = dirInfo.Name,
                FullPath = dirInfo.FullName,
                LastWriteTimeUtc = dirInfo.LastWriteTimeUtc,
                SubDirectories = subDirs,
                Files = files
            };
        }

        /// <summary>
        /// Securely retrieves file metadata including cryptographic SHA-256 integrity hash.
        /// </summary>
        public async Task<FileMetadata> GetFileMetadataAsync(string operatorId, string filePath, CancellationToken ct = default)
        {
            string valPath = _pathValidator.ValidateAndCanonicalize(filePath);
            await _authService.ValidateAndAuditAsync(operatorId, valPath, "GetFileMetadata", FilePermissionScope.Read, ct).ConfigureAwait(false);

            if (!File.Exists(valPath))
            {
                throw new FileNotFoundException($"Target file not found: {filePath}");
            }

            var fileInfo = new FileInfo(valPath);
            string hashValue = await _checksumService.CalculateHashAsync(valPath, "SHA256", ct).ConfigureAwait(false);

            var attributes = new Dictionary<string, string>
            {
                { "Attributes", fileInfo.Attributes.ToString() },
                { "Extension", fileInfo.Extension }
            };

            return new FileMetadata
            {
                Name = fileInfo.Name,
                FullPath = fileInfo.FullName,
                SizeBytes = fileInfo.Length,
                CreatedAtUtc = fileInfo.CreationTimeUtc,
                LastWriteTimeUtc = fileInfo.LastWriteTimeUtc,
                LastAccessTimeUtc = fileInfo.LastAccessTimeUtc,
                ChecksumSha256 = hashValue,
                IsReadOnly = fileInfo.IsReadOnly,
                Attributes = attributes
            };
        }
    }
}

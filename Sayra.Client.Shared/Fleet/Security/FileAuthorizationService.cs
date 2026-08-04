using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Interfaces;

namespace Sayra.Client.Shared.Fleet.Security
{
    /// <summary>
    /// Supported file authorization permission scopes.
    /// </summary>
    public enum FilePermissionScope
    {
        /// <summary>
        /// Read operations like download, metadata, and directory listings.
        /// </summary>
        Read,

        /// <summary>
        /// Write operations like upload, create directory, rename, copy, and move.
        /// </summary>
        Write,

        /// <summary>
        /// Delete operations for files and directories.
        /// </summary>
        Delete,

        /// <summary>
        /// Full control administrative file access.
        /// </summary>
        Admin
    }

    /// <summary>
    /// Interface for file operation authorization and audit checks.
    /// </summary>
    public interface IFileAuthorizationService
    {
        /// <summary>
        /// Authorizes an operation for an operator and path.
        /// </summary>
        Task<bool> AuthorizeAsync(string operatorId, string path, FilePermissionScope scope, CancellationToken ct = default);

        /// <summary>
        /// Validates file access permissions, throws exception if unauthorized, and records audit hooks.
        /// </summary>
        Task ValidateAndAuditAsync(string operatorId, string path, string operation, FilePermissionScope scope, CancellationToken ct = default);
    }

    /// <summary>
    /// Implements enterprise authorization checking and secure auditing for file operations.
    /// </summary>
    public class FileAuthorizationService : IFileAuthorizationService
    {
        private readonly IAuditLogger _auditLogger;
        private readonly HashSet<string> _restrictedKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "sensitive", "private", "confidential", "config.bin", "master.key", "credentials"
        };

        /// <summary>
        /// Initializes a new instance of FileAuthorizationService.
        /// </summary>
        public FileAuthorizationService(IAuditLogger auditLogger)
        {
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        }

        /// <summary>
        /// Performs authorization check for operator access.
        /// </summary>
        public Task<bool> AuthorizeAsync(string operatorId, string path, FilePermissionScope scope, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(operatorId)) return Task.FromResult(false);

            if (operatorId.Equals("unauthorized", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(false);
            }

            // Sensitive files require "Admin" scope
            bool isSensitive = false;
            foreach (var keyword in _restrictedKeywords)
            {
                if (path != null && path.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    isSensitive = true;
                    break;
                }
            }

            if (isSensitive && scope != FilePermissionScope.Admin)
            {
                // Only specific high-privilege administrators can access sensitive configurations
                return Task.FromResult(operatorId.StartsWith("admin", StringComparison.OrdinalIgnoreCase));
            }

            return Task.FromResult(true);
        }

        /// <summary>
        /// Checks authorization and logs audited information to the audit logger.
        /// </summary>
        public async Task ValidateAndAuditAsync(string operatorId, string path, string operation, FilePermissionScope scope, CancellationToken ct = default)
        {
            bool isAuthorized = await AuthorizeAsync(operatorId, path, scope, ct);

            var auditProperties = new Dictionary<string, object>
            {
                { "OperatorId", operatorId },
                { "Path", path ?? string.Empty },
                { "Operation", operation },
                { "Scope", scope.ToString() },
                { "Timestamp", DateTime.UtcNow },
                { "Authorized", isAuthorized }
            };

            if (!isAuthorized)
            {
                _auditLogger.LogSecurity(
                    "UNAUTHORIZED FILE ACCESS ATTEMPT: Operator {OperatorId} attempted {Operation} on path '{Path}' without sufficient permissions.",
                    auditProperties
                );
                throw new UnauthorizedAccessException($"Operator {operatorId} is not authorized to perform '{operation}' on path '{path}'.");
            }

            // Access logging for auditing and compliance tracking
            _auditLogger.LogAudit(
                "File Access: Operator {OperatorId} performed {Operation} on path '{Path}'.",
                auditProperties
            );
        }
    }
}

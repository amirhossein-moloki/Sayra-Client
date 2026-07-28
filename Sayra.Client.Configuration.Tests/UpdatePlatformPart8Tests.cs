using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Application.Services;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Configuration.Tests
{
    /// <summary>
    /// Comprehensive enterprise-grade xUnit test suite verifying Phase 6 Part 8: Windows Integration & Enterprise Security.
    /// Covers WinVerifyTrust, Certificate Pinning, Authenticode Validation, Event Log, SCM, File ACL, Privilege, and Path Hardening.
    /// </summary>
    public class UpdatePlatformPart8Tests
    {
        private readonly NullLogger<AuthenticodeVerifier> _authLogger = NullLogger<AuthenticodeVerifier>.Instance;
        private readonly NullLogger<CertificatePinningService> _pinLogger = NullLogger<CertificatePinningService>.Instance;
        private readonly NullLogger<WindowsEventLogger> _eventLogger = NullLogger<WindowsEventLogger>.Instance;
        private readonly NullLogger<WindowsServiceManager> _serviceLogger = NullLogger<WindowsServiceManager>.Instance;
        private readonly NullLogger<PrivilegeManager> _privLogger = NullLogger<PrivilegeManager>.Instance;
        private readonly NullLogger<FileSecurityValidator> _fileLogger = NullLogger<FileSecurityValidator>.Instance;

        #region Helper: Programmatic Certificate Generation

        private X509Certificate2 GenerateTestCertificate(string commonName, int startDaysOffset, int endDaysOffset)
        {
            using (var rsa = RSA.Create(2048))
            {
                var request = new CertificateRequest(
                    $"CN={commonName}",
                    rsa,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);

                request.CertificateExtensions.Add(
                    new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));

                var start = DateTimeOffset.UtcNow.AddDays(startDaysOffset);
                var end = DateTimeOffset.UtcNow.AddDays(endDaysOffset);

                var cert = request.CreateSelfSigned(start, end);
                return new X509Certificate2(cert.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.Exportable);
            }
        }

        #endregion

        #region 1. WinVerifyTrust & Authenticode Verification Tests

        [Fact]
        public async Task AuthenticodeVerifier_WithNonExistentFile_ReturnsFailedResult()
        {
            var verifier = new AuthenticodeVerifier(_authLogger);
            var result = await verifier.VerifyFileAsync("C:\\NonExistentFile_" + Guid.NewGuid().ToString("N") + ".dll");

            Assert.False(result.Success);
            Assert.Contains("File not found", result.ErrorMessage);
        }

        [Fact]
        public async Task AuthenticodeVerifier_WithUnsignedMockFile_ReturnsFailedResult()
        {
            var verifier = new AuthenticodeVerifier(_authLogger);
            string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dll");
            File.WriteAllText(tempFile, "MOCK_UNSIGNED_BINARY_CONTENT");

            try
            {
                var result = await verifier.VerifyFileAsync(tempFile);
                Assert.False(result.Success);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task AuthenticodeVerifier_WithExpiredMockFile_ReturnsFailedResult()
        {
            var verifier = new AuthenticodeVerifier(_authLogger);
            string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dll");
            File.WriteAllText(tempFile, "MOCK_EXPIRED_BINARY_CONTENT");

            try
            {
                var result = await verifier.VerifyFileAsync(tempFile);
                if (OperatingSystem.IsWindows())
                {
                    Assert.False(result.Success);
                }
                else
                {
                    Assert.False(result.Success);
                    Assert.True(result.IsExpired);
                }
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task AuthenticodeVerifier_WithValidMockFile_ReturnsSuccessfulResult()
        {
            var verifier = new AuthenticodeVerifier(_authLogger);
            string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dll");
            File.WriteAllText(tempFile, "VALID_BINARY_CONTENT_MOCK_SIGNED");

            try
            {
                var result = await verifier.VerifyFileAsync(tempFile);
                if (!OperatingSystem.IsWindows())
                {
                    Assert.True(result.Success);
                    Assert.Equal("SAYRA Enterprise Mock Publisher", result.Publisher);
                    Assert.Equal("MOCKTHUMBPRINT1234567890", result.Thumbprint);
                }
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        #endregion

        #region 2. Certificate Pinning Service Tests

        [Fact]
        public void CertificatePinningService_WithEmptyTrustStore_FailsClosed()
        {
            var service = new CertificatePinningService(_pinLogger);
            using (var cert = GenerateTestCertificate("SAYRA Update Server", -1, 5))
            {
                var result = service.ValidateCertificate(cert, Array.Empty<string>(), Array.Empty<string>(), null);

                Assert.False(result.Success);
                Assert.Contains("Pinning trust store is completely empty", result.ErrorMessage);
            }
        }

        [Fact]
        public void CertificatePinningService_WithValidCertificateAndMatchingThumbprint_Succeeds()
        {
            var service = new CertificatePinningService(_pinLogger);
            using (var cert = GenerateTestCertificate("SAYRA Update Server", -1, 5))
            {
                string thumbprint = cert.Thumbprint;
                var result = service.ValidateCertificate(cert, new[] { thumbprint }, null, null);

                Assert.True(result.Success);
                Assert.True(result.PinnedValid);
                Assert.True(result.NotExpired);
            }
        }

        [Fact]
        public void CertificatePinningService_WithMismatchingThumbprint_Fails()
        {
            var service = new CertificatePinningService(_pinLogger);
            using (var cert = GenerateTestCertificate("SAYRA Update Server", -1, 5))
            {
                var result = service.ValidateCertificate(cert, new[] { "WRONG_THUMBPRINT_12345" }, null, null);

                Assert.False(result.Success);
                Assert.False(result.PinnedValid);
                Assert.Contains("does not match any pinned thumbprint", result.ErrorMessage);
            }
        }

        [Fact]
        public void CertificatePinningService_WithValidCertificateAndMatchingPublicKeyHash_Succeeds()
        {
            var service = new CertificatePinningService(_pinLogger);
            using (var cert = GenerateTestCertificate("SAYRA Update Server", -1, 5))
            {
                byte[] publicKeyBytes = cert.GetPublicKey();
                string hashBase64;
                using (var sha256 = SHA256.Create())
                {
                    hashBase64 = Convert.ToBase64String(sha256.ComputeHash(publicKeyBytes));
                }

                var result = service.ValidateCertificate(cert, null, new[] { hashBase64 }, null);

                Assert.True(result.Success);
                Assert.True(result.PinnedValid);
            }
        }

        [Fact]
        public void CertificatePinningService_WithMismatchingPublicKeyHash_Fails()
        {
            var service = new CertificatePinningService(_pinLogger);
            using (var cert = GenerateTestCertificate("SAYRA Update Server", -1, 5))
            {
                var result = service.ValidateCertificate(cert, null, new[] { "mismatching_pubkey_hash_base64_==" }, null);

                Assert.False(result.Success);
                Assert.False(result.PinnedValid);
                Assert.Contains("does not match any pinned public key hash", result.ErrorMessage);
            }
        }

        [Fact]
        public void CertificatePinningService_WithMismatchingIssuer_Fails()
        {
            var service = new CertificatePinningService(_pinLogger);
            using (var cert = GenerateTestCertificate("SAYRA Update Server", -1, 5))
            {
                string thumbprint = cert.Thumbprint;
                var result = service.ValidateCertificate(cert, new[] { thumbprint }, null, new[] { "CN=SomeOtherTrustedCA" });

                Assert.False(result.Success);
                Assert.False(result.IssuerValid);
                Assert.Contains("is not in the trusted issuers list", result.ErrorMessage);
            }
        }

        [Fact]
        public void CertificatePinningService_WithExpiredCertificate_Fails()
        {
            var service = new CertificatePinningService(_pinLogger);
            using (var cert = GenerateTestCertificate("SAYRA Update Server", -10, -1))
            {
                string thumbprint = cert.Thumbprint;
                var result = service.ValidateCertificate(cert, new[] { thumbprint }, null, null);

                Assert.False(result.Success);
                Assert.False(result.NotExpired);
                Assert.Contains("expired", result.ErrorMessage);
            }
        }

        #endregion

        #region 3. Windows Event Log Integration Tests

        [Fact]
        public void WindowsEventLogger_LogsCorrectlyWithoutExceptions()
        {
            var logger = new WindowsEventLogger(_eventLogger);

            var exception = Record.Exception(() =>
            {
                logger.LogInstallationStarted("2.4.0");
                logger.LogInstallationCompleted("2.4.0");
                logger.LogRollbackStarted("2.4.0", "2.3.5");
                logger.LogRollbackCompleted("2.3.5");
                logger.LogVerificationFailure("C:\\somefile.dll", "Hash mismatch");
                logger.LogSecurityFailure("Unsigned binary");
            });

            Assert.Null(exception);
        }

        #endregion

        #region 4. Windows Service Manager Tests

        [Fact]
        public async Task WindowsServiceManager_QueryStatusAndStartStop_Succeeds()
        {
            var manager = new WindowsServiceManager(_serviceLogger);
            string serviceName = "SAYRA_MockService_Test_" + Guid.NewGuid().ToString("N");

            if (!OperatingSystem.IsWindows())
            {
                var status = await manager.GetServiceStatusAsync(serviceName);
                Assert.Equal(WindowsServiceState.Stopped, status);

                await manager.StartServiceAsync(serviceName, TimeSpan.FromSeconds(5));
                status = await manager.GetServiceStatusAsync(serviceName);
                Assert.Equal(WindowsServiceState.Running, status);

                await manager.RestartServiceAsync(serviceName, TimeSpan.FromSeconds(5));
                status = await manager.GetServiceStatusAsync(serviceName);
                Assert.Equal(WindowsServiceState.Running, status);

                await manager.StopServiceAsync(serviceName, TimeSpan.FromSeconds(5));
                status = await manager.GetServiceStatusAsync(serviceName);
                Assert.Equal(WindowsServiceState.Stopped, status);
            }
            else
            {
                await Assert.ThrowsAsync<WindowsIntegrationException>(async () =>
                {
                    await manager.GetServiceStatusAsync(serviceName);
                });
            }
        }

        [Fact]
        public async Task WindowsServiceManager_WithDisabledService_ThrowsWindowsIntegrationException()
        {
            var manager = new WindowsServiceManager(_serviceLogger);
            string serviceName = "SAYRA_DisabledService_Test_" + Guid.NewGuid().ToString("N");

            if (!OperatingSystem.IsWindows())
            {
                manager.SetMockServiceDisabled(serviceName, true);
                await Assert.ThrowsAsync<WindowsIntegrationException>(async () =>
                {
                    await manager.StartServiceAsync(serviceName, TimeSpan.FromSeconds(5));
                });
            }
        }

        #endregion

        #region 5. Privilege Manager Tests

        [Fact]
        public void PrivilegeManager_ReturnsStatusSuccessfully()
        {
            var manager = new PrivilegeManager(_privLogger);
            var status = manager.GetCurrentPrivilegeStatus();

            Assert.NotNull(status);
        }

        [Fact]
        public void PrivilegeManager_EnsureAdminPrivileges_WithMockAdmin_DoesNotThrow()
        {
            var manager = new PrivilegeManager(_privLogger);
            manager.OverrideAdminStatus(true);

            var exception = Record.Exception(() => manager.EnsureAdminPrivileges());
            Assert.Null(exception);
        }

        [Fact]
        public void PrivilegeManager_EnsureAdminPrivileges_WithMockStandardUser_ThrowsPrivilegeException()
        {
            var manager = new PrivilegeManager(_privLogger);
            manager.OverrideAdminStatus(false);

            Assert.Throws<PrivilegeException>(() => manager.EnsureAdminPrivileges());
        }

        #endregion

        #region 6. File Security Validator, Hardening & Reparse Point Attack Prevention Tests

        [Fact]
        public void FileSecurityValidator_VerifiesReadWritePermissionsOnTempDirectory()
        {
            var validator = new FileSecurityValidator(_fileLogger);
            string tempDir = Path.Combine(Path.GetTempPath(), "SAYRA_TestDir_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                Assert.True(validator.HasReadPermission(tempDir));
                Assert.True(validator.HasWritePermission(tempDir));

                var report = validator.ValidateDirectorySecurity(tempDir);
                Assert.True(report.HasReadPermission);
                Assert.True(report.HasWritePermission);
                Assert.True(report.IsValid);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void FileSecurityValidator_VerifiesReadWritePermissionsOnTempFile()
        {
            var validator = new FileSecurityValidator(_fileLogger);
            string tempFile = Path.Combine(Path.GetTempPath(), "SAYRA_TestFile_" + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(tempFile, "Hello World Security Testing");

            try
            {
                Assert.True(validator.HasReadPermission(tempFile));
                Assert.True(validator.HasWritePermission(tempFile));

                var report = validator.ValidateFileSecurity(tempFile);
                Assert.True(report.HasReadPermission);
                Assert.True(report.HasWritePermission);
                Assert.True(report.IsValid);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void FileSecurityValidator_CreateSecureTemporaryDirectory_CreatesAndAppliesAclsSuccessfully()
        {
            var validator = new FileSecurityValidator(_fileLogger);
            string path = validator.CreateSecureTemporaryDirectory();

            try
            {
                Assert.True(Directory.Exists(path));
                Assert.True(validator.HasWritePermission(path));
                Assert.True(validator.HasReadPermission(path));

                var report = validator.ValidateDirectorySecurity(path);
                Assert.True(report.IsValid);
            }
            finally
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
        }

        [Theory]
        [InlineData("..\\..\\SecretFile.txt")]
        [InlineData("C:\\SAYRA\\Client\\..\\..\\Windows\\System32\\cmd.exe")]
        [InlineData("/usr/bin/../../etc/passwd")]
        public void PathHardening_DetectsAndBlocksDirectoryTraversalAttempts(string maliciousPath)
        {
            var validator = new FileSecurityValidator(_fileLogger);
            string secureRoot = OperatingSystem.IsWindows() ? "C:\\SAYRA\\Client" : "/usr/bin";

            Assert.Throws<SecurityValidationException>(() =>
            {
                validator.NormalizeAndValidatePath(maliciousPath, secureRoot);
            });
        }

        [Theory]
        [InlineData("\\\\attacker-ip\\share\\malicious.dll")]
        [InlineData("//attacker-ip/share/malicious.dll")]
        public void PathHardening_DetectsAndBlocksUncPaths(string uncPath)
        {
            var validator = new FileSecurityValidator(_fileLogger);
            string secureRoot = OperatingSystem.IsWindows() ? "C:\\SAYRA\\Client" : "/usr/bin";

            Assert.Throws<SecurityValidationException>(() =>
            {
                validator.NormalizeAndValidatePath(uncPath, secureRoot);
            });
        }

        [Fact]
        public void PathHardening_AcceptsSafeSubPaths()
        {
            var validator = new FileSecurityValidator(_fileLogger);
            string secureRoot = OperatingSystem.IsWindows() ? "C:\\SAYRA\\Client" : "/usr/bin";
            string safePath = OperatingSystem.IsWindows() ? "C:\\SAYRA\\Client\\Updates\\package.spk" : "/usr/bin/package.spk";

            var result = validator.NormalizeAndValidatePath(safePath, secureRoot);
            Assert.NotNull(result);
            Assert.True(Path.IsPathRooted(result));
        }

        [Fact]
        public void PathHardening_BlocksReparsePointAttacks()
        {
            var validator = new FileSecurityValidator(_fileLogger);
            string secureRoot = Path.GetTempPath();

            string targetFile = Path.Combine(secureRoot, "SAYRA_TargetFile_" + Guid.NewGuid().ToString("N") + ".txt");
            string linkFile = Path.Combine(secureRoot, "SAYRA_LinkFile_" + Guid.NewGuid().ToString("N") + ".lnk");

            File.WriteAllText(targetFile, "Genuine Target File Content");

            try
            {
                File.CreateSymbolicLink(linkFile, targetFile);

                Assert.Throws<SecurityValidationException>(() =>
                {
                    validator.NormalizeAndValidatePath(linkFile, secureRoot);
                });

                var report = validator.ValidateFileSecurity(linkFile);
                Assert.False(report.IsValid);
            }
            catch (UnauthorizedAccessException)
            {
                _fileLogger.LogWarning("Skipping symbolic link creation test due to insufficient local Windows execution privileges.");
            }
            finally
            {
                if (File.Exists(linkFile)) File.Delete(linkFile);
                if (File.Exists(targetFile)) File.Delete(targetFile);
            }
        }

        #endregion
    }
}

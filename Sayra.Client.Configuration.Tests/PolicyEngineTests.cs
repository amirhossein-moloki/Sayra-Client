using System;
using System.Collections.Generic;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Security;
using Sayra.Client.Shared.Models;
using SayraClient.Kiosk.Application.Interfaces;
using SayraClient.RemoteOperations.Services;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Sayra.Client.Configuration.Tests
{
    public class PolicyEngineTests
    {
        private readonly Mock<IPolicyRepository> _repoMock;
        private readonly Mock<ISignatureVerifier> _sigVerifierMock;
        private readonly Mock<IAuditService> _auditMock;
        private readonly Mock<IMaintenanceModeService> _maintenanceMock;

        private readonly Mock<ILogger<RegistryPolicyManager>> _regLogger;
        private readonly Mock<ILogger<UsbPolicyManager>> _usbLogger;
        private readonly Mock<ILogger<NetworkPolicyManager>> _netLogger;
        private readonly Mock<ILogger<SessionPolicyManager>> _sesLogger;
        private readonly Mock<ILogger<PolicyValidator>> _valLogger;
        private readonly Mock<ILogger<PolicyRollbackService>> _rollLogger;
        private readonly Mock<ILogger<PolicyEngine>> _engLogger;
        private readonly Mock<ILogger<PolicySynchronizationService>> _syncLogger;

        private readonly RegistryPolicyManager _registryManager;
        private readonly UsbPolicyManager _usbManager;
        private readonly NetworkPolicyManager _networkManager;
        private readonly SessionPolicyManager _sessionManager;
        private readonly PolicyValidator _validator;
        private readonly PolicyRollbackService _rollbackService;
        private readonly PolicyEngine _policyEngine;
        private readonly PolicySynchronizationService _syncService;

        public PolicyEngineTests()
        {
            _repoMock = new Mock<IPolicyRepository>();
            _sigVerifierMock = new Mock<ISignatureVerifier>();
            _auditMock = new Mock<IAuditService>();
            _maintenanceMock = new Mock<IMaintenanceModeService>();

            _regLogger = new Mock<ILogger<RegistryPolicyManager>>();
            _usbLogger = new Mock<ILogger<UsbPolicyManager>>();
            _netLogger = new Mock<ILogger<NetworkPolicyManager>>();
            _sesLogger = new Mock<ILogger<SessionPolicyManager>>();
            _valLogger = new Mock<ILogger<PolicyValidator>>();
            _rollLogger = new Mock<ILogger<PolicyRollbackService>>();
            _engLogger = new Mock<ILogger<PolicyEngine>>();
            _syncLogger = new Mock<ILogger<PolicySynchronizationService>>();

            _registryManager = new RegistryPolicyManager(_regLogger.Object);
            _usbManager = new UsbPolicyManager(_usbLogger.Object);
            _networkManager = new NetworkPolicyManager(_netLogger.Object);
            _sessionManager = new SessionPolicyManager(_sesLogger.Object, _maintenanceMock.Object);

            _validator = new PolicyValidator(_sigVerifierMock.Object, _valLogger.Object);
            _rollbackService = new PolicyRollbackService(_registryManager, _usbManager, _networkManager, _sessionManager, _rollLogger.Object);

            _policyEngine = new PolicyEngine(
                _repoMock.Object,
                _validator,
                _rollbackService,
                _registryManager,
                _usbManager,
                _networkManager,
                _sessionManager,
                _engLogger.Object
            );

            _syncService = new PolicySynchronizationService(
                _policyEngine,
                _repoMock.Object,
                _validator,
                _auditMock.Object,
                _syncLogger.Object
            );
        }

        private PolicyProfile CreateValidProfile(long version = 1)
        {
            return new PolicyProfile
            {
                PolicyId = "POLICY_001",
                Name = "Default Workstation Security Policy",
                Version = version,
                IssuedAt = DateTime.UtcNow,
                Signature = "VALID_TEST_SIGNATURE",
                Rules = new List<PolicyRule>
                {
                    new PolicyRule
                    {
                        RuleId = "RULE_01",
                        Category = PolicyCategory.WINDOWS,
                        Action = "DISABLE_TASK_MANAGER",
                        Value = "true"
                    }
                }
            };
        }

        // 1. Policy Validation Tests
        [Fact]
        public void PolicyValidator_ShouldDetectMissingFields()
        {
            var invalidProfile = new PolicyProfile
            {
                PolicyId = "",
                Version = 0,
                Signature = "",
                Rules = new List<PolicyRule>()
            };

            var res = _validator.Validate(invalidProfile);

            Assert.False(res.IsValid);
            Assert.Contains("PolicyId is required.", res.Errors);
            Assert.Contains("Policy version must be greater than zero.", res.Errors);
            Assert.Contains("Digital signature is missing.", res.Errors);
            Assert.Contains("Policy profile must contain at least one rule.", res.Errors);
        }

        // 2. Signature Validation Tests
        [Fact]
        public void PolicyValidator_ShouldValidateSignature()
        {
            var profile = CreateValidProfile();
            profile.Signature = "INVALID_SIGNATURE";

            _sigVerifierMock.Setup(s => s.VerifySignature(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(false);

            var res = _validator.Validate(profile);

            Assert.False(res.IsValid);
            Assert.Contains("Cryptographic digital signature verification failed against Master Server public key.", res.Errors);
        }

        // 3. Registry Application Tests
        [Fact]
        public async Task RegistryPolicyManager_ShouldApplySupportedRegistryPolicies()
        {
            var actions = new[] {
                "HIDE_DRIVES", "DISABLE_CONTROL_PANEL", "DISABLE_TASK_MANAGER",
                "DISABLE_REGISTRY_EDITOR", "DISABLE_COMMAND_PROMPT", "DISABLE_POWERSHELL",
                "DESKTOP_RESTRICTION", "EXPLORER_RESTRICTION"
            };

            foreach (var action in actions)
            {
                bool result = await _registryManager.ApplyRegistryPolicyAsync(action, "true", CancellationToken.None);
                Assert.True(result);
            }
        }

        // 4. Rollback Tests
        [Fact]
        public async Task PolicyRollbackService_ShouldRestoreOriginalConfiguration()
        {
            await _registryManager.ApplyRegistryPolicyAsync("DISABLE_TASK_MANAGER", "true", CancellationToken.None);
            await _usbManager.ApplyUsbPolicyAsync("USB_BLOCK", "true", null, CancellationToken.None);
            await _networkManager.ApplyNetworkPolicyAsync("BANDWIDTH_LIMIT", "5000", null, CancellationToken.None);
            await _sessionManager.ApplySessionPolicyAsync("AUTO_LOGOUT", "true", CancellationToken.None);

            bool result = await _rollbackService.RollbackAllAsync(CancellationToken.None);

            Assert.True(result);
        }

        // 5. Version Upgrades Tests
        [Fact]
        public async Task SynchronizationService_ShouldAcceptNewerVersionCode()
        {
            _repoMock.Setup(r => r.GetPolicyVersionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1L);
            _sigVerifierMock.Setup(s => s.VerifySignature(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(true);

            var newerProfile = CreateValidProfile(version: 2);

            var res = await _syncService.SynchronizePolicyAsync(newerProfile, CancellationToken.None);

            Assert.True(res.Success);
            _repoMock.Verify(r => r.SavePolicyAsync(newerProfile, It.IsAny<CancellationToken>()), Times.Once);
        }

        // 6. Version Downgrade Rejection Tests
        [Fact]
        public async Task SynchronizationService_ShouldRejectOlderVersionCode()
        {
            _repoMock.Setup(r => r.GetPolicyVersionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(5L);

            var olderProfile = CreateValidProfile(version: 3);

            var res = await _syncService.SynchronizePolicyAsync(olderProfile, CancellationToken.None);

            Assert.False(res.Success);
            Assert.Contains("Downgrade rejection!", res.Errors[0]);
            _repoMock.Verify(r => r.SavePolicyAsync(It.IsAny<PolicyProfile>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // 7. USB Block/Unblock Tests
        [Fact]
        public async Task UsbPolicyManager_ShouldBlockAndUnblockUsbStorage()
        {
            bool blockRes = await _usbManager.ApplyUsbPolicyAsync("USB_BLOCK", "true", null, CancellationToken.None);
            Assert.True(blockRes);
            Assert.Equal(4, _usbManager.GetUsbStorStartForTest());

            bool unblockRes = await _usbManager.ApplyUsbPolicyAsync("USB_BLOCK", "false", null, CancellationToken.None);
            Assert.True(unblockRes);
            Assert.Equal(3, _usbManager.GetUsbStorStartForTest());
        }

        [Fact]
        public async Task UsbPolicyManager_ShouldCorrectlyEvaluateBlackListAndWhiteList()
        {
            var approvedDevices = new List<string> { "USB\\VID_1111&PID_2222", "USB\\VID_3333&PID_4444" };
            await _usbManager.ApplyUsbPolicyAsync("USB_WHITELIST", "true", approvedDevices, CancellationToken.None);

            Assert.True(_usbManager.IsHardwareIdAllowed("USB\\VID_1111&PID_2222"));
            Assert.True(_usbManager.IsHardwareIdAllowed("USB\\VID_3333&PID_4444"));

            var blockedDevices = new List<string> { "USB\\VID_6666&PID_7777" };
            await _usbManager.ApplyUsbPolicyAsync("USB_BLACKLIST", "true", blockedDevices, CancellationToken.None);

            Assert.False(_usbManager.IsHardwareIdAllowed("USB\\VID_6666&PID_7777"));
        }

        // 8. Network Policy Application Tests
        [Fact]
        public async Task NetworkPolicyManager_ShouldApplyNetworkPoliciesCorrectly()
        {
            await _networkManager.ApplyNetworkPolicyAsync("BANDWIDTH_LIMIT", "2000", null, CancellationToken.None);
            await _networkManager.ApplyNetworkPolicyAsync("DNS_CONFIGURATION", "1.1.1.1,8.8.8.8", null, CancellationToken.None);
            await _networkManager.ApplyNetworkPolicyAsync("QOS_INTEGRATION", "DSCP_46", null, CancellationToken.None);
            await _networkManager.ApplyNetworkPolicyAsync("APP_DENY_LIST", "true", new List<string> { "cheat.exe" }, CancellationToken.None);

            Assert.Equal("2000", _networkManager.GetConfigValueForTest("BandwidthLimitKbps"));
            Assert.Equal("1.1.1.1,8.8.8.8", _networkManager.GetConfigValueForTest("DnsConfiguration"));
            Assert.Equal("DSCP_46", _networkManager.GetConfigValueForTest("QosPriority"));
            Assert.False(_networkManager.IsNetworkAccessAllowedForApp("cheat.exe"));
        }

        // 9. Session Timeout Tests
        [Fact]
        public async Task SessionPolicyManager_ShouldConfigureSessionSettingsCorrectly()
        {
            await _sessionManager.ApplySessionPolicyAsync("SESSION_TIMEOUT", "45", CancellationToken.None);
            await _sessionManager.ApplySessionPolicyAsync("IDLE_TIMEOUT", "15", CancellationToken.None);
            await _sessionManager.ApplySessionPolicyAsync("KIOSK_ENFORCEMENT", "true", CancellationToken.None);

            Assert.Equal(45, _sessionManager.GetSettingForTest("SessionTimeoutMinutes"));
            Assert.Equal(15, _sessionManager.GetSettingForTest("IdleTimeoutMinutes"));
            Assert.Equal(true, _sessionManager.GetSettingForTest("KioskEnforcementActive"));
        }

        [Fact]
        public async Task SessionPolicyManager_ShouldToggleMaintenanceModeCorrectly()
        {
            await _sessionManager.ApplySessionPolicyAsync("MAINTENANCE_MODE", "true", CancellationToken.None);
            _maintenanceMock.Verify(m => m.EnterMaintenanceModeAsync("DefaultAdminPassword123!"), Times.Once);

            await _sessionManager.ApplySessionPolicyAsync("MAINTENANCE_MODE", "false", CancellationToken.None);
            _maintenanceMock.Verify(m => m.ExitMaintenanceMode(), Times.Once);
        }

        // 10. Permission Denied Tests
        [Fact]
        public async Task UsbPolicyManager_ShouldThrowSecurityExceptionIfUserNotAdmin()
        {
            _usbManager.SimulateNonAdminForTest = true;

            await Assert.ThrowsAsync<SecurityException>(() =>
                _usbManager.ApplyUsbPolicyAsync("USB_BLOCK", "true", null, CancellationToken.None));
        }

        // 11. Partial Failure & Reversion Tests
        [Fact]
        public async Task PolicyEngine_ShouldPerformCompleteRollbackOnPartialRuleFailure()
        {
            var invalidRuleProfile = CreateValidProfile();
            invalidRuleProfile.Rules = new List<PolicyRule>
            {
                new PolicyRule
                {
                    RuleId = "RULE_01",
                    Category = PolicyCategory.WINDOWS,
                    Action = "DISABLE_TASK_MANAGER",
                    Value = "true"
                },
                new PolicyRule
                {
                    RuleId = "RULE_02",
                    Category = PolicyCategory.WINDOWS,
                    Action = "INVALID_UNSUPPORTED_ACTION",
                    Value = "true"
                }
            };

            var res = await _policyEngine.ApplyPoliciesAsync(invalidRuleProfile, CancellationToken.None);

            Assert.False(res.Success);
            var val = _registryManager.GetCurrentPolicyValueForTest(@"Software\Microsoft\Windows\CurrentVersion\Policies\System", "DisableTaskMgr");
            Assert.Null(val);
        }

        // 12. Concurrent Policy Updates Tests
        [Fact]
        public async Task PolicyEngine_ShouldHandleConcurrentPolicyUpdatesGracefully()
        {
            var p1 = CreateValidProfile(10);
            var p2 = CreateValidProfile(20);

            var t1 = Task.Run(() => _policyEngine.ApplyPoliciesAsync(p1, CancellationToken.None));
            var t2 = Task.Run(() => _policyEngine.ApplyPoliciesAsync(p2, CancellationToken.None));

            await Task.WhenAll(t1, t2);

            Assert.True(t1.Result.Success || t2.Result.Success);
        }

        // 13. Policy Conflict Detection Tests
        [Fact]
        public void PolicyValidator_ShouldDetectRuleConflictsAndDuplicates()
        {
            var conflictingProfile = CreateValidProfile();
            conflictingProfile.Rules = new List<PolicyRule>
            {
                new PolicyRule
                {
                    RuleId = "RULE_01",
                    Category = PolicyCategory.WINDOWS,
                    Action = "DISABLE_TASK_MANAGER",
                    Value = "true"
                },
                new PolicyRule
                {
                    RuleId = "RULE_02",
                    Category = PolicyCategory.WINDOWS,
                    Action = "DISABLE_TASK_MANAGER",
                    Value = "false"
                }
            };

            var res = _validator.Validate(conflictingProfile);

            Assert.False(res.IsValid);
            Assert.Contains("Duplicate rule conflict detected for 'WINDOWS:DISABLE_TASK_MANAGER:'.", res.Errors);
        }

        // 14. Policy Idempotency Tests
        [Fact]
        public async Task PolicyEngine_ShouldApplyIdempotentlyWithoutCorruption()
        {
            var profile = CreateValidProfile(4);
            _sigVerifierMock.Setup(s => s.VerifySignature(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(true);

            var res1 = await _policyEngine.ApplyPoliciesAsync(profile, CancellationToken.None);
            var res2 = await _policyEngine.ApplyPoliciesAsync(profile, CancellationToken.None);

            Assert.True(res1.Success);
            Assert.True(res2.Success);
            _repoMock.Verify(r => r.SavePolicyAsync(profile, It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        // 15. Audit Logging Tests
        [Fact]
        public async Task SynchronizationService_ShouldEmitAllRequiredAuditEvents()
        {
            _repoMock.Setup(r => r.GetPolicyVersionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1L);
            _sigVerifierMock.Setup(s => s.VerifySignature(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(true);

            var profile = CreateValidProfile(version: 12);

            var res = await _syncService.SynchronizePolicyAsync(profile, CancellationToken.None);

            Assert.True(res.Success);

            _auditMock.Verify(a => a.RecordPolicyEventAsync(profile.PolicyId, "POLICY_RECEIVED", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            _auditMock.Verify(a => a.RecordPolicyEventAsync(profile.PolicyId, "POLICY_VALIDATED", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            _auditMock.Verify(a => a.RecordPolicyEventAsync(profile.PolicyId, "POLICY_APPLIED", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        // 16. Rollback Verification Tests
        [Fact]
        public async Task PolicyRollbackService_ShouldSuccessfullyVerifyRollbackStatus()
        {
            await _registryManager.ApplyRegistryPolicyAsync("DISABLE_TASK_MANAGER", "true", CancellationToken.None);

            await _rollbackService.RollbackAllAsync(CancellationToken.None);

            bool verified = await _rollbackService.VerifyRollbackAsync(CancellationToken.None);
            Assert.True(verified);
        }

        // 17. High Load / Performance Sync Tests
        [Fact]
        public async Task SynchronizationService_ShouldExecuteRapidlyUnderHighLoad()
        {
            _repoMock.Setup(r => r.GetPolicyVersionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1L);
            _sigVerifierMock.Setup(s => s.VerifySignature(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(true);

            var profiles = new List<PolicyProfile>();
            for (int i = 2; i <= 20; i++)
            {
                profiles.Add(CreateValidProfile(i));
            }

            var start = DateTime.UtcNow;
            foreach (var profile in profiles)
            {
                var res = await _syncService.SynchronizePolicyAsync(profile, CancellationToken.None);
                Assert.True(res.Success);
            }
            var duration = DateTime.UtcNow - start;

            // Execution of 19 updates must be sub-second under mock storage
            Assert.True(duration.TotalMilliseconds < 1000);
        }
    }
}

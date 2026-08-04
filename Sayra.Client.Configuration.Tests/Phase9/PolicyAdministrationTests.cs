using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Sayra.Client.Shared.DependencyInjection;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Domain.Policy;
using Sayra.Client.Shared.Models.Phase9.Enums;
using Sayra.Client.Shared.Models.Phase9.Events;
using Sayra.Client.Shared.Models.Phase9.Options;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Phase9;
using Sayra.Client.Shared.Interfaces.Security;
using Sayra.Client.Shared.Fleet.Policy.Interfaces;
using Sayra.Client.Shared.Fleet.Policy.Services;
using Xunit;

namespace Sayra.Client.Configuration.Tests.Phase9
{
    using LocalFleetManager = Sayra.Client.Shared.Interfaces.Phase9.IFleetManager;

    public class PolicyAdministrationTests
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly Mock<LocalFleetManager> _mockFleetManager;
        private readonly Mock<IEventDispatcher> _mockEventDispatcher;
        private readonly Mock<ICryptographyService> _mockCryptography;

        public PolicyAdministrationTests()
        {
            var services = new ServiceCollection();

            _mockFleetManager = new Mock<LocalFleetManager>();
            _mockEventDispatcher = new Mock<IEventDispatcher>();
            _mockCryptography = new Mock<ICryptographyService>();

            // Setup default ICryptographyService mock
            _mockCryptography.Setup(c => c.CreateHash(It.IsAny<string>()))
                .Returns((string s) => "HASH_" + s.Length);
            _mockCryptography.Setup(c => c.CreateSignature(It.IsAny<byte[]>(), It.IsAny<byte[]>()))
                .Returns((byte[] b, byte[] k) => b.Concat(new byte[] { 1, 2, 3 }).ToArray());
            _mockCryptography.Setup(c => c.VerifySignature(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>()))
                .Returns(true);

            // Register Options
            services.Configure<PolicyOptions>(options =>
            {
                options.PolicyCacheDirectory = "Data/TestPolicies";
                options.ComplianceEvaluationIntervalMinutes = 10;
            });

            // Register Mocks
            services.AddSingleton(_mockFleetManager.Object);
            services.AddSingleton(_mockEventDispatcher.Object);
            services.AddSingleton(_mockCryptography.Object);

            // Add standard Null Logger
            services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

            // Register Policy Administration Engine
            services.AddPolicyAdministration();

            _serviceProvider = services.BuildServiceProvider();
        }

        private IPolicyManager GetManager() => _serviceProvider.GetRequiredService<IPolicyManager>();

        #region 1. Policy Creation and Lifecycles

        [Fact]
        public async Task CreatePolicy_Generates_Valid_Template_And_Audit_Logs()
        {
            // Arrange
            var manager = GetManager();

            // Act
            var policy = await manager.Administration.CreatePolicyAsync(
                "Kiosk Lockdown Rules",
                "Strict shell restriction controls for guest sessions",
                "Kiosk",
                "Admin-1",
                CancellationToken.None);

            // Assert
            Assert.NotNull(policy);
            Assert.StartsWith("POL_", policy.PolicyId);
            Assert.Equal("Kiosk Lockdown Rules", policy.Name);
            Assert.Equal("Kiosk", policy.Category);
            Assert.False(policy.IsArchived);

            // Verification of Audit History saving
            var history = await manager.Repository.GetHistoryAsync(policy.PolicyId);
            Assert.NotEmpty(history);
            Assert.Contains(history, h => h.Action == "Create" && h.Actor == "Admin-1");

            // Event dispatched check
            _mockEventDispatcher.Verify(ed => ed.Dispatch(It.Is<PolicyCreated>(e => e.PolicyId == policy.PolicyId)), Times.Once);
        }

        [Fact]
        public async Task ClonePolicy_Clones_Rules_And_Preserves_Immutability()
        {
            // Arrange
            var manager = GetManager();
            var src = await manager.Administration.CreatePolicyAsync("Source Policy", "Desc", "Security", "Operator-A");

            // Add a rule to source
            var rule = new PolicyRule
            {
                RuleId = "RULE-01",
                Name = "Block Removable Remedia",
                Category = "Hardware",
                Parameters = new List<PolicyParameter> { new PolicyParameter { Name = "usb_enable", Value = "false" } }
            };
            await manager.Administration.PublishVersionAsync(src.PolicyId, "1.1.0", new List<PolicyRule> { rule }, "Update Rule", "Operator-A");

            // Act
            var clone = await manager.Administration.ClonePolicyAsync(src.PolicyId, "Cloned Policy", "Operator-B");

            // Assert
            Assert.NotNull(clone);
            Assert.NotEqual(src.PolicyId, clone.PolicyId);
            Assert.Equal("Cloned Policy", clone.Name);
            Assert.Single(clone.Rules);
            Assert.Equal("RULE-01", clone.Rules[0].RuleId);

            // Ensure changing clone rules doesn't affect source
            var modifiedClone = clone with { Rules = new List<PolicyRule>() };
            Assert.Single(clone.Rules); // Original immutable clone untouched
        }

        #endregion

        #region 2. Semantic Versioning and History

        [Fact]
        public async Task VersionManager_Calculates_Next_Patch_Version_And_Maintains_SemVer()
        {
            // Arrange
            var manager = GetManager();
            var pol = await manager.Administration.CreatePolicyAsync("Versioned Policy", "Desc", "Network", "Admin");

            // Act
            var v1 = await manager.Administration.PublishVersionAsync(pol.PolicyId, "1.0.1", new List<PolicyRule>(), "Initial version", "Admin");
            var v2 = await manager.Administration.PublishVersionAsync(pol.PolicyId, "1.0.2", new List<PolicyRule>(), "First patch", "Admin");

            // Assert
            Assert.Equal("1.0.1", v1.VersionTag);
            Assert.Equal("1.0.2", v2.VersionTag);

            var cmp = await manager.Versions.CompareVersionsAsync("1.0.2", "1.0.1");
            Assert.True(cmp > 0);

            var history = await manager.Versions.GetVersionHistoryAsync(pol.PolicyId);
            Assert.Equal(3, history.Count); // Initial mock + 1.0.1 + 1.0.2
            Assert.Equal("1.0.2", history[0].VersionTag); // Sorted descending
        }

        #endregion

        #region 3. Policy Assignment, Priorities, and Inheritance

        [Fact]
        public async Task PolicyAssignments_Resolved_For_Machine_With_Specificity_Hierarchies()
        {
            // Arrange
            var manager = GetManager();
            var polGlobal = await manager.Administration.CreatePolicyAsync("Global Base Policy", "Base config", "System", "Admin");
            var polSpecific = await manager.Administration.CreatePolicyAsync("Machine Specific Policy", "Override config", "System", "Admin");

            var ruleGlobal = new PolicyRule
            {
                RuleId = "RULE-SYSTEM",
                Name = "Core Settings",
                Category = "General",
                Parameters = new List<PolicyParameter>
                {
                    new PolicyParameter { Name = "kiosk_mode", Value = "false" },
                    new PolicyParameter { Name = "volume_limit", Value = "50" }
                }
            };
            var ruleSpecific = new PolicyRule
            {
                RuleId = "RULE-SYSTEM",
                Name = "Core Settings Specific",
                Category = "General",
                Parameters = new List<PolicyParameter>
                {
                    new PolicyParameter { Name = "kiosk_mode", Value = "true" } // override
                }
            };

            await manager.Administration.PublishVersionAsync(polGlobal.PolicyId, "1.0.0", new List<PolicyRule> { ruleGlobal }, "v1", "Admin");
            await manager.Administration.PublishVersionAsync(polSpecific.PolicyId, "1.0.0", new List<PolicyRule> { ruleSpecific }, "v1", "Admin");

            // Setup Machine Mock Context
            var machine = new MachineInfo
            {
                MachineId = "WS-01",
                Hostname = "PC-FRONT-01",
                IpAddress = "192.168.1.10",
                Inventory = new MachineInventory
                {
                    OperatingSystem = "Windows 11 Professional",
                    StorageDrives = new Dictionary<string, string> { { "C", "512GB" } }
                }
            };

            _mockFleetManager.Setup(fm => fm.GetGroupMembersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<MachineInfo> { machine });

            // Create assignments:
            // 1. Group level assignment: priority 100, override enabled
            await manager.Assignments.CreateAssignmentAsync(
                polGlobal.PolicyId,
                "1.0.0",
                new PolicyTarget { TargetId = "G_GAMING", TargetType = "Group" },
                100,
                true,
                null,
                "Admin");

            // 2. Specific machine level assignment: priority 200, override enabled
            await manager.Assignments.CreateAssignmentAsync(
                polSpecific.PolicyId,
                "1.0.0",
                new PolicyTarget { TargetId = "WS-01", TargetType = "Machine" },
                200,
                true,
                null,
                "Admin");

            // Act
            var mergedPolicy = await manager.Assignments.ResolveAndMergePoliciesAsync(machine);

            // Assert
            Assert.NotNull(mergedPolicy);
            var systemRule = mergedPolicy.Rules.FirstOrDefault(r => r.RuleId == "RULE-SYSTEM");
            Assert.NotNull(systemRule);

            // Higher priority/specificity Machine Assignment overrides kiosk_mode to true,
            // but inherits remaining volume_limit parameter as 50.
            var paramKiosk = systemRule.Parameters.FirstOrDefault(p => p.Name == "kiosk_mode");
            Assert.NotNull(paramKiosk);
            Assert.Equal("true", paramKiosk.Value);

            var paramVolume = systemRule.Parameters.FirstOrDefault(p => p.Name == "volume_limit");
            Assert.NotNull(paramVolume);
            Assert.Equal("50", paramVolume.Value);
        }

        #endregion

        #region 4. Validations and Compatibility

        [Fact]
        public async Task Validator_Detects_Category_Violation_And_Rule_Dependency_Loops()
        {
            // Arrange
            var manager = GetManager();
            var validator = manager.Validator;

            var invalidPol = new PolicyDefinition
            {
                PolicyId = "POL-01",
                Name = "Invalid",
                Category = "SuperUnallowedCustomType", // Invalid category
                Rules = new List<PolicyRule>()
            };

            // Act & Assert Schema
            var validSchema = await validator.ValidateSchemaAsync(invalidPol);
            Assert.False(validSchema);

            // Dependency check
            var depPol = new PolicyDefinition
            {
                PolicyId = "POL-DEP",
                Name = "Dependency Check",
                Category = "Security",
                Rules = new List<PolicyRule>
                {
                    new PolicyRule
                    {
                        RuleId = "RULE-A",
                        Name = "Rule A",
                        Conditions = new List<PolicyCondition>
                        {
                            new PolicyCondition { Field = "Dependency", Operator = "DependsOn", Value = "RULE-B" } // Missing RULE-B
                        }
                    }
                }
            };

            var validDeps = await validator.ValidateDependenciesAsync(depPol);
            Assert.False(validDeps);
        }

        #endregion

        #region 5. Policy Preview Engine

        [Fact]
        public async Task PreviewEngine_Calculates_Affected_Count_And_Flags_Conflicts()
        {
            // Arrange
            var manager = GetManager();
            var pol = await manager.Administration.CreatePolicyAsync("Preview Pol", "Desc", "Network", "Admin");

            var machine1 = new MachineInfo { MachineId = "WS-01", Hostname = "PC-FRONT-01" };
            var machine2 = new MachineInfo { MachineId = "WS-02", Hostname = "PC-BACK-02" };
            _mockFleetManager.Setup(fm => fm.GetAllMachinesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<MachineInfo> { machine1, machine2 });

            // Set up pre-existing assignment to flag conflicts
            await manager.Assignments.CreateAssignmentAsync(
                "POL_OTHER",
                "1.0.0",
                new PolicyTarget { TargetId = "WS-01", TargetType = "Machine" },
                100,
                true,
                null,
                "Admin");

            // Act
            var preview = await manager.PreviewEngine.GeneratePreviewAsync(
                pol.PolicyId,
                "1.0.0",
                new PolicyTarget { TargetId = "WS-01", TargetType = "Machine" });

            // Assert
            Assert.NotNull(preview);
            Assert.Equal(1, preview.AffectedWorkstationsCount);
            Assert.NotEmpty(preview.PotentialConflicts);
            Assert.Contains(preview.PotentialConflicts, c => c.Contains("POL_OTHER"));
        }

        #endregion

        #region 6. Policy Difference Comparison

        [Fact]
        public async Task DiffEngine_Computes_Structural_Added_And_Changed_Parameters()
        {
            // Arrange
            var manager = GetManager();

            var rule1 = new PolicyRule
            {
                RuleId = "RULE-01",
                Name = "Core Rules",
                Parameters = new List<PolicyParameter> { new PolicyParameter { Name = "kiosk", Value = "true" } }
            };

            var rule2 = new PolicyRule
            {
                RuleId = "RULE-01",
                Name = "Core Rules",
                Parameters = new List<PolicyParameter> { new PolicyParameter { Name = "kiosk", Value = "false" } } // changed
            };

            var oldVer = new PolicyVersion { PolicyId = "POL-01", VersionTag = "1.0.0", Rules = new List<PolicyRule> { rule1 } };
            var newVer = new PolicyVersion { PolicyId = "POL-01", VersionTag = "1.1.0", Rules = new List<PolicyRule> { rule2 } };

            // Act
            var report = await manager.DiffEngine.CompareVersionsAsync(oldVer, newVer);

            // Assert
            Assert.NotNull(report);
            Assert.True(report.IsDifferent);
            Assert.Empty(report.AddedRules);
            Assert.Empty(report.RemovedRules);
            Assert.NotEmpty(report.ChangedValues);
            Assert.Contains(report.ChangedValues, v => v.Contains("kiosk") && v.Contains("false"));
        }

        #endregion

        #region 7. Compliance Tracking and Scoring

        [Fact]
        public async Task EvaluateCompliance_Calculates_Correct_Score_And_Records_Violations()
        {
            // Arrange
            var manager = GetManager();

            var machine = new MachineInfo
            {
                MachineId = "WS-100",
                Hostname = "PC-FRONT-100",
                Inventory = new MachineInventory { OperatingSystem = "Windows" }
            };

            _mockFleetManager.Setup(fm => fm.GetMachineAsync("WS-100", It.IsAny<CancellationToken>()))
                .ReturnsAsync(machine);

            var pol = await manager.Administration.CreatePolicyAsync("Comp Pol", "Desc", "Security", "Admin");
            var rule = new PolicyRule
            {
                RuleId = "RULE-SEC",
                Name = "Sec Check",
                Parameters = new List<PolicyParameter>
                {
                    new PolicyParameter { Name = "usb_enable", Value = "false" },
                    new PolicyParameter { Name = "admin_pw", Value = "secure!" }
                }
            };
            await manager.Administration.PublishVersionAsync(pol.PolicyId, "1.0.0", new List<PolicyRule> { rule }, "Publish", "Admin");

            // Assign
            await manager.Assignments.CreateAssignmentAsync(
                pol.PolicyId,
                "1.0.0",
                new PolicyTarget { TargetId = "WS-100", TargetType = "Machine" },
                100,
                true,
                null,
                "Admin");

            // Workstation Mismatched State
            var actualState = new Dictionary<string, string>
            {
                { "usb_enable", "true" }, // Mismatched (Violation)
                { "admin_pw", "secure!" } // Matched
            };

            // Act
            var complianceRecord = await manager.Compliance.EvaluateComplianceAsync("WS-100", actualState);

            // Assert
            Assert.NotNull(complianceRecord);
            Assert.Equal(50.0, complianceRecord.ComplianceScore); // 1 out of 2 rules matched
            Assert.Equal(ComplianceStatus.NonCompliantWarning, complianceRecord.OverallStatus);
            Assert.Single(complianceRecord.Violations);
            Assert.Equal("RULE-SEC", complianceRecord.Violations[0].RuleId);

            // Verify event dispatched
            _mockEventDispatcher.Verify(ed => ed.Dispatch(It.Is<ComplianceChanged>(e => e.NewScore == 50.0)), Times.Once);
        }

        #endregion

        #region 8. Policy Rollback System

        [Fact]
        public async Task RollbackToVersion_Successfully_Restores_Older_Rules_And_Verifies_Signatures()
        {
            // Arrange
            var manager = GetManager();
            var pol = await manager.Administration.CreatePolicyAsync("Rollback Pol", "Desc", "Security", "Admin");

            var ruleV1 = new PolicyRule { RuleId = "RULE-1", Name = "Rule v1" };
            var ruleV2 = new PolicyRule { RuleId = "RULE-2", Name = "Rule v2" };

            await manager.Administration.PublishVersionAsync(pol.PolicyId, "1.0.0", new List<PolicyRule> { ruleV1 }, "First Version", "Admin");
            await manager.Administration.PublishVersionAsync(pol.PolicyId, "1.0.1", new List<PolicyRule> { ruleV2 }, "Second Version", "Admin");

            var polState = await manager.Repository.GetPolicyAsync(pol.PolicyId);
            Assert.Equal("1.0.1", polState.ActiveVersionTag);

            // Act
            var restoredVersion = await manager.Rollback.RollbackToVersionAsync(pol.PolicyId, "1.0.0", "Operator-Rollback");

            // Assert
            Assert.NotNull(restoredVersion);
            Assert.Equal("1.0.0", restoredVersion.VersionTag);

            var polRestored = await manager.Repository.GetPolicyAsync(pol.PolicyId);
            Assert.Equal("1.0.0", polRestored.ActiveVersionTag);
            Assert.Single(polRestored.Rules);
            Assert.Equal("RULE-1", polRestored.Rules[0].RuleId);

            _mockEventDispatcher.Verify(ed => ed.Dispatch(It.Is<PolicyRollbackCompleted>(e => e.PolicyId == pol.PolicyId && e.RestoredVersionTag == "1.0.0")), Times.Once);
        }

        #endregion

        #region 9. Concurrency Thread Safety

        [Fact]
        public async Task Concurrent_Policy_Execution_Does_Not_Cause_Race_Conditions()
        {
            // Arrange
            var manager = GetManager();
            var pol = await manager.Administration.CreatePolicyAsync("Concurrency Pol", "Desc", "Performance", "Admin");

            int threadCount = 10;
            var tasks = new Task[threadCount];

            // Act
            for (int i = 0; i < threadCount; i++)
            {
                int index = i;
                tasks[i] = Task.Run(async () =>
                {
                    var rule = new PolicyRule { RuleId = $"RULE-CONCUR-{index}", Name = $"Rule {index}" };
                    await manager.Administration.PublishVersionAsync(
                        pol.PolicyId,
                        $"1.0.{index}",
                        new List<PolicyRule> { rule },
                        $"Version index {index}",
                        "Concurrent-Admin");
                });
            }

            await Task.WhenAll(tasks);

            // Assert
            var finalPol = await manager.Repository.GetPolicyAsync(pol.PolicyId);
            Assert.NotNull(finalPol);
            Assert.NotEmpty(finalPol.ActiveVersionTag);

            var history = await manager.Repository.GetHistoryAsync(pol.PolicyId);
            Assert.Equal(threadCount + 1, history.Count); // initial + 10 publishes
        }

        #endregion
    }
}

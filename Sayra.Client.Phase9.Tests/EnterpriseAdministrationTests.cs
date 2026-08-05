using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Sayra.Client.Shared.DependencyInjection;
using Sayra.Client.Shared.Fleet.Administration;
using Sayra.Client.Shared.Fleet.Administration.Security;
using Sayra.Client.Shared.Fleet.Administration.Orchestration;
using Sayra.Client.Shared.Fleet.Administration.Queries;
using Sayra.Client.Shared.Interfaces.Phase9;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Dtos;
using Sayra.Client.Shared.Models.Phase9.Enums;
using Xunit;

namespace Sayra.Client.Phase9.Tests
{
    public class EnterpriseAdministrationTests
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IAuthenticationService _authService;
        private readonly IAuthorizationService _authzService;
        private readonly IAdministrationApiService _apiService;
        private readonly IEnterpriseManagementCoordinator _coordinator;
        private readonly IDashboardQueryService _dashboard;
        private readonly IAuditIntegrationService _audit;

        // Mocks for constructor DI resolution
        private readonly Mock<IFleetManager> _fleetMock = new();
        private readonly Mock<IRemoteCommandService> _commandMock = new();
        private readonly Mock<IRemoteDiagnosticsService> _diagMock = new();
        private readonly Mock<IPolicyAssignmentService> _policyAssignMock = new();
        private readonly Mock<IPolicyComplianceService> _policyComplianceMock = new();
        private readonly Mock<IPolicyAdministrationService> _policyAdminMock = new();
        private readonly Mock<IAssetManagementService> _assetMock = new();
        private readonly Mock<IMaintenanceService> _maintenanceMock = new();
        private readonly Mock<IMaintenanceScheduler> _maintenanceSchedulerMock = new();
        private readonly Mock<IBulkOperationService> _bulkMock = new();
        private readonly Mock<IRemoteSupportService> _supportMock = new();
        private readonly Mock<IRemoteSessionManager> _supportSessionMock = new();
        private readonly Mock<IRemoteFileService> _fileMock = new();
        private readonly Mock<ITransferManager> _transferMock = new();
        private readonly Mock<Sayra.Client.Shared.Interfaces.Recovery.IHealthMonitor> _healthMonitorMock = new();
        private readonly Mock<Sayra.Client.Shared.Interfaces.Recovery.IResourceMonitor> _resourceMonitorMock = new();
        private readonly Mock<Sayra.Client.Shared.Interfaces.IEventDispatcher> _eventDispatcherMock = new();
        private readonly Mock<Sayra.Client.Shared.Interfaces.Security.ICryptographyService> _cryptographyServiceMock = new();
        private readonly Mock<Sayra.Client.Shared.Interfaces.IAuditLogger> _auditLoggerMock = new();

        public EnterpriseAdministrationTests()
        {
            var services = new ServiceCollection();

            // Add standard Logging infrastructure
            services.AddLogging();

            // Register Phase 9 standard foundations (validators, options, etc.)
            services.AddPhase9Foundation();

            // Register Enterprise Administration Stage 10 platform services
            services.AddEnterpriseAdministration();

            // Register Mocks AFTER core extensions to override default implementations cleanly
            services.AddSingleton(_fleetMock.Object);
            services.AddSingleton(_commandMock.Object);
            services.AddSingleton(_diagMock.Object);
            services.AddSingleton(_policyAssignMock.Object);
            services.AddSingleton(_policyComplianceMock.Object);
            services.AddSingleton(_policyAdminMock.Object);
            services.AddSingleton(_assetMock.Object);
            services.AddSingleton(_maintenanceMock.Object);
            services.AddSingleton(_maintenanceSchedulerMock.Object);
            services.AddSingleton(_bulkMock.Object);
            services.AddSingleton(_supportMock.Object);
            services.AddSingleton(_supportSessionMock.Object);
            services.AddSingleton(_fileMock.Object);
            services.AddSingleton(_transferMock.Object);
            services.AddSingleton(_healthMonitorMock.Object);
            services.AddSingleton(_resourceMonitorMock.Object);
            services.AddSingleton(_eventDispatcherMock.Object);
            services.AddSingleton(_cryptographyServiceMock.Object);
            services.AddSingleton(_auditLoggerMock.Object);

            _serviceProvider = services.BuildServiceProvider();

            _authService = _serviceProvider.GetRequiredService<IAuthenticationService>();
            _authzService = _serviceProvider.GetRequiredService<IAuthorizationService>();
            _apiService = _serviceProvider.GetRequiredService<IAdministrationApiService>();
            _coordinator = _serviceProvider.GetRequiredService<IEnterpriseManagementCoordinator>();
            _dashboard = _serviceProvider.GetRequiredService<IDashboardQueryService>();
            _audit = _serviceProvider.GetRequiredService<IAuditIntegrationService>();
        }

        #region 1. AUTHENTICATION TESTS
        [Fact]
        public async Task Authentication_ValidCredentials_GeneratesAndValidatesToken()
        {
            // Act: Validate admin user
            var user = await _authService.ValidateCredentialsAsync("admin", "AdminPassword123!");
            Assert.NotNull(user);
            Assert.Equal("admin-01", user.AdministratorId);
            Assert.Equal(AdminRole.SuperAdministrator, user.Role);

            // Act: Generate token
            var token = await _authService.GenerateTokenAsync(user);
            Assert.False(string.IsNullOrWhiteSpace(token));

            // Act: Validate token
            var validatedUser = await _authService.ValidateTokenAsync(token);
            Assert.NotNull(validatedUser);
            Assert.Equal(user.AdministratorId, validatedUser.AdministratorId);

            // Act: Invalidate/Logout token
            var loggedOut = await _authService.InvalidateTokenAsync(token);
            Assert.True(loggedOut);

            // Act: Validate again should fail
            var invalidUser = await _authService.ValidateTokenAsync(token);
            Assert.Null(invalidUser);
        }

        [Fact]
        public async Task Authentication_InvalidCredentials_FailsGracefully()
        {
            var user = await _authService.ValidateCredentialsAsync("admin", "WrongPassword!");
            Assert.Null(user);

            var nonExistent = await _authService.ValidateCredentialsAsync("hacker", "Password123!");
            Assert.Null(nonExistent);
        }
        #endregion

        #region 2. AUTHORIZATION TESTS
        [Theory]
        [InlineData(AdminRole.SuperAdministrator, AdminPermission.ViewMachine, true)]
        [InlineData(AdminRole.SuperAdministrator, AdminPermission.ManageFiles, true)]
        [InlineData(AdminRole.Operator, AdminPermission.ViewMachine, true)]
        [InlineData(AdminRole.Operator, AdminPermission.ManagePolicy, false)]
        [InlineData(AdminRole.Auditor, AdminPermission.ViewAudit, true)]
        [InlineData(AdminRole.Auditor, AdminPermission.ExecuteCommand, false)]
        public void Authorization_RoleBasedAccessControl_CorrectlyRestrictsPermissions(AdminRole role, AdminPermission permission, bool expected)
        {
            var user = new AdminUser { Role = role };
            var result = _authzService.HasPermission(user, permission);
            Assert.Equal(expected, result);
        }
        #endregion

        #region 3. INTEGRATION ENDPOINT TESTS
        [Fact]
        public async Task FleetAPI_GetMachines_ReturnsFilteredAndPagedResult()
        {
            // Arrange
            var machines = new List<MachineInfo>
            {
                new() { MachineId = "WS-01", Hostname = "A-Alpha", Status = MachineStatus.Online, HealthStatus = MachineHealthStatus.Healthy },
                new() { MachineId = "WS-02", Hostname = "B-Beta", Status = MachineStatus.Offline, HealthStatus = MachineHealthStatus.Unknown },
                new() { MachineId = "WS-03", Hostname = "C-Gamma", Status = MachineStatus.Online, HealthStatus = MachineHealthStatus.Healthy }
            };
            _fleetMock.Setup(f => f.GetAllMachinesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(machines);

            // Act: GET /api/fleet/machines?status=Online&sort=hostname&page=1&pageSize=1
            var responseJson = await _apiService.HandleApiRequestAsync("GET api/fleet/machines?status=Online&sort=hostname&page=1&pageSize=1", "{}");
            var result = JsonSerializer.Deserialize<JsonElement>(responseJson);

            // Assert
            Assert.Equal(2, result.GetProperty("total").GetInt32());
            Assert.Equal(1, result.GetProperty("page").GetInt32());
            Assert.Equal(1, result.GetProperty("pageSize").GetInt32());

            var items = result.GetProperty("items").EnumerateArray().ToList();
            Assert.Single(items);
            Assert.Equal("A-Alpha", items[0].GetProperty("Hostname").GetString());
        }

        [Fact]
        public async Task CommandAPI_Execute_DispatchesAndLogsAudit()
        {
            // Arrange
            var request = new RemoteCommandRequest
            {
                MachineId = "WS-01",
                Action = "LOCK_PC",
                Priority = "High",
                Signature = "ValidSignatureBytes",
                OperatorId = "admin-01"
            };
            var commandResult = new CommandResult { Outcome = OperationResult.Success, Status = CommandStatus.Succeeded, OutputMessage = "Machine Locked" };
            _commandMock.Setup(c => c.ExecuteCommandAsync(It.IsAny<RemoteCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(commandResult);

            var body = JsonSerializer.Serialize(request);

            // Act: POST /api/commands/execute
            var responseJson = await _apiService.HandleApiRequestAsync("POST api/commands/execute", body);
            var result = JsonSerializer.Deserialize<RemoteCommandResponse>(responseJson);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Succeeded", result.Status);
            Assert.Equal("Success", result.Outcome);

            // Check Audit Logs
            var logs = await _audit.QueryEntriesAsync("admin-01", null, null, 1, 10);
            Assert.NotEmpty(logs);
            Assert.Contains(logs, e => e.Description.Contains("LOCK_PC"));
        }

        [Fact]
        public async Task DiagnosticsAPI_Start_TriggersWorkstationReport()
        {
            // Arrange
            var request = new DiagnosticRequest { MachineId = "WS-01", ReportType = "Performance" };
            var report = new DiagnosticReport { ReportId = "R-99", MachineId = "WS-01", Category = DiagnosticReportType.Performance, ContentJson = "{\"score\": 100}" };

            _diagMock.Setup(d => d.GenerateReportAsync("WS-01", DiagnosticReportType.Performance, It.IsAny<CancellationToken>())).ReturnsAsync(report);

            var body = JsonSerializer.Serialize(request);

            // Act: POST /api/diagnostics/start
            var responseJson = await _apiService.HandleApiRequestAsync("POST api/diagnostics/start", body);
            var result = JsonSerializer.Deserialize<DiagnosticReport>(responseJson);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("R-99", result.ReportId);
            Assert.Equal("Performance", result.Category.ToString());
        }

        [Fact]
        public async Task PolicyAPI_Assign_AppliesPolicyAndPublishesNotification()
        {
            // Arrange
            var request = new PolicyAssignmentRequest { PolicyId = "POL-01", VersionTag = "1.0", TargetId = "G-01" };
            _policyAssignMock.Setup(p => p.AssignPolicyAsync("POL-01", "1.0", "G-01", It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var body = JsonSerializer.Serialize(request);

            // Act: POST /api/policies/assign
            var responseJson = await _apiService.HandleApiRequestAsync("POST api/policies/assign", body);
            var result = JsonSerializer.Deserialize<JsonElement>(responseJson);

            // Assert
            Assert.True(result.GetProperty("success").GetBoolean());
        }

        [Fact]
        public async Task BulkAPI_Start_LaunchesMultiMachineOrchestration()
        {
            // Arrange
            var request = new BulkOperationRequest { Action = "RESTART", MachineIds = new List<string> { "WS-01", "WS-02" }, GroupIds = new List<string>(), OperatorId = "admin-01" };
            _bulkMock.Setup(b => b.StartBulkOperationAsync(It.IsAny<BulkOperation>(), It.IsAny<CancellationToken>())).ReturnsAsync("BULK-123");

            var body = JsonSerializer.Serialize(request);

            // Act: POST /api/bulk
            var responseJson = await _apiService.HandleApiRequestAsync("POST api/bulk", body);
            var result = JsonSerializer.Deserialize<BulkOperationResponse>(responseJson);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("BULK-123", result.BulkOperationId);
            Assert.Equal("Running", result.Status);
            Assert.Equal(2, result.TotalTargets);
        }

        [Fact]
        public async Task RemoteSupportAPI_Create_RequestsSessionHandshake()
        {
            // Arrange
            var request = new RemoteSupportRequest { MachineId = "WS-01", SessionType = "TerminalOnly", RequestedPermission = "FullControl" };
            var session = new RemoteSession { SessionId = "S-77", TargetMachineId = "WS-01", ConnectionType = SupportSessionType.TerminalOnly, Status = RemoteSessionStatus.Requested };

            _supportMock.Setup(s => s.RequestSupportSessionAsync("WS-01", SupportSessionType.TerminalOnly, It.IsAny<CancellationToken>())).ReturnsAsync(session);

            var body = JsonSerializer.Serialize(request);

            // Act: POST /api/support/sessions
            var responseJson = await _apiService.HandleApiRequestAsync("POST api/support/sessions", body);
            var result = JsonSerializer.Deserialize<RemoteSession>(responseJson);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("S-77", result.SessionId);
            Assert.Equal("Requested", result.Status.ToString());
        }
        #endregion

        #region 4. SECURITY & SANITIZATION TESTS
        [Fact]
        public async Task SecurityMiddleware_SuspiciousQueryScript_BlocksWithBadRequest()
        {
            // Act: Request with custom script tags in URL query
            var responseJson = await _apiService.HandleApiRequestAsync("GET api/fleet/machines?search=<script>alert(1)</script>", "{}");

            // Assert
            Assert.Contains("Suspicious input patterns detected", responseJson);
        }

        [Fact]
        public async Task SecurityMiddleware_SqlInjectionPattern_BlocksWithBadRequest()
        {
            // Act: Request with SQL Injection string in query params
            var responseJson = await _apiService.HandleApiRequestAsync("GET api/fleet/machines?id=1 OR 1=1", "{}");

            // Assert
            Assert.Contains("Suspicious input patterns detected", responseJson);
        }
        #endregion

        #region 5. PERFORMANCE & STRESS TESTS
        [Fact]
        public async Task Performance_ConcurrentRequests_ResolvesBulkOperationsSwiftly()
        {
            // Arrange
            _fleetMock.Setup(f => f.GetAllMachinesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<MachineInfo>());

            // Act: run 200 concurrent requests
            var tasks = new List<Task<string>>();
            for (int i = 0; i < 200; i++)
            {
                tasks.Add(_apiService.HandleApiRequestAsync("GET api/fleet/machines", "{}"));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(200, results.Length);
            Assert.All(results, r => Assert.Contains("total", r));
        }
        #endregion
    }
}

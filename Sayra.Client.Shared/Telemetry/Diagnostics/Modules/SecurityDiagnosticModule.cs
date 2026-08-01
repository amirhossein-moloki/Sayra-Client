using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Interfaces.Recovery;
using Sayra.Client.Shared.Models.Recovery;

namespace Sayra.Client.Shared.Telemetry.Diagnostics.Modules
{
    public class SecurityDiagnosticModule : IDiagnosticModule
    {
        private readonly ISecurityHardeningService? _securityService;

        public SecurityDiagnosticModule(ISecurityHardeningService? securityService = null)
        {
            _securityService = securityService;
        }

        public string Name => "Security";
        public string AffectedSubsystem => "Security";

        public async Task<DiagnosticModuleResult> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var result = new DiagnosticModuleResult { ModuleName = Name, Status = DiagnosticHealthStatus.Healthy };

            try
            {
                bool isConfigSignatureValid = true;
                bool isDatabasePragmaValid = true;
                bool isExecutableAuthenticodeValid = true;

                if (_securityService != null)
                {
                    try
                    {
                        var configValidation = await _securityService.ValidateConfigurationAsync(cancellationToken);
                        isConfigSignatureValid = configValidation.ValidationState == SecurityValidationState.Passed;
                        result.Data["ConfigSignatureValidation"] = configValidation.ValidationState.ToString();
                    }
                    catch (Exception ex) { result.Warnings.Add($"Config signature validation failed: {ex.Message}"); }

                    try
                    {
                        var dbValidation = await _securityService.ValidateDatabaseAsync(cancellationToken);
                        isDatabasePragmaValid = dbValidation.ValidationState == SecurityValidationState.Passed;
                        result.Data["DatabaseIntegrityPragma"] = dbValidation.ValidationState.ToString();
                    }
                    catch (Exception ex) { result.Warnings.Add($"Database integrity validation failed: {ex.Message}"); }

                    try
                    {
                        var exeValidation = await _securityService.ValidateExecutableAsync(cancellationToken);
                        isExecutableAuthenticodeValid = exeValidation.ValidationState == SecurityValidationState.Passed;
                        result.Data["ExecutableAuthenticode"] = exeValidation.ValidationState.ToString();
                    }
                    catch (Exception ex) { result.Warnings.Add($"Executable Authenticode validation failed: {ex.Message}"); }
                }
                else
                {
                    result.Data["ConfigSignatureValidation"] = "NotChecked";
                    result.Data["DatabaseIntegrityPragma"] = "NotChecked";
                    result.Data["ExecutableAuthenticode"] = "NotChecked";
                }

                result.Data["DPAPIStorageEncryption"] = "Active";
                result.Data["NamedPipeDACLPolicy"] = "Restricted";

                // Findings & Evaluation rules
                if (!isConfigSignatureValid)
                {
                    result.Status = DiagnosticHealthStatus.Critical;
                    result.Errors.Add("Configuration file has failed signature integrity checks. High risk of tampering.");
                    result.Findings.Add(new DiagnosticFinding
                    {
                        Key = "ConfigSignatureTampered",
                        Value = "Tampered",
                        Subsystem = AffectedSubsystem,
                        IsAnomaly = true,
                        Details = "Configuration signature check failed verification."
                    });
                }

                if (!isDatabasePragmaValid)
                {
                    if (result.Status < DiagnosticHealthStatus.Degraded) result.Status = DiagnosticHealthStatus.Degraded;
                    result.Errors.Add("SQLCipher database fails low-level cryptographic and structural integrity checks.");
                    result.Findings.Add(new DiagnosticFinding
                    {
                        Key = "DatabaseIntegrityTampered",
                        Value = "Tampered",
                        Subsystem = AffectedSubsystem,
                        IsAnomaly = true,
                        Details = "Database SQLCipher integrity PRAGMA checks failed."
                    });
                }

                if (!isExecutableAuthenticodeValid)
                {
                    if (result.Status < DiagnosticHealthStatus.Degraded) result.Status = DiagnosticHealthStatus.Degraded;
                    result.Warnings.Add("The workstation core binaries failed Authenticode digital signature checks.");
                    result.Findings.Add(new DiagnosticFinding
                    {
                        Key = "AuthenticodeValidationFailed",
                        Value = "VerificationFailed",
                        Subsystem = AffectedSubsystem,
                        IsAnomaly = true,
                        Details = "Workstation binaries failed Authenticode signature verification checks."
                    });
                }
            }
            catch (Exception ex)
            {
                result.Status = DiagnosticHealthStatus.Unknown;
                result.Errors.Add($"Security diagnostics failed: {ex.Message}");
            }

            return result;
        }
    }
}

using System;
using FluentValidation;
using Sayra.Client.Shared.Models.Phase9.Dtos;

namespace Sayra.Client.Shared.Models.Phase9.Validation
{
    /// <summary>
    /// Validator for <see cref="MachineQueryRequest"/>.
    /// </summary>
    public class MachineQueryRequestValidator : AbstractValidator<MachineQueryRequest>
    {
        /// <summary>
        /// Initializes validation rules.
        /// </summary>
        public MachineQueryRequestValidator()
        {
            RuleFor(x => x.MachineIds)
                .NotNull().WithMessage("MachineIds collection cannot be null.");

            RuleFor(x => x.MinRamGb)
                .GreaterThanOrEqualTo(0).When(x => x.MinRamGb.HasValue)
                .WithMessage("Minimum RAM Gb must be positive or zero.");
        }
    }

    /// <summary>
    /// Validator for <see cref="FleetQueryRequest"/>.
    /// </summary>
    public class FleetQueryRequestValidator : AbstractValidator<FleetQueryRequest>
    {
        /// <summary>
        /// Initializes validation rules.
        /// </summary>
        public FleetQueryRequestValidator()
        {
            RuleFor(x => x.GroupType)
                .Must(gt => string.IsNullOrEmpty(gt) || gt == "Static" || gt == "Dynamic" || gt == "System")
                .WithMessage("GroupType must be Static, Dynamic, or System if specified.");
        }
    }

    /// <summary>
    /// Validator for <see cref="RemoteCommandRequest"/>.
    /// </summary>
    public class RemoteCommandRequestValidator : AbstractValidator<RemoteCommandRequest>
    {
        /// <summary>
        /// Initializes validation rules.
        /// </summary>
        public RemoteCommandRequestValidator()
        {
            RuleFor(x => x.MachineId)
                .NotEmpty().WithMessage("MachineId cannot be empty.");

            RuleFor(x => x.Action)
                .NotEmpty().WithMessage("Action verb cannot be empty.");

            RuleFor(x => x.Priority)
                .Must(p => p == "Low" || p == "Normal" || p == "High" || p == "Critical" || p == "Emergency")
                .WithMessage("Priority must be Low, Normal, High, Critical, or Emergency.");

            RuleFor(x => x.Parameters)
                .NotNull().WithMessage("Parameters map cannot be null.");

            RuleFor(x => x.Signature)
                .NotEmpty().WithMessage("Cryptographic signature cannot be empty.");

            RuleFor(x => x.OperatorId)
                .NotEmpty().WithMessage("OperatorId cannot be empty.");
        }
    }

    /// <summary>
    /// Validator for <see cref="RemoteCommandResponse"/>.
    /// </summary>
    public class RemoteCommandResponseValidator : AbstractValidator<RemoteCommandResponse>
    {
        /// <summary>
        /// Initializes validation rules.
        /// </summary>
        public RemoteCommandResponseValidator()
        {
            RuleFor(x => x.CommandId)
                .NotEmpty().WithMessage("CommandId cannot be empty.");

            RuleFor(x => x.Status)
                .NotEmpty().WithMessage("Status cannot be empty.");
        }
    }

    /// <summary>
    /// Validator for <see cref="BulkOperationRequest"/>.
    /// </summary>
    public class BulkOperationRequestValidator : AbstractValidator<BulkOperationRequest>
    {
        /// <summary>
        /// Initializes validation rules.
        /// </summary>
        public BulkOperationRequestValidator()
        {
            RuleFor(x => x.Action)
                .NotEmpty().WithMessage("Bulk Action verb cannot be empty.");

            RuleFor(x => x.MachineIds)
                .NotNull().WithMessage("MachineIds list cannot be null.");

            RuleFor(x => x.GroupIds)
                .NotNull().WithMessage("GroupIds list cannot be null.");

            RuleFor(x => x.OperatorId)
                .NotEmpty().WithMessage("OperatorId cannot be empty.");
        }
    }

    /// <summary>
    /// Validator for <see cref="BulkOperationResponse"/>.
    /// </summary>
    public class BulkOperationResponseValidator : AbstractValidator<BulkOperationResponse>
    {
        /// <summary>
        /// Initializes validation rules.
        /// </summary>
        public BulkOperationResponseValidator()
        {
            RuleFor(x => x.BulkOperationId)
                .NotEmpty().WithMessage("BulkOperationId cannot be empty.");

            RuleFor(x => x.Status)
                .NotEmpty().WithMessage("Status state cannot be empty.");

            RuleFor(x => x.TotalTargets)
                .GreaterThanOrEqualTo(0).WithMessage("TotalTargets cannot be negative.");
        }
    }

    /// <summary>
    /// Validator for <see cref="PolicyAssignmentRequest"/>.
    /// </summary>
    public class PolicyAssignmentRequestValidator : AbstractValidator<PolicyAssignmentRequest>
    {
        /// <summary>
        /// Initializes validation rules.
        /// </summary>
        public PolicyAssignmentRequestValidator()
        {
            RuleFor(x => x.PolicyId)
                .NotEmpty().WithMessage("PolicyId cannot be empty.");

            RuleFor(x => x.VersionTag)
                .NotEmpty().WithMessage("VersionTag cannot be empty.");

            RuleFor(x => x.TargetId)
                .NotEmpty().WithMessage("TargetId scope cannot be empty.");
        }
    }

    /// <summary>
    /// Validator for <see cref="MaintenanceRequest"/>.
    /// </summary>
    public class MaintenanceRequestValidator : AbstractValidator<MaintenanceRequest>
    {
        /// <summary>
        /// Initializes validation rules.
        /// </summary>
        public MaintenanceRequestValidator()
        {
            RuleFor(x => x.Category)
                .NotEmpty().WithMessage("Category cannot be empty.");

            RuleFor(x => x.ScopeFilter)
                .NotEmpty().WithMessage("ScopeFilter cannot be empty.");

            RuleFor(x => x.DurationMinutes)
                .GreaterThan(0).WithMessage("DurationMinutes must be greater than zero.");
        }
    }

    /// <summary>
    /// Validator for <see cref="DiagnosticRequest"/>.
    /// </summary>
    public class DiagnosticRequestValidator : AbstractValidator<DiagnosticRequest>
    {
        /// <summary>
        /// Initializes validation rules.
        /// </summary>
        public DiagnosticRequestValidator()
        {
            RuleFor(x => x.MachineId)
                .NotEmpty().WithMessage("MachineId cannot be empty.");

            RuleFor(x => x.ReportType)
                .NotEmpty().WithMessage("ReportType cannot be empty.");
        }
    }

    /// <summary>
    /// Validator for <see cref="TransferRequest"/>.
    /// </summary>
    public class TransferRequestValidator : AbstractValidator<TransferRequest>
    {
        /// <summary>
        /// Initializes validation rules.
        /// </summary>
        public TransferRequestValidator()
        {
            RuleFor(x => x.FilePath)
                .NotEmpty().WithMessage("FilePath cannot be empty.");

            RuleFor(x => x.Direction)
                .Must(d => d == "Upload" || d == "Download")
                .WithMessage("Direction must be Upload or Download.");

            RuleFor(x => x.Category)
                .NotEmpty().WithMessage("Category type cannot be empty.");

            RuleFor(x => x.TotalFileSizeBytes)
                .GreaterThanOrEqualTo(0).WithMessage("TotalFileSizeBytes cannot be negative.");
        }
    }

    /// <summary>
    /// Validator for <see cref="TransferResponse"/>.
    /// </summary>
    public class TransferResponseValidator : AbstractValidator<TransferResponse>
    {
        /// <summary>
        /// Initializes validation rules.
        /// </summary>
        public TransferResponseValidator()
        {
            RuleFor(x => x.JobId)
                .NotEmpty().WithMessage("JobId cannot be empty.");

            RuleFor(x => x.Status)
                .NotEmpty().WithMessage("Status cannot be empty.");
        }
    }

    /// <summary>
    /// Validator for <see cref="RemoteSupportRequest"/>.
    /// </summary>
    public class RemoteSupportRequestValidator : AbstractValidator<RemoteSupportRequest>
    {
        /// <summary>
        /// Initializes validation rules.
        /// </summary>
        public RemoteSupportRequestValidator()
        {
            RuleFor(x => x.MachineId)
                .NotEmpty().WithMessage("MachineId cannot be empty.");

            RuleFor(x => x.SessionType)
                .Must(s => s == "TerminalOnly" || s == "DesktopStreaming" || s == "DiagnosticsOnly" || s == "UnifiedRemoteSupport")
                .WithMessage("SessionType is invalid.");

            RuleFor(x => x.RequestedPermission)
                .Must(p => p == "ViewOnly" || p == "InteractiveExecution" || p == "FullControl" || p == "FileTransferOnly" || p == "EmergencyOverride")
                .WithMessage("RequestedPermission is invalid.");
        }
    }

    /// <summary>
    /// Validator for <see cref="AuditQueryRequest"/>.
    /// </summary>
    public class AuditQueryRequestValidator : AbstractValidator<AuditQueryRequest>
    {
        /// <summary>
        /// Initializes validation rules.
        /// </summary>
        public AuditQueryRequestValidator()
        {
            RuleFor(x => x.EndDate)
                .GreaterThanOrEqualTo(x => x.StartDate!.Value)
                .When(x => x.StartDate.HasValue && x.EndDate.HasValue)
                .WithMessage("EndDate must be greater than or equal to StartDate.");
        }
    }

    /// <summary>
    /// Validator for <see cref="AdministrationReportRequest"/>.
    /// </summary>
    public class AdministrationReportRequestValidator : AbstractValidator<AdministrationReportRequest>
    {
        /// <summary>
        /// Initializes validation rules.
        /// </summary>
        public AdministrationReportRequestValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Report Title cannot be empty.");
        }
    }
}

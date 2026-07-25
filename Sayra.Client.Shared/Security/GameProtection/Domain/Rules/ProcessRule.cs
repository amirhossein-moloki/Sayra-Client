using System;

namespace Sayra.Client.Shared.Security.GameProtection.Domain.Rules;

public class ProcessRule
{
    public string ProcessName { get; set; } = string.Empty;
    public string PathPattern { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public ProcessAction Action { get; set; } = ProcessAction.Report;
    public string Severity { get; set; } = "Low";
}

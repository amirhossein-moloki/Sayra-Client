using Sayra.Client.Shared.Security.GameProtection.Domain.Models;

namespace Sayra.Client.Shared.Security.GameProtection.Application.Interfaces;

public interface IProcessPolicyEvaluator
{
    SecurityDecision Evaluate(ProcessInfo process);
}

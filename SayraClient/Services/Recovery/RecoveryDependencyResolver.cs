using System;
using System.Linq;
using System.Threading.Tasks;
using Sayra.Client.Shared.Interfaces.Recovery;
using Sayra.Client.Shared.Models.Recovery;
using Sayra.Client.Shared.Models.Recovery.Policies;

namespace SayraClient.Services.Recovery
{
    public enum DependencyStatus
    {
        Healthy,
        Blocked,
        FailClosed
    }

    public class DependencyValidationResult
    {
        public DependencyStatus Status { get; init; }
        public string? BlockedBySubsystem { get; init; }
    }

    public class RecoveryDependencyResolver
    {
        public DependencyValidationResult ValidateDependencies(string subsystemName, DependencyPolicy policy, IHealthMonitor healthMonitor)
        {
            if (policy?.PreRecoveryDependencies == null || policy.PreRecoveryDependencies.Count == 0)
            {
                return new DependencyValidationResult { Status = DependencyStatus.Healthy };
            }

            foreach (var dep in policy.PreRecoveryDependencies)
            {
                var depState = healthMonitor.GetSubsystemHealth(dep);
                if (depState != SubsystemHealthState.Healthy)
                {
                    if (policy.FailClosedOnDependencyFailure)
                    {
                        return new DependencyValidationResult
                        {
                            Status = DependencyStatus.FailClosed,
                            BlockedBySubsystem = dep
                        };
                    }
                    else
                    {
                        return new DependencyValidationResult
                        {
                            Status = DependencyStatus.Blocked,
                            BlockedBySubsystem = dep
                        };
                    }
                }
            }

            return new DependencyValidationResult { Status = DependencyStatus.Healthy };
        }
    }
}

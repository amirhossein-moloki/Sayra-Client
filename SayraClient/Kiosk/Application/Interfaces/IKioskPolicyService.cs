using SayraClient.Kiosk.Domain.Models;

namespace SayraClient.Kiosk.Application.Interfaces;

public interface IKioskPolicyService
{
    KioskPolicy GetCurrentPolicy();
    void UpdatePolicy(KioskPolicy policy);
    bool IsRestrictionEnabled(RestrictionType restrictionType);
    void ApplyPolicy(KioskPolicy policy);
}

using Sayra.Client.Authentication.Enums;

namespace Sayra.Client.Authentication.Contracts
{
    public interface IAuthorizationService
    {
        bool HasPermission(UserPermission permission);
        bool HasRole(UserRole role);
        bool CanAccess(string resource);
        bool CanExecute(string action);
    }
}

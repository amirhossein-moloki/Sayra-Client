namespace Sayra.Client.Authentication.Enums
{
    public enum AuthenticationState
    {
        Unauthenticated,
        Authenticating,
        Authenticated,
        Locked,
        Expired,
        Offline,
        SynchronizationRequired
    }
}

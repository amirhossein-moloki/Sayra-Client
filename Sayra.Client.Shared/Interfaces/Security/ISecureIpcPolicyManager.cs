namespace Sayra.Client.Shared.Interfaces.Security;

/// <summary>
/// Controls access policies and DACL security descriptors for Named Pipes.
/// </summary>
public interface ISecureIpcPolicyManager
{
    void CreateSecurePolicy();
    bool ValidateClient(int callerPid);
    bool ValidateCallerIdentity(int callerPid);

    void ApplyNamedPipeDacl(string pipeName);
    bool ValidatePipeCallerSid(int callerPid);
}

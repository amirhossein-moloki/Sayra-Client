using System.IO.Pipes;
using Sayra.Client.Shared.Ipc;

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

    // Track 4 Security Hardening Extensions
    PipeSecurity GetSecurePipeSecurity();
    bool ValidateIdentity(NamedPipeServerStream stream, out string clientName, out string clientSid);
    bool ValidateSession(int callerPid);
    bool ValidateProcess(int callerPid, out string imagePath);
    bool ValidateMessage(IpcMessage message);

    // Handshake Validation and Session Management
    bool ProcessHandshake(string streamId, IpcMessage message, int callerPid, out string errorMsg);
    bool IsHandshaken(string streamId);
    void RemoveStream(string streamId);
}

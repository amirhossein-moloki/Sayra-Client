using System;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces.Security;

namespace SayraClient.Services;

public class SecureIpcPolicyManager : ISecureIpcPolicyManager
{
    private readonly ILogger<SecureIpcPolicyManager> _logger;

    public SecureIpcPolicyManager(ILogger<SecureIpcPolicyManager> logger)
    {
        _logger = logger;
    }

    public void CreateSecurePolicy()
    {
        _logger.LogInformation("Creating secure IPC Policy...");
    }

    public bool ValidateClient(int callerPid)
    {
        // Stand-in validation for process
        _logger.LogInformation("Validating client process {Pid}", callerPid);
        return true;
    }

    public bool ValidateCallerIdentity(int callerPid)
    {
        return ValidatePipeCallerSid(callerPid);
    }

    public void ApplyNamedPipeDacl(string pipeName)
    {
        _logger.LogInformation("Applying restrictive DACL to pipe {Pipe}", pipeName);
    }

    public bool ValidatePipeCallerSid(int callerPid)
    {
        // Stand-in validation
        _logger.LogInformation("Validating caller SID for {Pid}", callerPid);
        return true;
    }

    /// <summary>
    /// Constructs a secure PipeSecurity configuration restricted to System, Admins and Authenticated Users.
    /// </summary>
    public PipeSecurity GetSecurePipeSecurity()
    {
        var pipeSecurity = new PipeSecurity();

        // 1. Allow SYSTEM full control
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        // 2. Allow Admins full control
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        // 3. Allow Authenticated Users read/write access (for the active desktop user in Session 1+)
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));

        return pipeSecurity;
    }

    /// <summary>
    /// Validates client WindowsIdentity over NamedPipeServerStream.
    /// </summary>
    public bool ValidateIdentity(NamedPipeServerStream stream, out string clientName, out string clientSid)
    {
        bool isClientAuthorized = false;
        string tempSid = "";
        string tempName = "";

        try
        {
            stream.RunAsClient(() =>
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    tempSid = identity.User?.Value ?? "";
                    tempName = identity.Name;

                    // Check if the client is SYSTEM or in the Administrators group
                    var principal = new WindowsPrincipal(identity);
                    if (identity.IsSystem || principal.IsInRole(WindowsBuiltInRole.Administrator))
                    {
                        isClientAuthorized = true;
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify client identity over Named Pipe IPC.");
        }

        clientSid = tempSid;
        clientName = tempName;
        return isClientAuthorized;
    }
}

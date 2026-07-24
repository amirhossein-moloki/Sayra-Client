using System;

namespace Sayra.Client.Shared.Interfaces.Security;

/// <summary>
/// Validates executable integrity and dynamic signature chains.
/// </summary>
public interface IIntegrityValidator
{
    string GenerateSignature(string data, DateTime timestamp);
    bool VerifySignature(string data, DateTime timestamp, string signature);
    bool VerifyFileIntegrity(string filepath, string expectedHash);

    bool ValidateFile(string filePath, string expectedHash);
    bool ValidateProcess(int processId);
    bool VerifyIntegrity();

    bool VerifyAuthenticodeSignature(string filePath);
    string ComputeSha256Hash(string filePath);
    bool ValidateDllIntegrity(string dllName);
}

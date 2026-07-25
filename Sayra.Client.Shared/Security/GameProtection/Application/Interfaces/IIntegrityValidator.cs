using Sayra.Client.Shared.Security.GameProtection.Domain.Models;

namespace Sayra.Client.Shared.Security.GameProtection.Application.Interfaces;

public interface IIntegrityValidator
{
    IntegrityResult ValidateExecutable(string filePath, string expectedHash, string expectedPublisher = "");
}

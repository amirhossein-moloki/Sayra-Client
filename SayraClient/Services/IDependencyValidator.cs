using System.Threading;
using System.Threading.Tasks;

namespace SayraClient.Services;

/// <summary>
/// Validates environmental, operational, configurations, and directory-level dependencies before execution.
/// </summary>
public interface IDependencyValidator
{
    /// <summary>
    /// Checks critical runtime dependencies (directories, system privileges, OS capabilities).
    /// </summary>
    Task ValidateDependenciesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Validates application configuration files, checking schemas, range safety, and signatures.
    /// </summary>
    Task ValidateConfigurationAsync(CancellationToken cancellationToken);
}

using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Shared.Interfaces
{
    public interface IDiagnosticsEngine
    {
        Task<SystemDiagnosticsReport> GenerateFullReportAsync(CancellationToken cancellationToken = default);
    }
}

using System;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Shared.Interfaces
{
    public interface ICommandResultReporter
    {
        Task ReportResultAsync(CommandResult result);
        Task SendStatusUpdateAsync(Guid commandId, CommandStatus status);
    }
}

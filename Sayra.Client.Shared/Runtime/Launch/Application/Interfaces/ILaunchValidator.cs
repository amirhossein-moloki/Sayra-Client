using System.Threading.Tasks;
using Sayra.Client.Shared.Runtime.Launch.Domain.Models;

namespace Sayra.Client.Shared.Runtime.Launch.Application.Interfaces
{
    public interface ILaunchValidator
    {
        Task ValidateAsync(LaunchRequest request, LaunchProfile profile);
    }
}

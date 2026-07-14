using Microsoft.Extensions.DependencyInjection;
using Sayra.Client.GameLibrary.Persistence;
using Sayra.Client.GameLibrary.Services;

namespace Sayra.Client.GameLibrary
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddGameLibrary(this IServiceCollection services, string? basePath = null)
        {
            services.AddSingleton<IGameLibraryRepository>(sp =>
                new GameLibraryRepository(basePath, sp.GetService<Microsoft.Extensions.Logging.ILogger<GameLibraryRepository>>()));
            services.AddSingleton<IGameLibraryService, GameLibraryService>();
            return services;
        }
    }
}

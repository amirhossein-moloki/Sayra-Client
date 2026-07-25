using System;
using Sayra.Client.Shared.Runtime.Domain.Entities;
using Sayra.Client.Shared.Runtime.Application.Interfaces;

namespace Sayra.Client.Shared.Runtime.Application.Services
{
    public class RuntimeContextProvider : IRuntimeContextProvider
    {
        private GameRuntimeContext? _context;
        private readonly object _lock = new();

        public GameRuntimeContext GetContext()
        {
            lock (_lock)
            {
                if (_context == null)
                {
                    _context = new GameRuntimeContext
                    {
                        GameIdentifier = "DefaultGame",
                        ExecutablePath = "C:\\Games\\DefaultGame\\game.exe",
                        ProcessId = null,
                        SessionId = Guid.Empty,
                        LaunchArguments = ""
                    };
                }
                return _context;
            }
        }

        public void SetContext(GameRuntimeContext context)
        {
            lock (_lock)
            {
                _context = context ?? throw new ArgumentNullException(nameof(context));
            }
        }
    }
}

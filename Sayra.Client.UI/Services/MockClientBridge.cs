using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace Sayra.Client.UI.Services
{
    public class MockClientBridge : IClientBridge
    {
        private readonly BehaviorSubject<ClientState> _stateSubject;
        private readonly List<AppModel> _mockApps;

        public MockClientBridge()
        {
            _stateSubject = new BehaviorSubject<ClientState>(new ClientState
            {
                Status = ClientStatus.Connected,
                SessionState = SessionState.Idle,
                RemainingTime = TimeSpan.FromMinutes(60),
                UserName = "Guest"
            });

            _mockApps = new List<AppModel>
            {
                new AppModel { Id = "1", Name = "League of Legends", Category = "MOBA", Description = "Battle in the Arena", IsFavorite = true },
                new AppModel { Id = "2", Name = "Counter-Strike 2", Category = "FPS", Description = "Tactical Shooter" },
                new AppModel { Id = "3", Name = "Dota 2", Category = "MOBA", Description = "Deep Strategy" },
                new AppModel { Id = "4", Name = "Valorant", Category = "FPS", Description = "Character-based Shooter", IsFavorite = true },
                new AppModel { Id = "5", Name = "Cyberpunk 2077", Category = "RPG", Description = "Open World RPG" },
                new AppModel { Id = "6", Name = "Minecraft", Category = "Sandbox", Description = "Build Anything" }
            };
        }

        public Task<ClientState> GetState() => Task.FromResult(_stateSubject.Value);

        public Task SendCommand(string action, object? parameters = null)
        {
            Console.WriteLine($"[MockIPC] Command Sent: {action}");
            if (action == "LOGIN")
            {
                _stateSubject.OnNext(new ClientState
                {
                    Status = ClientStatus.Connected,
                    SessionState = SessionState.Active,
                    RemainingTime = TimeSpan.FromHours(2),
                    UserName = "ProPlayer99"
                });
            }
            return Task.CompletedTask;
        }

        public IObservable<ClientState> SubscribeToStateChanged() => _stateSubject;

        public Task<IEnumerable<AppModel>> GetApplications() => Task.FromResult<IEnumerable<AppModel>>(_mockApps);
    }
}

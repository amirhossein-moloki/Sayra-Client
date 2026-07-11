using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Sayra.UI.Models;

namespace Sayra.UI.Services
{
    public class MockGameService
    {
        public async Task<ObservableCollection<GameItem>> GetGamesAsync()
        {
            // Simulate realistic API latency
            await Task.Delay(150);

            var games = new ObservableCollection<GameItem>
            {
                new GameItem
                {
                    Id = "1",
                    Title = "Cyberpunk 2077",
                    Genre = "RPG",
                    ImagePath = "Assets/Games/cyberpunk2077.jpg",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "Open-world, action-adventure story in Night City."
                },
                new GameItem
                {
                    Id = "2",
                    Title = "Elden Ring",
                    Genre = "Action RPG",
                    ImagePath = "Assets/Games/eldenring.jpg",
                    Status = "Installed",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "Rise, Tarnished, and be guided by grace to brandish the power of the Elden Ring."
                },
                new GameItem
                {
                    Id = "3",
                    Title = "Counter-Strike 2",
                    Genre = "Tactical Shooter",
                    ImagePath = "Assets/Games/cs2.jpg",
                    Status = "Currently Playing",
                    IsAvailable = true,
                    IsSelected = true, // Start with CS2 currently highlighted & selected
                    Description = "For over two decades, Counter-Strike has offered an elite competitive experience."
                },
                new GameItem
                {
                    Id = "4",
                    Title = "Red Dead Redemption 2",
                    Genre = "Action-Adventure",
                    ImagePath = "Assets/Games/rdr2.jpg",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "Arthur Morgan and the Van der Linde gang are outlaws on the run."
                },
                new GameItem
                {
                    Id = "5",
                    Title = "GTA V",
                    Genre = "Action-Adventure",
                    ImagePath = "Assets/Games/gta5.jpg",
                    Status = "Installed",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "A young street hustler, a retired bank robber and a terrifying psychopath."
                },
                new GameItem
                {
                    Id = "6",
                    Title = "Forza Horizon 5",
                    Genre = "Racing",
                    ImagePath = "Assets/Games/forza5.jpg",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "Your Ultimate Horizon Adventure awaits! Explore the vibrant open landscapes of Mexico."
                },
                new GameItem
                {
                    Id = "7",
                    Title = "Apex Legends",
                    Genre = "Battle Royale",
                    ImagePath = "Assets/Games/apex.jpg",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "Conquer with character in Apex Legends, a free-to-play Hero shooter."
                },
                new GameItem
                {
                    Id = "8",
                    Title = "The Witcher 3",
                    Genre = "RPG",
                    ImagePath = "Assets/Games/witcher3.jpg",
                    Status = "Locked",
                    IsAvailable = false, // Disabled & Locked overlay appearance
                    IsSelected = false,
                    Description = "You are Geralt of Rivia, mercenary monster slayer."
                },
                new GameItem
                {
                    Id = "9",
                    Title = "Dota 2",
                    Genre = "MOBA",
                    ImagePath = "Assets/Games/dota2.jpg",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "Every day, millions of players worldwide enter battle as one of over a hundred Dota heroes."
                },
                new GameItem
                {
                    Id = "10",
                    Title = "PUBG: BATTLEGROUNDS",
                    Genre = "Battle Royale",
                    ImagePath = "Assets/Games/pubg.jpg",
                    Status = "Unavailable",
                    IsAvailable = false, // Disabled appearance
                    IsSelected = false,
                    Description = "Land on strategic locations, loot weapons and survive to be the last standing."
                },
                new GameItem
                {
                    Id = "11",
                    Title = "Baldur's Gate 3",
                    Genre = "RPG",
                    ImagePath = "Assets/Games/baldursgate3.jpg",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "Gather your party, and return to the Forgotten Realms in a tale of fellowship."
                },
                new GameItem
                {
                    Id = "12",
                    Title = "Rust",
                    Genre = "Survival",
                    ImagePath = "Assets/Games/rust.jpg",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "The only aim in Rust is to survive - overcome struggles such as hunger, thirst and cold."
                }
            };

            return games;
        }
    }
}

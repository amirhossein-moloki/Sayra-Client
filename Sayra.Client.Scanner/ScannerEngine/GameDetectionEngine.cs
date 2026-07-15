using System;
using System.IO;
using Sayra.Client.Scanner.Models;
using Sayra.Client.Scanner.Providers;

namespace Sayra.Client.Scanner.ScannerEngine
{
    public interface IGameDetectionEngine
    {
        ClassificationResult Classify(ExecutableMetadata metadata);
    }

    public class ClassificationResult
    {
        public string Type { get; set; } = "Application"; // "Game", "Application", "Launcher", "Ignore"
        public string Launcher { get; set; } = "None"; // "Steam", "Epic Games", "Riot Games", "GOG Galaxy", etc.
        public string Category { get; set; } = "General";
        public string DisplayName { get; set; } = string.Empty;
        public int ConfidenceScore { get; set; } // 0 - 100
    }

    public class GameDetectionEngine : IGameDetectionEngine
    {
        private readonly IKnownGameDatabase _database;

        public GameDetectionEngine(IKnownGameDatabase database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public ClassificationResult Classify(ExecutableMetadata metadata)
        {
            var result = new ClassificationResult
            {
                Type = "Application",
                Launcher = "None",
                ConfidenceScore = 10,
                DisplayName = metadata.ProductName
            };

            string exeName = metadata.ExecutableName.ToLowerInvariant();
            string folderPath = metadata.WorkingDirectory.ToLowerInvariant();
            string publisher = metadata.Publisher.ToLowerInvariant();
            string company = metadata.Company.ToLowerInvariant();
            string productName = metadata.ProductName.ToLowerInvariant();

            // 1. Check database for specific match
            var signature = _database.GetSignature(exeName);
            if (signature != null)
            {
                result.Type = "Game";
                result.Launcher = signature.Launcher;
                result.Category = signature.Category;
                result.DisplayName = signature.DisplayName;
                result.ConfidenceScore = 100;
                return result;
            }

            // 2. Check if launcher itself
            if (_database.IsLauncher(exeName))
            {
                result.Type = "Launcher";
                result.ConfidenceScore = 100;
                return result;
            }

            // 3. Heuristic checks for launcher folder names
            bool isSteamFolder = folderPath.Contains(@"\steamapps\common\") || folderPath.Contains(@"/steamapps/common/") || folderPath.Contains("steam library") || folderPath.Contains("steamapps");
            bool isEpicFolder = folderPath.Contains(@"\epic games\") || folderPath.Contains(@"/epic games/");
            bool isRiotFolder = folderPath.Contains(@"\riot games\") || folderPath.Contains(@"/riot games/") || folderPath.Contains("valorant");
            bool isUbisoftFolder = folderPath.Contains(@"\ubisoft\") || folderPath.Contains(@"/ubisoft/");
            bool isEaFolder = folderPath.Contains(@"\ea desktop\") || folderPath.Contains(@"/ea desktop/") || folderPath.Contains(@"\origin games\");
            bool isGogFolder = folderPath.Contains(@"\gog galaxy\") || folderPath.Contains(@"/gog galaxy/");
            bool isXboxFolder = folderPath.Contains(@"\windowsapps\") || folderPath.Contains(@"/windowsapps/");

            if (isSteamFolder) { result.Launcher = "Steam"; result.ConfidenceScore += 40; }
            else if (isEpicFolder) { result.Launcher = "Epic Games"; result.ConfidenceScore += 40; }
            else if (isRiotFolder) { result.Launcher = "Riot Games"; result.ConfidenceScore += 50; }
            else if (isUbisoftFolder) { result.Launcher = "Ubisoft"; result.ConfidenceScore += 40; }
            else if (isEaFolder) { result.Launcher = "EA App"; result.ConfidenceScore += 40; }
            else if (isGogFolder) { result.Launcher = "GOG Galaxy"; result.ConfidenceScore += 40; }
            else if (isXboxFolder) { result.Launcher = "Xbox App"; result.ConfidenceScore += 40; }

            // Heuristics for publishers
            bool isGamePublisher = publisher.Contains("valve") || publisher.Contains("epic games") ||
                                    publisher.Contains("riot games") || publisher.Contains("blizzard") ||
                                    publisher.Contains("ubisoft") || publisher.Contains("electronic arts") ||
                                    publisher.Contains("rockstar") || publisher.Contains("cd projekt") ||
                                    publisher.Contains("activision") || publisher.Contains("bethesda") ||
                                    publisher.Contains("sega") || publisher.Contains("capcom") ||
                                    publisher.Contains("square enix") || publisher.Contains("nintendo") ||
                                    publisher.Contains("xbox") || publisher.Contains("playstation") ||
                                    company.Contains("valve") || company.Contains("riot games") || company.Contains("epic games");

            if (isGamePublisher)
            {
                result.ConfidenceScore += 40;
            }

            // Heuristics based on common executable/folder keywords indicating games
            if (folderPath.Contains(@"\games\") || folderPath.Contains(@"/games/") || folderPath.Contains(@"\spielt\") || folderPath.Contains(@"\game\"))
            {
                result.ConfidenceScore += 20;
            }

            // Final score evaluation
            if (result.ConfidenceScore >= 60)
            {
                result.Type = "Game";
                result.Category = "General Game";
            }
            else
            {
                result.Type = "Application";
                result.Category = "General Application";
            }

            // Cap confidence score
            result.ConfidenceScore = Math.Min(100, result.ConfidenceScore);

            return result;
        }
    }
}

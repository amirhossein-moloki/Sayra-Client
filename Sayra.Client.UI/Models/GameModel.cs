namespace Sayra.Client.UI.Models;

public enum GameState
{
    Available,
    Playing,
    Unavailable
}

public sealed class GameModel
{
    public string Title { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Cover { get; init; } = string.Empty;
    public GameState State { get; init; }
    public string ActionText => State switch
    {
        GameState.Playing => "Continue Playing",
        GameState.Unavailable => "Unavailable",
        _ => "PLAY"
    };
}

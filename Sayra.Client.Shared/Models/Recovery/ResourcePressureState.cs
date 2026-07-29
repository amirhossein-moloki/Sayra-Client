namespace Sayra.Client.Shared.Models.Recovery
{
    /// <summary>
    /// Represents the discrete tracked resource pressure states.
    /// </summary>
    public enum ResourcePressureState
    {
        /// <summary>
        /// Resource usage is within normal bounds.
        /// </summary>
        Normal,

        /// <summary>
        /// Resource usage has crossed warning thresholds.
        /// </summary>
        Warning,

        /// <summary>
        /// Resource usage has crossed critical thresholds.
        /// </summary>
        Critical,

        /// <summary>
        /// Resource usage has crossed emergency thresholds, risking workstation freeze or crash.
        /// </summary>
        Emergency
    }
}

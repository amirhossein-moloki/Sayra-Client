using System.Collections.Generic;

namespace Sayra.Client.Shared.Telemetry.Diagnostics
{
    /// <summary>
    /// Evaluates structural findings from diagnostic modules and compiles actionable optimization recommendations.
    /// </summary>
    public interface IDiagnosticsRecommendationEngine
    {
        /// <summary>
        /// Compiles and generates actionable recommendations based on system findings.
        /// </summary>
        /// <param name="findings">The list of findings to evaluate.</param>
        /// <returns>A collection of generated recommendations.</returns>
        IEnumerable<DiagnosticRecommendation> Evaluate(IEnumerable<DiagnosticFinding> findings);
    }
}

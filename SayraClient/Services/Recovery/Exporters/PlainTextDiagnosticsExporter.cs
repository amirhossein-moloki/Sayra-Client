using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Interfaces.Recovery;
using Sayra.Client.Shared.Models.Recovery;

namespace SayraClient.Services.Recovery.Exporters
{
    /// <summary>
    /// Exporter for saving human-readable Plain Text diagnostics reports.
    /// </summary>
    public class PlainTextDiagnosticsExporter : IDiagnosticsExporter
    {
        public string Format => "TXT";

        public async Task<string> ExportAsync(ReportType reportType, string content, string destinationPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(destinationPath))
            {
                throw new ArgumentException("Destination path cannot be null or empty.", nameof(destinationPath));
            }

            cancellationToken.ThrowIfCancellationRequested();

            string dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await File.WriteAllTextAsync(destinationPath, content, cancellationToken);
            return destinationPath;
        }
    }
}

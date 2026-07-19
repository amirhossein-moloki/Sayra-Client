using System;

namespace Sayra.Client.Diagnostics.Models
{
    public record DisplayInformation(
        string Resolution,
        double RefreshRate,
        bool Hdr,
        double Dpi
    );
}

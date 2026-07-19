using System;

namespace Sayra.Client.Diagnostics.Models
{
    public record MotherboardInformation(
        string Manufacturer,
        string Product,
        string BiosVersion
    );
}

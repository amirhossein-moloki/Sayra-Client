using System;

namespace Sayra.Client.Diagnostics.Models
{
    public record NetworkInformation(
        string Hostname,
        string IPv4,
        string IPv6,
        string MacAddress
    );
}

using System;
using System.Collections.Generic;
using Sayra.Client.Shared.Security.GameProtection.Domain.Models;

namespace Sayra.Client.Shared.Security.GameProtection.Domain.Rules;

public class ProcessPolicy
{
    public List<ProcessRule> Rules { get; set; } = new();
    public List<AllowedGame> AllowedGames { get; set; } = new();
    public List<BlockedApplication> BlockedApplications { get; set; } = new();
    public bool StrictWhitelistingEnabled { get; set; }
}

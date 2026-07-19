using System;
using System.Collections.Generic;

namespace Sayra.Client.Diagnostics.Models
{
    public record ValidationResult(
        bool IsValid,
        List<string> Errors,
        List<string> Warnings
    );
}

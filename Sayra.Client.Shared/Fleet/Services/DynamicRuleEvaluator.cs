using System;
using System.Linq;
using Sayra.Client.Shared.Models.Phase9.Domain;

namespace Sayra.Client.Shared.Fleet.Services
{
    /// <summary>
    /// Utility to evaluate dynamic rule expressions on workstations for dynamic group memberships.
    /// </summary>
    public static class DynamicRuleEvaluator
    {
        /// <summary>
        /// Evaluates a rule expression (e.g. "GPU == RTX4090", "RAM >= 16", "Status == Online") against a workstation.
        /// </summary>
        public static bool Evaluate(string expression, MachineInfo machine, IReadOnlyList<FleetTag> tags)
        {
            if (string.IsNullOrWhiteSpace(expression)) return false;
            if (machine == null) return false;

            try
            {
                // Normalize expression: remove multiple spaces
                var parts = expression.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3) return false;

                string lhs = parts[0].Trim().ToUpperInvariant();
                string op = parts[1].Trim();
                // Join the rest as the rhs value (in case it contains spaces like "NVIDIA GeForce RTX4090")
                string rhs = string.Join(" ", parts, 2, parts.Length - 2).Trim().Trim('"', '\'');

                string lhsValue = string.Empty;
                double lhsNum = 0;
                double rhsNum = 0;
                bool isNumeric = false;

                switch (lhs)
                {
                    case "GPU":
                        lhsValue = machine.Inventory?.GpuName ?? string.Empty;
                        break;
                    case "CPU":
                        lhsValue = machine.Inventory?.CpuName ?? string.Empty;
                        break;
                    case "RAM":
                        if (machine.Inventory != null)
                        {
                            lhsNum = machine.Inventory.RamGb;
                            isNumeric = double.TryParse(rhs, out rhsNum);
                        }
                        break;
                    case "OS":
                        lhsValue = machine.Inventory?.OperatingSystem ?? string.Empty;
                        break;
                    case "STATUS":
                        lhsValue = machine.Status.ToString();
                        break;
                    case "HEALTH":
                        lhsValue = machine.HealthStatus.ToString();
                        break;
                    case "TAG":
                        // Check if any tag matches rhs
                        return tags != null && tags.Any(t =>
                            string.Equals(t.Key, rhs, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(t.Value, rhs, StringComparison.OrdinalIgnoreCase));
                    default:
                        return false;
                }

                if (isNumeric)
                {
                    return op switch
                    {
                        "==" => Math.Abs(lhsNum - rhsNum) < 0.001,
                        "!=" => Math.Abs(lhsNum - rhsNum) >= 0.001,
                        ">" => lhsNum > rhsNum,
                        "<" => lhsNum < rhsNum,
                        ">=" => lhsNum >= rhsNum,
                        "<=" => lhsNum <= rhsNum,
                        _ => false
                    };
                }

                // String comparison
                return op switch
                {
                    "==" => lhsValue.Contains(rhs, StringComparison.OrdinalIgnoreCase),
                    "!=" => !lhsValue.Contains(rhs, StringComparison.OrdinalIgnoreCase),
                    _ => false
                };
            }
            catch
            {
                return false;
            }
        }
    }
}

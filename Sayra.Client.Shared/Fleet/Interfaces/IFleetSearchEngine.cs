using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Phase9.Domain;

namespace Sayra.Client.Shared.Fleet.Interfaces
{
    /// <summary>
    /// Parameters governing searches, filtering, and page bounds.
    /// </summary>
    public class SearchParameters
    {
        /// <summary>Is matched partially against the workstation identifier.</summary>
        public string? MachineId { get; set; }

        /// <summary>Is matched partially against the hostname.</summary>
        public string? Hostname { get; set; }

        /// <summary>Metadata tag key criteria.</summary>
        public string? TagKey { get; set; }

        /// <summary>Metadata tag value criteria.</summary>
        public string? TagValue { get; set; }

        /// <summary>Filter by specific regional sector.</summary>
        public string? RegionId { get; set; }

        /// <summary>Filter by specific organizational department.</summary>
        public string? DepartmentId { get; set; }

        /// <summary>Filter by operational Status.</summary>
        public string? Status { get; set; }

        /// <summary>Filter by Health tier.</summary>
        public string? HealthStatus { get; set; }

        /// <summary>Filter by semantic version.</summary>
        public string? SemVer { get; set; }

        /// <summary>Filter by system capability keyword.</summary>
        public string? Capability { get; set; }

        /// <summary>Column name to order by (e.g. Hostname, HealthScore, LastSeenUtc, MachineId).</summary>
        public string SortBy { get; set; } = "Hostname";

        /// <summary>True to sort descending, false for ascending.</summary>
        public bool SortDescending { get; set; }

        /// <summary>0-based page index.</summary>
        public int PageIndex { get; set; }

        /// <summary>Page size limit.</summary>
        public int PageSize { get; set; } = 20;
    }

    /// <summary>
    /// Paginated search outcome carrying page meta and items.
    /// </summary>
    public class SearchResult
    {
        /// <summary>Workstations on the current page.</summary>
        public IReadOnlyList<MachineInfo> Items { get; set; } = new List<MachineInfo>();

        /// <summary>Total matching records count.</summary>
        public int TotalCount { get; set; }

        /// <summary>Active page index.</summary>
        public int PageIndex { get; set; }

        /// <summary>Page size limit.</summary>
        public int PageSize { get; set; }
    }

    /// <summary>
    /// Search engine contract for executing partial match searches and filtering.
    /// </summary>
    public interface IFleetSearchEngine
    {
        /// <summary>
        /// Executes paginated workstation searches with customizable filtering and ordering.
        /// </summary>
        Task<SearchResult> SearchAsync(SearchParameters parameters, CancellationToken ct = default);
    }
}

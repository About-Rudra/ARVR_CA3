using System;
using System.Text.RegularExpressions;

namespace ArPassport.Dashboard
{
    /// <summary>
    /// Dashboard-side asset DTO. Mirrors the backend Asset shape (owned by DEV2,
    /// ArPassport.Core) but is intentionally decoupled so the dashboard compiles
    /// and runs in offline seed mode before Core lands. Field mapping from the
    /// real backend type happens in DashboardApiAdapter via tolerant reflection.
    /// </summary>
    [Serializable]
    public class DashboardAsset
    {
        public string id = "";
        public string name = "";
        public string category = "";
        public string location = "";
        public string status = StatusOperational;
        public string markerId = "";
        public string description = "";
        public string lastMaintenance = "";
        public string nextMaintenance = "";

        public const string StatusOperational = "operational";
        public const string StatusMaintenance = "maintenance";
        public const string StatusOutOfService = "out_of_service";

        public static readonly string[] AllStatuses =
        {
            StatusOperational, StatusMaintenance, StatusOutOfService
        };

        public static string NormalizeStatus(string raw)
        {
            string s = (raw ?? "").Trim().ToLowerInvariant().Replace(" ", "_").Replace("-", "_");
            switch (s)
            {
                case "operational":
                case "operation":
                case "active":
                case "ok":
                case "running":
                case "available":
                    return StatusOperational;
                case "maintenance":
                case "under_maintenance":
                case "in_maintenance":
                case "repair":
                case "fault":
                case "faulty":
                case "broken":
                    return StatusMaintenance;
                case "out_of_service":
                case "decommissioned":
                case "retired":
                case "offline":
                case "inactive":
                    return StatusOutOfService;
                default:
                    return StatusOperational;
            }
        }

        public static string DisplayStatus(string status)
        {
            switch (NormalizeStatus(status))
            {
                case StatusMaintenance: return "Under Maintenance";
                case StatusOutOfService: return "Out of Service";
                default: return "Operational";
            }
        }

        public static bool IsValidId(string id)
        {
            return !string.IsNullOrEmpty(id) && Regex.IsMatch(id.Trim(), @"^AST-\d{3,}$");
        }

        public DashboardAsset Clone()
        {
            return (DashboardAsset)MemberwiseClone();
        }
    }

    [Serializable]
    public class DashboardIssue
    {
        public string id = "";
        public string assetId = "";
        public string title = "";
        public string description = "";
        public string status = StatusOpen;
        public string severity = "";
        public string createdAt = "";

        public const string StatusOpen = "open";
        public const string StatusInProgress = "in_progress";
        public const string StatusResolved = "resolved";
        public const string StatusRejected = "rejected";

        public static readonly string[] AllStatuses =
        {
            StatusOpen, StatusInProgress, StatusResolved, StatusRejected
        };

        public static string NormalizeIssueStatus(string raw)
        {
            string s = (raw ?? "").Trim().ToLowerInvariant().Replace(" ", "_").Replace("-", "_");
            switch (s)
            {
                case "open":
                case "new":
                case "reported":
                    return StatusOpen;
                case "in_progress":
                case "inprogress":
                case "progress":
                case "acknowledged":
                case "working":
                    return StatusInProgress;
                case "resolved":
                case "closed":
                case "fixed":
                case "done":
                    return StatusResolved;
                case "rejected":
                case "wontfix":
                case "invalid":
                case "dismissed":
                    return StatusRejected;
                default:
                    return StatusOpen;
            }
        }

        public static string DisplayStatus(string status)
        {
            switch (NormalizeIssueStatus(status))
            {
                case StatusInProgress: return "In Progress";
                case StatusResolved: return "Resolved";
                case StatusRejected: return "Rejected";
                default: return "Open";
            }
        }
    }

    [Serializable]
    public class DashboardMaintenanceRecord
    {
        public string id = "";
        public string assetId = "";
        public string date = "";
        public string type = "";
        public string technician = "";
        public string notes = "";
    }

    /// <summary>Top-level shape of Assets/Resources/seed_assets.json for JsonUtility.</summary>
    [Serializable]
    public class DashboardSeedData
    {
        public DashboardAsset[] assets;
        public DashboardIssue[] issues;
        public DashboardMaintenanceRecord[] maintenance;
    }
}

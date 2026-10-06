using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArPassport.Dashboard
{
    /// <summary>
    /// Loads the offline sample dataset from Assets/Resources/seed_assets.json.
    /// Parsing is tolerant: accepts the full wrapper shape, a bare asset array,
    /// or a {"data":{...}} envelope. Never throws; reports a friendly error.
    /// </summary>
    public static class DashboardSeedProvider
    {
        public const string SeedResourceName = "seed_assets";

        [Serializable]
        private class SeedEnvelope
        {
            public DashboardSeedData data;
        }

        public static bool TryLoadSeed(
            out List<DashboardAsset> assets,
            out List<DashboardIssue> issues,
            out List<DashboardMaintenanceRecord> maintenance,
            out string error)
        {
            assets = new List<DashboardAsset>();
            issues = new List<DashboardIssue>();
            maintenance = new List<DashboardMaintenanceRecord>();
            error = null;

            TextAsset ta = Resources.Load<TextAsset>(SeedResourceName);
            if (ta == null)
            {
                error = "Sample data file is missing (Assets/Resources/seed_assets.json).";
                return false;
            }
            return TryParseSeedJson(ta.text, out assets, out issues, out maintenance, out error);
        }

        public static bool TryParseSeedJson(
            string json,
            out List<DashboardAsset> assets,
            out List<DashboardIssue> issues,
            out List<DashboardMaintenanceRecord> maintenance,
            out string error)
        {
            assets = new List<DashboardAsset>();
            issues = new List<DashboardIssue>();
            maintenance = new List<DashboardMaintenanceRecord>();
            error = null;

            string work = (json ?? "").Trim();
            if (work.Length == 0)
            {
                error = "Sample data is empty.";
                return false;
            }

            // Tolerance 0: shared seed file uses Core key names; map them to ours.
            // Core shape: {locations, assets, maintenanceRecords, issues, reportedAt}.
            // Ours:       {assets, maintenance, issues, createdAt}.
            work = work.Replace("\"maintenanceRecords\"", "\"maintenance\"");
            work = work.Replace("\"reportedAt\"", "\"createdAt\"");

            // Tolerance 1: bare asset array -> wrap it.
            if (work.StartsWith("["))
            {
                work = "{\"assets\":" + work + "}";
            }

            DashboardSeedData data = null;
            try
            {
                data = JsonUtility.FromJson<DashboardSeedData>(work);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Dashboard] Seed parse attempt 1 failed: " + ex.Message);
            }

            // Tolerance 2: {"data": {...}} envelope.
            if ((data == null || data.assets == null) && work.Contains("\"data\""))
            {
                try
                {
                    SeedEnvelope env = JsonUtility.FromJson<SeedEnvelope>(work);
                    if (env != null && env.data != null)
                    {
                        data = env.data;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[Dashboard] Seed parse attempt 2 failed: " + ex.Message);
                }
            }

            if (data == null)
            {
                error = "Sample data is not valid JSON.";
                return false;
            }

            if (data.assets != null)
            {
                foreach (var a in data.assets)
                {
                    if (a == null) continue;
                    a.id = Safe(a.id);
                    a.name = Safe(a.name);
                    a.category = Safe(a.category);
                    a.location = Safe(a.location);
                    a.status = DashboardAsset.NormalizeStatus(a.status);
                    a.markerId = Safe(a.markerId);
                    a.description = Safe(a.description);
                    a.lastMaintenance = Safe(a.lastMaintenance);
                    a.nextMaintenance = Safe(a.nextMaintenance);
                    assets.Add(a);
                }
            }
            if (data.issues != null)
            {
                foreach (var i in data.issues)
                {
                    if (i == null) continue;
                    i.id = Safe(i.id);
                    i.assetId = Safe(i.assetId);
                    i.title = Safe(i.title);
                    i.description = Safe(i.description);
                    i.status = DashboardIssue.NormalizeIssueStatus(i.status);
                    i.severity = Safe(i.severity);
                    i.createdAt = Safe(i.createdAt);
                    issues.Add(i);
                }
            }
            if (data.maintenance != null)
            {
                foreach (var m in data.maintenance)
                {
                    if (m == null) continue;
                    m.id = Safe(m.id);
                    m.assetId = Safe(m.assetId);
                    m.date = Safe(m.date);
                    m.type = Safe(m.type);
                    m.technician = Safe(m.technician);
                    m.notes = Safe(m.notes);
                    maintenance.Add(m);
                }
            }
            return true;
        }

        private static string Safe(string s)
        {
            return s ?? "";
        }
    }
}

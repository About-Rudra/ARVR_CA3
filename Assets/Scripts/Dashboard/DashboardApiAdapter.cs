using System;
using System.Collections.Generic;
using ArPassport.Core;
using UnityEngine;

namespace ArPassport.Dashboard
{
    /// <summary>
    /// Backend bridge (DEV3). Consumes DEV2's ArPassport.Core.ApiClient with the
    /// documented callback contract and converts Core DTOs into dashboard DTOs.
    /// Read endpoints degrade gracefully: network-level failures fall back to
    /// the offline seed (Core LocalSeedProvider, then the tolerant local parser).
    /// Write endpoints never fake success: without a live backend they return a
    /// friendly error. Raw exceptions/codes are logged, never shown in the UI.
    /// No URLs are hardcoded here; ResolveBaseUrl() delegates to ApiConfig.
    /// Consumed endpoints:
    ///   GET /api/assets, GET /api/assets/{id}, GET /api/assets/{id}/maintenance,
    ///   GET /api/issues, PATCH /api/issues/{id}/status,
    ///   POST /api/assets, PATCH /api/assets/{id}, DELETE /api/assets/{id},
    ///   GET /api/locations.
    /// </summary>
    public static class DashboardApiAdapter
    {
        public const string BaseUrlEnvVar = "AR_API_BASE_URL";

        /// <summary>True when the last read was served from offline sample data.</summary>
        public static bool LastUsedSeed { get; private set; }

        /// <summary>"ok", "seed" or "error". For diagnostics display only.</summary>
        public static string LastStatus { get; private set; }

        // ------------------------------------------------------------------ //
        // Availability / config                                               //
        // ------------------------------------------------------------------ //

        public static bool IsBackendAvailable(out string reason)
        {
            try
            {
                if (ApiClient.Instance == null)
                {
                    reason = "Backend client is present but not initialized (ApiClient.Instance is null).";
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Dashboard] ApiClient lookup failed: " + ex.Message);
                reason = "Backend client (ArPassport.Core.ApiClient) is not available in this build.";
                return false;
            }
            reason = null;
            return true;
        }

        /// <summary>Base URL for diagnostics display. Reads Core config chain
        /// (env AR_API_BASE_URL, PlayerPrefs, api_config.json, ScriptableObject).</summary>
        public static string ResolveBaseUrl()
        {
            try
            {
                if (ApiClient.Instance != null)
                {
                    return ApiClient.Instance.BaseUrl;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Dashboard] BaseUrl lookup failed: " + ex.Message);
            }
            try
            {
                return ApiConfig.GetBaseUrl();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Dashboard] ApiConfig lookup failed: " + ex.Message);
                return "";
            }
        }

        // ------------------------------------------------------------------ //
        // Reads (backend first, seed fallback on network failure)             //
        // ------------------------------------------------------------------ //

        public static void FetchAssets(Action<List<DashboardAsset>> ok, Action<string> err)
        {
            if (!IsBackendAvailable(out string reason))
            {
                UseSeedAssets(ok, err, reason);
                return;
            }
            try
            {
                ApiClient.Instance.GetAssets(
                    list => Deliver(ok, ConvertAssets(list)),
                    (code, msg) => OnReadError(code, msg, () => UseSeedAssets(ok, err, null), () => Fail(err, code, msg)));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Dashboard] GetAssets failed: " + ex.Message);
                UseSeedAssets(ok, err, null);
            }
        }

        public static void FetchAssetById(string id, Action<DashboardAsset> ok, Action<string> err)
        {
            if (!IsBackendAvailable(out string reason))
            {
                FetchSeedBundle(
                    list =>
                    {
                        DashboardAsset found = list.Find(a => string.Equals(a.id, id, StringComparison.OrdinalIgnoreCase));
                        if (found != null) { MarkSeed(); if (ok != null) ok(found); }
                        else Fail(err, null, "Asset '" + id + "' was not found in the sample data.");
                    },
                    err);
                return;
            }
            try
            {
                ApiClient.Instance.GetAssetById(id,
                    asset => Deliver(ok, FromCore(asset)),
                    (code, msg) => OnReadError(code, msg,
                        () => FetchSeedBundle(
                            list =>
                            {
                                DashboardAsset found = list.Find(a => string.Equals(a.id, id, StringComparison.OrdinalIgnoreCase));
                                if (found != null) { MarkSeed(); if (ok != null) ok(found); }
                                else Fail(err, null, "Asset '" + id + "' was not found in the sample data.");
                            },
                            err),
                        () => Fail(err, code, msg)));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Dashboard] GetAssetById failed: " + ex.Message);
                Fail(err, null, null);
            }
        }

        public static void FetchMaintenance(string assetId, Action<List<DashboardMaintenanceRecord>> ok, Action<string> err)
        {
            if (!IsBackendAvailable(out string reason))
            {
                UseSeedMaintenance(assetId, ok, err);
                return;
            }
            try
            {
                ApiClient.Instance.GetMaintenance(assetId,
                    list => Deliver(ok, ConvertMaintenance(list)),
                    (code, msg) => OnReadError(code, msg,
                        () => UseSeedMaintenance(assetId, ok, err),
                        () => Fail(err, code, msg)));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Dashboard] GetMaintenance failed: " + ex.Message);
                UseSeedMaintenance(assetId, ok, err);
            }
        }

        public static void FetchIssues(Action<List<DashboardIssue>> ok, Action<string> err)
        {
            if (!IsBackendAvailable(out string reason))
            {
                UseSeedIssues(ok, err);
                return;
            }
            try
            {
                ApiClient.Instance.GetIssues(
                    list => Deliver(ok, ConvertIssues(list)),
                    (code, msg) => OnReadError(code, msg,
                        () => UseSeedIssues(ok, err),
                        () => Fail(err, code, msg)));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Dashboard] GetIssues failed: " + ex.Message);
                UseSeedIssues(ok, err);
            }
        }

        public static void FetchLocations(Action<List<string>> ok, Action<string> err)
        {
            if (!IsBackendAvailable(out string reason))
            {
                UseSeedLocations(ok, err);
                return;
            }
            try
            {
                ApiClient.Instance.GetLocations(
                    list => Deliver(ok, ConvertLocationNames(list)),
                    (code, msg) => OnReadError(code, msg,
                        () => UseSeedLocations(ok, err),
                        () => Fail(err, code, msg)));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Dashboard] GetLocations failed: " + ex.Message);
                UseSeedLocations(ok, err);
            }
        }

        // ------------------------------------------------------------------ //
        // Writes (live backend required; friendly error when offline)         //
        // ------------------------------------------------------------------ //

        public static void UpdateIssueStatus(string issueId, string status, Action ok, Action<string> err)
        {
            if (string.IsNullOrEmpty(issueId)) { Fail(err, null, "Issue id is missing."); return; }
            if (!IsBackendAvailable(out string reason))
            {
                Fail(err, null, "Offline mode: issue status changes need a server connection. " + reason);
                return;
            }
            try
            {
                ApiClient.Instance.UpdateIssueStatus(issueId, ToBackendIssueStatus(status),
                    _ => { if (ok != null) ok(); },
                    (code, msg) => Fail(err, code, msg));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Dashboard] UpdateIssueStatus failed: " + ex.Message);
                Fail(err, null, null);
            }
        }

        public static void CreateAsset(DashboardAsset asset, Action ok, Action<string> err)
        {
            if (asset == null) { Fail(err, null, "Nothing to save: the asset is empty."); return; }
            if (!IsBackendAvailable(out string reason))
            {
                Fail(err, null, "Offline mode: new assets cannot be saved without a server connection. " + reason);
                return;
            }
            try
            {
                ApiClient.Instance.CreateAsset(ToCore(asset),
                    _ => { if (ok != null) ok(); },
                    (code, msg) => Fail(err, code, msg));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Dashboard] CreateAsset failed: " + ex.Message);
                Fail(err, null, null);
            }
        }

        public static void UpdateAsset(DashboardAsset asset, Action ok, Action<string> err)
        {
            if (asset == null) { Fail(err, null, "Nothing to save: the asset is empty."); return; }
            if (!IsBackendAvailable(out string reason))
            {
                Fail(err, null, "Offline mode: changes cannot be saved without a server connection. " + reason);
                return;
            }
            try
            {
                ApiClient.Instance.UpdateAsset(asset.id, ToCore(asset),
                    _ => { if (ok != null) ok(); },
                    (code, msg) => Fail(err, code, msg));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Dashboard] UpdateAsset failed: " + ex.Message);
                Fail(err, null, null);
            }
        }

        public static void DeleteAsset(string id, Action ok, Action<string> err)
        {
            if (string.IsNullOrEmpty(id)) { Fail(err, null, "Asset id is missing."); return; }
            if (!IsBackendAvailable(out string reason))
            {
                Fail(err, null, "Offline mode: assets cannot be deleted without a server connection. " + reason);
                return;
            }
            try
            {
                ApiClient.Instance.DeleteAsset(id,
                    () => { if (ok != null) ok(); },
                    (code, msg) => Fail(err, code, msg));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Dashboard] DeleteAsset failed: " + ex.Message);
                Fail(err, null, null);
            }
        }

        // ------------------------------------------------------------------ //
        // Seed fallback (Core LocalSeedProvider first, local parser backup)  //
        // ------------------------------------------------------------------ //

        public static void FetchSeedBundle(
            Action<List<DashboardAsset>, List<DashboardIssue>, List<DashboardMaintenanceRecord>> ok,
            Action<string> err)
        {
            try
            {
                if (LocalSeedProvider.EnsureLoaded())
                {
                    List<DashboardAsset> assets = ConvertAssets(LocalSeedProvider.GetAllAssets());
                    List<DashboardIssue> issues = ConvertIssues(LocalSeedProvider.GetAllIssues());
                    List<DashboardMaintenanceRecord> maint = new List<DashboardMaintenanceRecord>();
                    foreach (var a in assets)
                    {
                        if (a == null) continue;
                        maint.AddRange(ConvertMaintenance(LocalSeedProvider.GetMaintenanceForAsset(a.id)));
                    }
                    MarkSeed();
                    if (ok != null) ok(assets, issues, maint);
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Dashboard] LocalSeedProvider failed: " + ex.Message);
            }
            List<DashboardAsset> bAssets;
            List<DashboardIssue> bIssues;
            List<DashboardMaintenanceRecord> bMaint;
            string error;
            if (DashboardSeedProvider.TryLoadSeed(out bAssets, out bIssues, out bMaint, out error))
            {
                MarkSeed();
                if (ok != null) ok(bAssets, bIssues, bMaint);
            }
            else
            {
                Fail(err, null, string.IsNullOrEmpty(error) ? "Sample data could not be loaded." : error);
            }
        }

        private static void UseSeedAssets(Action<List<DashboardAsset>> ok, Action<string> err, string reason)
        {
            if (!string.IsNullOrEmpty(reason)) Debug.Log("[Dashboard] Using offline sample data. " + reason);
            else Debug.Log("[Dashboard] Using offline sample data.");
            FetchSeedBundle((a, i, m) => { if (ok != null) ok(a); }, err);
        }

        private static void UseSeedIssues(Action<List<DashboardIssue>> ok, Action<string> err)
        {
            Debug.Log("[Dashboard] Using offline sample data (issues).");
            FetchSeedBundle((a, i, m) => { if (ok != null) ok(i); }, err);
        }

        private static void UseSeedMaintenance(string assetId, Action<List<DashboardMaintenanceRecord>> ok, Action<string> err)
        {
            Debug.Log("[Dashboard] Using offline sample data (maintenance).");
            FetchSeedBundle(
                (a, i, m) =>
                {
                    List<DashboardMaintenanceRecord> mine = (m ?? new List<DashboardMaintenanceRecord>()).FindAll(r =>
                        r != null && string.Equals(r.assetId, assetId, StringComparison.OrdinalIgnoreCase));
                    MarkSeed();
                    if (ok != null) ok(mine);
                }, err);
        }

        private static void UseSeedLocations(Action<List<string>> ok, Action<string> err)
        {
            Debug.Log("[Dashboard] Using offline sample data (locations).");
            try
            {
                if (LocalSeedProvider.EnsureLoaded())
                {
                    MarkSeed();
                    if (ok != null) ok(ConvertLocationNames(LocalSeedProvider.GetAllLocations()));
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Dashboard] Seed locations failed: " + ex.Message);
            }
            FetchSeedBundle(
                (a, i, m) =>
                {
                    List<string> locs = new List<string>();
                    if (a != null)
                    {
                        foreach (var asset in a)
                        {
                            if (asset == null || string.IsNullOrEmpty(asset.location)) continue;
                            if (!locs.Contains(asset.location)) locs.Add(asset.location);
                        }
                    }
                    locs.Sort(StringComparer.OrdinalIgnoreCase);
                    MarkSeed();
                    if (ok != null) ok(locs);
                }, err);
        }

        // ------------------------------------------------------------------ //
        // Converters Core <-> dashboard                                      //
        // ------------------------------------------------------------------ //

        public static DashboardAsset FromCore(Asset src)
        {
            if (src == null) return null;
            return new DashboardAsset
            {
                id = src.id ?? "",
                name = src.name ?? "",
                category = src.category ?? "",
                location = src.location ?? "",
                status = DashboardAsset.NormalizeStatus(src.status),
                markerId = src.markerId ?? "",
                description = src.description ?? "",
                lastMaintenance = src.lastMaintenance ?? "",
                nextMaintenance = src.nextMaintenance ?? ""
            };
        }

        public static Asset ToCore(DashboardAsset src)
        {
            if (src == null) return null;
            return new Asset
            {
                id = src.id ?? "",
                name = src.name ?? "",
                category = src.category ?? "",
                location = src.location ?? "",
                status = ToBackendAssetStatus(src.status),
                description = src.description ?? "",
                imageUrl = "",
                modelUrl = "",
                lastMaintenance = src.lastMaintenance ?? "",
                nextMaintenance = src.nextMaintenance ?? "",
                markerId = src.markerId ?? ""
            };
        }

        /// <summary>Dashboard vocab (operational/maintenance/out_of_service) to
        /// backend vocab (operational/under_maintenance/faulty/retired).</summary>
        public static string ToBackendAssetStatus(string status)
        {
            switch (DashboardAsset.NormalizeStatus(status))
            {
                case DashboardAsset.StatusMaintenance: return "under_maintenance";
                case DashboardAsset.StatusOutOfService: return "retired";
                default: return "operational";
            }
        }

        /// <summary>Backend has open|in_progress|resolved|closed; the dashboard
        /// additionally tracks rejected (seed/local only), sent as closed.</summary>
        public static string ToBackendIssueStatus(string status)
        {
            string s = DashboardIssue.NormalizeIssueStatus(status);
            if (s == DashboardIssue.StatusRejected) return "closed";
            return s;
        }

        public static DashboardIssue FromCore(Issue src)
        {
            if (src == null) return null;
            return new DashboardIssue
            {
                id = src.id ?? "",
                assetId = src.assetId ?? "",
                title = src.title ?? "",
                description = src.description ?? "",
                status = DashboardIssue.NormalizeIssueStatus(src.status),
                severity = "",
                createdAt = src.reportedAt ?? ""
            };
        }

        public static DashboardMaintenanceRecord FromCore(MaintenanceRecord src)
        {
            if (src == null) return null;
            return new DashboardMaintenanceRecord
            {
                id = src.id ?? "",
                assetId = src.assetId ?? "",
                date = src.date ?? "",
                type = src.type ?? "",
                technician = src.technician ?? "",
                notes = src.notes ?? ""
            };
        }

        public static List<DashboardAsset> ConvertAssets(List<Asset> list)
        {
            List<DashboardAsset> outList = new List<DashboardAsset>();
            if (list == null) return outList;
            foreach (var item in list)
            {
                DashboardAsset a = FromCore(item);
                if (a != null) outList.Add(a);
            }
            return outList;
        }

        public static List<DashboardIssue> ConvertIssues(List<Issue> list)
        {
            List<DashboardIssue> outList = new List<DashboardIssue>();
            if (list == null) return outList;
            foreach (var item in list)
            {
                DashboardIssue i = FromCore(item);
                if (i != null) outList.Add(i);
            }
            return outList;
        }

        public static List<DashboardMaintenanceRecord> ConvertMaintenance(List<MaintenanceRecord> list)
        {
            List<DashboardMaintenanceRecord> outList = new List<DashboardMaintenanceRecord>();
            if (list == null) return outList;
            foreach (var item in list)
            {
                DashboardMaintenanceRecord r = FromCore(item);
                if (r != null) outList.Add(r);
            }
            return outList;
        }

        public static List<string> ConvertLocationNames(List<Location> list)
        {
            List<string> outList = new List<string>();
            if (list == null) return outList;
            foreach (var loc in list)
            {
                if (loc == null) continue;
                string name = !string.IsNullOrEmpty(loc.name) ? loc.name : loc.id;
                if (!string.IsNullOrEmpty(name) && !outList.Contains(name)) outList.Add(name);
            }
            outList.Sort(StringComparer.OrdinalIgnoreCase);
            return outList;
        }

        // ------------------------------------------------------------------ //
        // Completion helpers                                                  //
        // ------------------------------------------------------------------ //

        private static void Deliver<T>(Action<T> ok, T value)
        {
            LastUsedSeed = false;
            LastStatus = "ok";
            if (ok != null) ok(value);
        }

        private static void MarkSeed()
        {
            LastUsedSeed = true;
            LastStatus = "seed";
        }

        private static void Fail(Action<string> err, string code, string msg)
        {
            LastStatus = "error:" + (code ?? "UNKNOWN");
            if (err != null) err(FriendlyError(code, msg));
        }

        private static void OnReadError(string code, string msg, Action useSeed, Action surface)
        {
            if (IsNetworkCode(code))
            {
                if (useSeed != null) useSeed();
            }
            else if (surface != null)
            {
                surface();
            }
        }

        private static bool IsNetworkCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return true;
            return code == "NETWORK_OFFLINE" || code == "NETWORK_ERROR" || code == "HTTP_0";
        }

        private static string FriendlyError(string code, string message)
        {
            if (string.IsNullOrEmpty(code) || code == "NETWORK_OFFLINE")
            {
                return "You're offline. Please check your connection and try again.";
            }
            if (code == "NETWORK_ERROR" || code == "HTTP_0")
            {
                return "Couldn't reach the server. Please check your connection and try again.";
            }
            if (code == "HTTP_404")
            {
                return "The requested item was not found on the server.";
            }
            if (code == "HTTP_401" || code == "HTTP_403")
            {
                return "You are not authorized to do that. Please sign in again.";
            }
            if (code == "PARSE_ERROR")
            {
                return "The server sent a response we couldn't understand. Please try again.";
            }
            string safe = (message ?? "").Trim();
            if (safe.Length > 160) safe = safe.Substring(0, 160) + "...";
            if (safe.Length == 0)
            {
                return "Something went wrong. Please try again.";
            }
            return "Something went wrong: " + safe;
        }
    }
}

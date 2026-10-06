using System.Collections.Generic;
using UnityEngine;

namespace ArPassport.Core
{
    /// <summary>
    /// Offline fallback data source. Loads Assets/Resources/seed_assets.json
    /// (must match database/seed.sql) and exposes the same lookups as the
    /// backend synchronously. Use when ApiClient reports NETWORK_OFFLINE /
    /// NETWORK_ERROR so the demo works without network.
    /// Usage: LocalSeedProvider.EnsureLoaded(); var a = LocalSeedProvider.GetAssetByMarker("MARK-001");
    /// </summary>
    public static class LocalSeedProvider
    {
        [System.Serializable]
        private class SeedDatabase
        {
            public List<Location> locations;
            public List<Asset> assets;
            public List<MaintenanceRecord> maintenanceRecords;
            public List<Issue> issues;
        }

        private static bool _loaded;
        private static List<Asset> _assets = new List<Asset>();
        private static List<Location> _locations = new List<Location>();
        private static List<MaintenanceRecord> _maintenance = new List<MaintenanceRecord>();
        private static List<Issue> _issues = new List<Issue>();

        public static bool IsLoaded => _loaded;

        /// <summary>Load from Resources/seed_assets.json. Safe to call repeatedly.</summary>
        public static bool EnsureLoaded()
        {
            if (_loaded) return true;
            TextAsset ta = Resources.Load<TextAsset>("seed_assets");
            if (ta == null || string.IsNullOrEmpty(ta.text))
            {
                Debug.LogError("[LocalSeedProvider] Assets/Resources/seed_assets.json not found or empty.");
                return false;
            }
            try
            {
                var db = JsonUtility.FromJson<SeedDatabase>(ta.text);
                _assets = db.assets ?? new List<Asset>();
                _locations = db.locations ?? new List<Location>();
                _maintenance = db.maintenanceRecords ?? new List<MaintenanceRecord>();
                _issues = db.issues ?? new List<Issue>();
                _loaded = true;
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError("[LocalSeedProvider] Failed to parse seed_assets.json: " + e.Message);
                return false;
            }
        }

        public static List<Asset> GetAllAssets()
        {
            EnsureLoaded();
            return new List<Asset>(_assets);
        }

        public static Asset GetAssetById(string id)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < _assets.Count; i++)
                if (_assets[i] != null && _assets[i].id == id) return _assets[i];
            return null;
        }

        public static Asset GetAssetByMarker(string markerId)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(markerId)) return null;
            string key = markerId.Trim().ToUpperInvariant();
            for (int i = 0; i < _assets.Count; i++)
            {
                var a = _assets[i];
                if (a != null && !string.IsNullOrEmpty(a.markerId) &&
                    a.markerId.Trim().ToUpperInvariant() == key) return a;
            }
            return null;
        }

        /// <summary>Synchronous equivalent of GET /api/assets/marker/:markerId.</summary>
        public static bool TryResolveMarker(string markerId, out string assetId)
        {
            var a = GetAssetByMarker(markerId);
            assetId = a != null ? a.id : null;
            return a != null;
        }

        public static List<MaintenanceRecord> GetMaintenanceForAsset(string assetId)
        {
            EnsureLoaded();
            var outList = new List<MaintenanceRecord>();
            for (int i = 0; i < _maintenance.Count; i++)
                if (_maintenance[i] != null && _maintenance[i].assetId == assetId) outList.Add(_maintenance[i]);
            return outList;
        }

        public static List<Issue> GetIssuesForAsset(string assetIdOrNull)
        {
            EnsureLoaded();
            var outList = new List<Issue>();
            for (int i = 0; i < _issues.Count; i++)
            {
                if (_issues[i] == null) continue;
                if (string.IsNullOrEmpty(assetIdOrNull) || _issues[i].assetId == assetIdOrNull)
                    outList.Add(_issues[i]);
            }
            return outList;
        }

        public static List<Issue> GetAllIssues()
        {
            EnsureLoaded();
            return new List<Issue>(_issues);
        }

        public static List<Location> GetAllLocations()
        {
            EnsureLoaded();
            return new List<Location>(_locations);
        }

        public static Location GetLocationById(string id)
        {
            EnsureLoaded();
            for (int i = 0; i < _locations.Count; i++)
                if (_locations[i] != null && _locations[i].id == id) return _locations[i];
            return null;
        }

        /// <summary>For tests / editor validation: number of seeded assets.</summary>
        public static int AssetCount { get { EnsureLoaded(); return _assets.Count; } }
    }
}

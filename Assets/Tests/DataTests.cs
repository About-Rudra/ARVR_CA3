using System.Collections.Generic;

namespace ArPassport.Tests
{
    /// <summary>
    /// Plain-C# offline validation for seed data shapes (no Unity Test Framework
    /// dependency so it also runs as a simple EditMode-style check).
    /// Mirrors tests/api.test.js assertions for the Unity side.
    /// Run from Unity: call DataTests.RunAll() from an editor button / menu item.
    /// </summary>
    public static class DataTests
    {
        public struct CheckResult
        {
            public string name;
            public bool passed;
            public string detail;
            public CheckResult(string n, bool p, string d) { name = n; passed = p; detail = d; }
        }

        public static List<CheckResult> RunAll()
        {
            var results = new List<CheckResult>();
            results.Add(SeedLoads());
            results.Add(MarkerLookup());
            results.Add(StatusEnums());
            results.Add(MaintenanceLinked());
            return results;
        }

        private static CheckResult SeedLoads()
        {
            bool ok = Core.LocalSeedProvider.EnsureLoaded();
            int n = Core.LocalSeedProvider.AssetCount;
            return new CheckResult("SeedLoads", ok && n == 5,
                ok ? $"assets={n} (expected 5)" : "seed_assets.json missing/unparseable");
        }

        private static CheckResult MarkerLookup()
        {
            Core.LocalSeedProvider.EnsureLoaded();
            bool ok = Core.LocalSeedProvider.TryResolveMarker("MARK-001", out string id) && id == "AST-001"
                && Core.LocalSeedProvider.TryResolveMarker("mark-002", out string id2) && id2 == "AST-002"
                && !Core.LocalSeedProvider.TryResolveMarker("MARK-999", out _);
            return new CheckResult("MarkerLookup", ok, ok ? "MARK-001->AST-001, case-insensitive, miss->false" : "marker resolution failed");
        }

        private static CheckResult StatusEnums()
        {
            Core.LocalSeedProvider.EnsureLoaded();
            var allowed = new HashSet<string> { "operational", "under_maintenance", "faulty", "retired" };
            foreach (var a in Core.LocalSeedProvider.GetAllAssets())
                if (!allowed.Contains(a.status))
                    return new CheckResult("StatusEnums", false, $"asset {a.id} has invalid status '{a.status}'");
            return new CheckResult("StatusEnums", true, "all asset statuses valid");
        }

        private static CheckResult MaintenanceLinked()
        {
            Core.LocalSeedProvider.EnsureLoaded();
            var recs = Core.LocalSeedProvider.GetMaintenanceForAsset("AST-001");
            bool ok = recs != null && recs.Count >= 2;
            return new CheckResult("MaintenanceLinked", ok, $"AST-001 records={recs?.Count ?? -1} (expected >=2)");
        }
    }
}

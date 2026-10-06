using System;
using System.Collections.Generic;

namespace ArPassport.Core
{
    // NOTE: Field names intentionally match the REST API JSON keys exactly
    // (camelCase where the API uses camelCase) so UnityEngine.JsonUtility works
    // without custom converters.
    // If the Newtonsoft.Json package (com.unity.nuget.newtonsoft-json) is available,
    // prefer: JsonConvert.DeserializeObject<T>(json) / JsonConvert.SerializeObject(obj).
    // The FromJson helpers below use JsonUtility + a small array wrapper so this file
    // compiles on stock Unity 2022.3 with no extra packages.

    [Serializable]
    public class Asset
    {
        public string id;               // e.g. AST-001
        public string name;
        public string category;
        public string location;         // location id, e.g. CL-6
        public string status;           // operational|under_maintenance|faulty|retired
        public string description;
        public string imageUrl;
        public string modelUrl;
        public string lastMaintenance;  // ISO date yyyy-MM-dd
        public string nextMaintenance;  // ISO date yyyy-MM-dd
        public string markerId;         // e.g. MARK-001

        public static Asset FromJson(string json)
        {
            return UnityEngine.JsonUtility.FromJson<Asset>(json);
        }

        public string ToJson()
        {
            return UnityEngine.JsonUtility.ToJson(this);
        }
    }

    [Serializable]
    public class MaintenanceRecord
    {
        public string id;
        public string assetId;
        public string date;       // ISO date
        public string type;       // preventive|corrective|calibration|inspection
        public string technician;
        public string notes;

        public static MaintenanceRecord FromJson(string json)
        {
            return UnityEngine.JsonUtility.FromJson<MaintenanceRecord>(json);
        }
    }

    [Serializable]
    public class Issue
    {
        public string id;           // ISS-xxx (server-assigned on create)
        public string assetId;
        public string title;
        public string description;
        public string status;       // open|in_progress|resolved|closed
        public string reportedAt;   // ISO-8601
        public string reportedBy;

        public static Issue FromJson(string json)
        {
            return UnityEngine.JsonUtility.FromJson<Issue>(json);
        }

        public string ToJson()
        {
            return UnityEngine.JsonUtility.ToJson(this);
        }
    }

    /// <summary>Payload for POST /api/issues (no id / reportedAt; server assigns).</summary>
    [Serializable]
    public class IssueDraft
    {
        public string assetId;
        public string title;
        public string description;
        public string reportedBy;

        public string ToJson()
        {
            return UnityEngine.JsonUtility.ToJson(this);
        }
    }

    [Serializable]
    public class Location
    {
        public string id;     // e.g. CL-6
        public string name;
        public int floor;
        public string building;

        public static Location FromJson(string json)
        {
            return UnityEngine.JsonUtility.FromJson<Location>(json);
        }
    }

    [Serializable]
    public class MarkerLookupResponse
    {
        public string assetId;

        public static MarkerLookupResponse FromJson(string json)
        {
            return UnityEngine.JsonUtility.FromJson<MarkerLookupResponse>(json);
        }
    }

    [Serializable]
    public class ApiErrorDetail
    {
        public string code;
        public string message;
    }

    [Serializable]
    public class ApiErrorResponse
    {
        public ApiErrorDetail error;
    }

    /// <summary>
    /// JsonUtility cannot parse top-level JSON arrays, so list endpoints are
    /// wrapped: "{\"items\":" + arrayJson + "}". Works on stock Unity 2022.3.
    /// With Newtonsoft available, use JsonConvert.DeserializeObject&lt;List&lt;T&gt;&gt;(json) directly.
    /// </summary>
    public static class JsonListHelper
    {
        [Serializable]
        private class Wrapper<T>
        {
            public T[] items;
        }

        public static List<T> FromJsonArray<T>(string arrayJson)
        {
            if (string.IsNullOrEmpty(arrayJson)) return new List<T>();
            string trimmed = arrayJson.TrimStart();
            // Already an object wrapper? Try direct wrapper parse first only for arrays.
            if (trimmed.StartsWith("["))
            {
                var w = UnityEngine.JsonUtility.FromJson<Wrapper<T>>("{\"items\":" + arrayJson + "}");
                return w != null && w.items != null ? new List<T>(w.items) : new List<T>();
            }
            // Single object -> single-element list fallback.
            var single = UnityEngine.JsonUtility.FromJson<T>(arrayJson);
            return single != null ? new List<T> { single } : new List<T>();
        }

        public static T[] FromJsonArrayRaw<T>(string arrayJson)
        {
            return FromJsonArray<T>(arrayJson).ToArray();
        }

        /// <summary>Try to extract {error:{code,message}} from an error body.</summary>
        public static bool TryParseError(string json, out string code, out string message)
        {
            code = "UNKNOWN";
            message = json;
            if (string.IsNullOrEmpty(json)) { message = "Empty response"; return false; }
            try
            {
                var err = UnityEngine.JsonUtility.FromJson<ApiErrorResponse>(json);
                if (err != null && err.error != null)
                {
                    code = string.IsNullOrEmpty(err.error.code) ? "UNKNOWN" : err.error.code;
                    message = err.error.message ?? json;
                    return true;
                }
            }
            catch { /* fall through */ }
            return false;
        }
    }
}

# Backend Guide — AR Digital Passport

## 1. Architecture

```
Unity app (AR scene + Dashboard scene)
  │  UnityWebRequest (JSON), base URL from ApiConfig
  ▼
Express REST API (Backend/server.js, CORS enabled)
  │  pg Pool when DATABASE_URL is set, else in-memory seed store
  ▼
Postgres (Supabase-compatible) — schema in database/schema.sql
```

Offline fallback (demo without network): `Assets/Scripts/Core/LocalSeedProvider.cs`
loads `Assets/Resources/seed_assets.json` synchronously and exposes the same
lookups (`GetAssetByMarker`, `GetAssetById`, maintenance/issues). `ApiClient`
signals `NETWORK_OFFLINE` / `NETWORK_ERROR` so AR/Dashboard code can fall back.

Unity files owned by DEV 2 (do not edit AR/Dashboard scripts):
- `Assets/Scripts/Core/Models.cs` — serializable DTOs, exact API field names.
- `Assets/Scripts/Core/ApiConfig.cs` — base-URL resolution (env → PlayerPrefs → `Resources/api_config.json` → ScriptableObject default `http://localhost:3000`), 10 s timeout.
- `Assets/Scripts/Core/ApiClient.cs` — singleton coroutine client, all 11 contract calls.
- `Assets/Scripts/Core/LocalSeedProvider.cs` — offline seed provider.

## 2. Schema explanation

| Table | Purpose | Key constraints |
|---|---|---|
| `locations` | Rooms/labs (`CL-6`, `FAB-LAB`, `AR-LAB`) | `id` PK |
| `assets` | Physical assets, `location_id` → `locations` | PK `id LIKE 'AST-%'`; `status` check; date sanity check |
| `asset_markers` | Physical marker ↔ asset (1:1 both sides) | `marker_id` PK, `asset_id` UNIQUE + FK cascade |
| `maintenance_records` | Service history per asset | FK cascade, `type` check |
| `issues` | Fault reports per asset | FK cascade, `status` check, title length check |
| `admins` | Dashboard login (future auth) | `username` UNIQUE |

Indexes on `assets(location_id, status)`, `maintenance_records(asset_id)`, `issues(asset_id, status)`.
`updated_at` auto-bumped by trigger. See `database/schema.sql`.

## 3. Environment variables

| Var | Default | Meaning |
|---|---|---|
| `PORT` | `3000` | Express listen port |
| `DATABASE_URL` | _(unset)_ | Postgres/Supabase connection string. Unset → in-memory seed store |
| `CORS_ORIGIN` | _(unset = allow all)_ | Comma-separated origins allowed to call the API (e.g. `https://app.example.com`). Unset → `cors()` default, open for Unity editor / LAN-device testing; set it to restrict in production |

Unity side: `AR_API_BASE_URL` (env var or `PlayerPrefs`, or `Assets/Resources/api_config.json`).
Never commit `.env` — only `Backend/.env.example`.

## 4. Setup / seed / test

```bash
cd Backend
npm install
npm test            # supertest+jest, in-memory store, no DB needed
npm start           # http://localhost:3000
```

With Postgres/Supabase:
```bash
# .env: DATABASE_URL=postgresql://...
psql "$DATABASE_URL" -f ../database/schema.sql -f ../database/seed.sql
npm start
```

Unity: open project in Unity 2022.3.9f1, ensure `Assets/Resources/seed_assets.json`
exists (offline demo works with no server). Set API URL via
`Resources/api_config.json` or `ApiConfig.SetOverride("http://<lan-ip>:3000")`
for on-device testing.

## 5. Deploy notes

- Any Node host (Render/Fly/Railway) or Supabase Edge-adjacent VM: set `PORT` + `DATABASE_URL`, run `npm start`.
- CORS is open by default (`cors()` when `CORS_ORIGIN` is unset) so LAN devices and the Unity editor can reach the API during development; set `CORS_ORIGIN` (comma-separated, see `Backend/.env.example`) to restrict `origin` in production.
- Supabase: create project → copy connection string → run `schema.sql` + `seed.sql` in the SQL editor.

## 6. Limitations

- In-memory mode is ephemeral (restart reseeds) and single-process — use Postgres for multi-client persistence.
- No auth on API routes yet (`admins` table reserved); add JWT/session middleware before production.
- Image/model URLs are placeholders (`example.com`); asset images should move to object storage + CDN.
- Marker resolution is exact-match on `marker_id`; fuzzy/alias matching not implemented.

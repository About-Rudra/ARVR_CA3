# DEPLOYMENT — AR Passport (DEV4)

Free-tier topology: **Unity APK (ARCore) + PC standalone (dashboard demo) →
Express on Render/Railway → Postgres on Supabase/Neon.** Native AR needs no
HTTPS/camera-browser workarounds — those apply to WebAR only.
Backend entry: `Backend/server.js` (`npm start`); default `PORT=3000`;
empty `DATABASE_URL` runs the in-memory seed store (demo/tests).

## 1. Database (Supabase _or_ Neon — pick one)

**Supabase:** dashboard → New project → SQL editor → paste
`database/schema.sql` → run → paste `database/seed.sql` → run (or
`psql $DATABASE_URL -f database/schema.sql -f database/seed.sql`).
Expect: 3 locations, 5 assets, 5 markers (`MARK-001..005`), 6 maintenance
rows, 3 issues. **Neon:** same SQL against its connection string.
Copy the pooled connection string into backend `DATABASE_URL`.

Env: `DATABASE_URL` (+ optional `SUPABASE_URL`, `SUPABASE_KEY`).

## 2. Backend (Render _or_ Railway)

**Render:** New → Web Service → repo, root dir `Backend/` → build `npm ci` →
start `npm start` → env `PORT` (platform default; code falls back to 3000),
`DATABASE_URL`, `CORS_ORIGIN` → Deploy → note `https://<svc>.onrender.com`.
**Railway:** New → GitHub repo → service root `Backend/` → same env → Deploy.
Verify: `GET https://<svc>/health` → `200 {"ok":true}`; spot-check
`GET /api/assets/marker/MARK-001` → `{"assetId":"AST-001"}`.
Cold starts (~30–60 s Render free) are normal — app shows the offline banner,
then connects (TEST_PLAN J).

## 3. Unity Android APK (ARCore)

1. Hub + Unity **2022.3.9f1**, Android module (SDK/NDK via Hub), ARCore XR
   Plug-in from `Packages/manifest.json` (DEV4: AR Foundation 5.0.7).
2. Build Settings → Android → add `Boot`, `Dashboard`, `ARScanner`
   (`ARScanner.unity` landed — ships empty, `ARBootstrap` builds at runtime;
   `Boot` + `Dashboard` still WIP, DEV3). `Boot` first.
   Texture compression ASTC.
3. Player Settings: package `com.team.arpassport`, **min API 26**, target 33,
   IL2CPP/ARM64, **camera permission** (`CAMERA` auto-declared by AR
   Foundation; confirm in manifest), Internet access required.
4. API URL = deployed origin `https://<svc>` (no `/api` suffix): set
   `AppBootstrapper.apiBaseUrl`, or `Resources/api_config.json`, or ship with
   `--api-url` / PlayerPrefs `AR_API_BASE_URL`. Release builds with
   `localhost` trigger the DEV4 prod-URL warning; a trailing `/api` triggers
   the suffix warning (`GuardProdUrl`).
5. XR Plug-in Management → Android → ARCore checked. Build → install APK →
   grant camera on first scan; denial path in TEST_PLAN N.
6. Print markers from `Assets/StreamingAssets/markers/` + `Assets/Markers/`
   (DEV1, landed: `MARK-001..003`) at a fixed physical size for tracking stability.

## 4. PC standalone (dashboard demo)

Target PC/Mac/Linux standalone, scenes Boot+Dashboard (+ARScanner optional —
AR camera inert on PC; DEV1 `SimulationProvider` covers `MARK-001/002/003`
in Editor). Same origin wiring. Use for classroom demo with no Android device.

## 5. Env vars (all in root `.env.example`, mirrored by `Backend/.env.example`)

| Var | Where | Notes |
|-----|-------|-------|
| `PORT` | Backend | `3000` local; platform injects prod port |
| `DATABASE_URL` | Backend | Pooled Postgres string, or empty for seed store |
| `CORS_ORIGIN` | Backend | Dashboard origins; native builds unaffected |
| `API_BASE_URL` / `AR_API_BASE_URL` | Unity + CI | Deployed origin `https://<svc>`; localhost Editor-only; shared key `AR_API_BASE_URL` |
| `SUPABASE_URL` / `SUPABASE_KEY` | Backend (opt) | Only if using Supabase client libs |

No secrets in git (`.gitignore` covers `.env`, keystores). No `localhost` in
prod builds.

## 6. Rollback / rebuild from fresh clone

```powershell
git clone <repo-url>; cd ca3-nimi
cd Backend; npm install; npm test            # jest over in-memory seed, no DB
copy .env.example .env                       # set DATABASE_URL for real DB
npm start                                    # http://localhost:3000, GET /health
psql $env:DATABASE_URL -f ../database/schema.sql -f ../database/seed.sql
# Unity Hub open (2022.3.9f1) -> set backend origin -> Build & Run
```

Current live URLs: backend _(placeholder — none deployed yet)_,
dashboard demo _(PC build artifact, unreleased)_.

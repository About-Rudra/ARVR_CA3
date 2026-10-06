# AR Passport — Unity AR + Dashboard over Express/Postgres

Problem: field equipment needs a scannable identity — point a phone at a
machine marker and see its passport (specs, service history), file issues, and
have the back-office dashboard reflect it live. This repo is a Unity-entirely
build (Android ARCore + PC standalone) on a free-tier backend.

Features:

- AR marker → asset lookup (`AssetProvider` + `ARMarkerTracker` sim keys
  `1/2/3`; `ARScanner.unity` ships empty, `ARBootstrap` builds stack at
  runtime; DEV1; printed markers landed, `Boot` scene pending).
- REST API: `GET /api/assets`, `/marker/:markerId`, `/:id/maintenance`,
  `GET/POST /api/issues`, `PATCH /api/issues/:id/status`,
  `GET /api/locations`, `GET /health` with `{error:{code,message}}` envelope
  and in-memory seed fallback (DEV2, landed).
- Offline seed fallback in Unity (`LocalSeedProvider` over
  `Assets/Resources/seed_assets.json`, DEV2, landed).
- Dashboard bridge/UI (DEV3; scripts landed, scenes `Boot`/`Dashboard` **WIP**).
- DEV4 integration/QA/deploy layer: boot routing, offline banner, prod-URL
  guard, validator, test plan, deployment guide.

Architecture (ASCII):

```
  [Unity AR (ARScanner)] --\
                             >-- [ApiClient] ---> [Express /api] ---> [Postgres]
  [Unity Dashboard] ------/                \---> [LocalSeedProvider fallback]
        |                       AppBootstrapper (route/health/guard) + SceneNavigator
```

Stack: Unity 2022.3.9f1, AR Foundation 5.0.7 / ARCore, TextMeshPro,
Newtonsoft JSON, NUnit Test Framework; backend Node 20 + Express + `pg`
(DEV2); Postgres (Supabase/Neon free tier); jest + supertest for API tests.

Repo structure:

```
Assets/Scripts/AR/        DEV1 provider + tracker + runtime bootstrap (+ ARScanner.unity, markers)
Assets/Scripts/Core/      DEV2 ApiClient/ApiConfig/Models/LocalSeedProvider (landed)
Assets/Resources/         DEV2 api_config.json + seed_assets.json (offline seed)
Assets/Scripts/Dashboard/ DEV3 bridge/UI scripts (Dashboard/Boot scenes WIP)
Assets/Scripts/Utils/     DEV4 AppBootstrapper, SceneNavigator
Assets/Tests/             DEV4 IntegrationCheck.cs + DEV2 DataTests.cs
Backend/  database/       DEV2 API (server.js) + schema.sql/seed.sql
tests/                    DEV2 api.test.js + DEV4 validate_integration.ps1
docs/                     DEV4 TEST_PLAN.md, DEPLOYMENT.md
Packages/ ProjectSettings/ .gitignore .env.example  DEV4
```

Setup (fresh clone):

```powershell
git clone <repo-url>; cd ca3-nimi
# Unity: Hub -> Add project -> version 2022.3.9f1 (+ Android module w/ ARCore)
cd Backend; npm install
copy .env.example .env            # Backend's own example; set DATABASE_URL (or leave empty for seed store)
npm test                     # jest + supertest, no DB needed
npm start                    # http://localhost:3000, check GET /health
# Real DB:
psql $env:DATABASE_URL -f ..\database\schema.sql -f ..\database\seed.sql
# Validate (no Unity needed):
powershell -ExecutionPolicy Bypass -File ..\tests\validate_integration.ps1
```

Env: see root `.env.example` — backend origin convention (no `/api`
suffix), `PORT=3000`, `DATABASE_URL` (empty = seed store), `API_BASE_URL`
(alias `AR_API_BASE_URL`, canonical key shared by `ApiConfig` and
`AppBootstrapper`). Unity wiring order: `--api-url` CLI > env >
PlayerPrefs `AR_API_BASE_URL` > `Resources/api_config.json` > inspector.
Never commit `.env`.

DB seed: `database/schema.sql` + `database/seed.sql` mirrored by
`Assets/Resources/seed_assets.json` (DEV2, landed): 5 assets
(`AST-001..005` ↔ `MARK-001..005`), locations, maintenance, issues.

Run:

- Editor: open `Boot` → Play → auto-routes to Dashboard; `GET {origin}/health`
  probed; offline banner on failure. AR sim keys `1/2/3` (`ARMarkerTracker` →
  `MARK-001/002/003`) until printed-marker testing on device.
- Device: build APK per `docs/DEPLOYMENT.md` (min API 26, camera permission,
  deployed origin — never `localhost` in release).

Testing: validator (`-Strict`, `-ProbeApi` flags); `cd Backend; npm test`;
Unity Test Runner EditMode → `ArPassport.Tests.IntegrationCheck`; full
checklists A–N in `docs/TEST_PLAN.md`.

Deployment: backend Render/Railway + Postgres Supabase/Neon; APK via Build
Settings; PC standalone for dashboard demo — exact steps in
`docs/DEPLOYMENT.md`. Live URLs: _(placeholder — nothing deployed yet)_.

Demo script (3–5 min, once DEV3 scenes land): Dashboard list → ARScanner →
resolve `MARK-001` (Epson projector `AST-001`) → passport opens →
maintenance tab (MNT-001/002) → report issue → back to Dashboard → issue
visible → set `in_progress` → persists on refresh. Today: same flow provable
via API (`api.test.js`) + Unity seed (`DataTests`).

Team contributions (Unity roles): DEV1 `Assets/Scripts/AR/` + `ARScanner.unity`
+ markers (provider, tracking, passport UI; `Boot` scene + deep-link consume
pending); DEV2 `Assets/Scripts/Core/` + `Backend/` +
`database/` + `Resources/*.json` + `DataTests`/`api.test.js`; DEV3
`Assets/Scripts/Dashboard/` + scenes (**WIP**); DEV4 (integration, QA,
deployment): `README.md`, `docs/`, `Packages/`, `ProjectSettings/`,
`.gitignore`, `.env.example`, `Utils/AppBootstrapper.cs` +
`SceneNavigator.cs`, `Assets/Tests/IntegrationCheck.cs`,
`tests/validate_integration.ps1`, CI workflow.

Limitations: `Boot`/`Dashboard` scenes not landed (marked
**WIP**); deep-link `PendingMarker` written by `AppBootstrapper` but not yet
nowhere offline); Render free cold starts delay first connect; ARCore needs a
supported Android device (no mobile-browser/WebAR path).

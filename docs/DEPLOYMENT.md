# DEPLOYMENT — AR Passport (DEV4)

Free-tier topology: **static web frontends (`frontend/ar` + `frontend/dashboard`)
→ Express on Render/Railway → Postgres on Supabase/Neon.** Unity APK
(`Assets/`) remains as an alternative native-AR path. Browsers gate the camera
behind HTTPS, so production frontends must be served over HTTPS (Vercel/Netlify
do this by default) and the backend should be HTTPS too (Render/Railway default),
otherwise browsers block the API calls as mixed content.

Current live URLs: backend _(placeholder — nothing deployed yet)_,
WebAR scanner _(placeholder — nothing deployed yet)_,
dashboard _(placeholder — nothing deployed yet)_.

## 0. What deploys from where

| Piece | Source dir | Host | Build |
|-------|-----------|------|-------|
| WebAR scanner | `frontend/ar/` (static, no deps) | Vercel / Netlify | none |
| Web dashboard | `frontend/dashboard/` (static, no deps) | Vercel / Netlify (same or separate site) | none |
| REST API | `Backend/` (`npm start`, `PORT` env) | Render / Railway | `npm ci` |
| Postgres | `database/schema.sql` + `seed.sql` | Supabase / Neon | SQL run once |
| Android APK (alternative) | `Assets/` Unity project | local build, sideload | Unity 2022.3.9f1 |

## 1. Database (Supabase _or_ Neon — pick one)

**Supabase:** dashboard → New project → SQL editor → paste
`database/schema.sql` → run → paste `database/seed.sql` → run (or
`psql $DATABASE_URL -f database/schema.sql -f database/seed.sql`).
Expect: 3 locations, 5 assets, 5 markers (`MARK-001..005`), 6 maintenance
rows, 3 issues. **Neon:** same SQL against its connection string.
Copy the pooled connection string into backend `DATABASE_URL`.

Env: `DATABASE_URL` (+ optional `SUPABASE_URL`, `SUPABASE_KEY` only if using
Supabase client libs).

## 2. Backend (Render _or_ Railway, root dir `Backend/`)

**Render (recommended — Blueprint):** push the repo to GitHub → Render
dashboard → New → **Blueprint** → pick the repo → it reads `render.yaml`
(service `ar-passport-api`, root `Backend/`, `npm ci --omit=dev`, `npm start`,
health check `/health`) and prompts for `DATABASE_URL` and `CORS_ORIGIN`
(secrets are never in the file) → Apply.
**Render (manual):** New → Web Service → repo, root dir `Backend/` → build `npm ci` →
start `npm start` → env `PORT` (platform default; code falls back to 3000),
`DATABASE_URL`, `CORS_ORIGIN` → Deploy → note `https://<svc>.onrender.com`.
**Railway:** New → GitHub repo → service root `Backend/` → same env → Deploy.
Verify: `GET https://<svc>/health` → `200 {"ok":true,"db":"up"}` (`"down"` =
check `DATABASE_URL`; `"memory"` = `DATABASE_URL` not set); spot-check
`GET /api/assets/marker/MARK-001` → `{"assetId":"AST-001"}`.
Cold starts (~30–60 s Render free) are normal — frontends show their
offline/unreachable banner, then connect (TEST_PLAN J).

Env: `PORT`, `DATABASE_URL`, `CORS_ORIGIN` (set to the deployed frontend
origin(s), e.g. `https://<dashboard>.vercel.app,https://<ar>.vercel.app`).

## 3. Web frontends (Vercel _or_ Netlify — no build step)

Both folders are dependency-free static sites (plain HTML/CSS/JS, CDN only for
the AR page's A-Frame/MindAR). The only "build" is writing `env.js` (the
backend URL) from the `API_BASE_URL` env var via `scripts/write-frontend-env.js`.

**Netlify (recommended, configs checked in):** Add new site → Import from Git →
repo → **Base directory `frontend/ar`** (Netlify then reads
`frontend/ar/netlify.toml`: build command writes `env.js`, publish `.`, camera
Permissions-Policy) → Environment variables: `API_BASE_URL =
https://<svc>.onrender.com` (origin, no `/api`) → Deploy. Repeat with base
directory `frontend/dashboard`. Production builds refuse a localhost URL.
Changing the backend later = change `API_BASE_URL` and redeploy (`env.js` is
served `no-cache`).

Other hosts (no config file): create a site per folder:

**Vercel:** New project → repo → Root Directory `frontend/ar` (and a second
project with Root Directory `frontend/dashboard`) → Framework Preset: Other →
no build command, output = directory root → Deploy (gets HTTPS by default).
**Netlify:** New site → repo → Publish directory `frontend/ar` (likewise
`frontend/dashboard`) → no build command → Deploy.

Point each site at the backend via **one** of, in priority order
(see `frontend/*/config.js`):
1. `window.__API_BASE_URL` — set by `env.js` (generated at deploy from
   `API_BASE_URL`; for hosts without a build step run
   `API_BASE_URL=https://<svc> node scripts/write-frontend-env.js frontend/ar frontend/dashboard`
   and upload the result);
2. the in-page **API base URL** field (persists to `localStorage "AR_API_BASE_URL"`;
   AR page also accepts `?api=https://<svc>` once, which persists);
3. fallback `http://localhost:3000` applies only when no override is set
   (local dev only — never a production URL, nothing is baked into the code).

Verify: open `https://<ar-site>/?marker=MARK-001` (no camera needed) → Epson
projector passport → maintenance (MNT-001/002) → report an issue → open
`https://<dashboard-site>/` → Issues tab shows it → set `in_progress` →
refresh → persists. Mixed-content note: if the dashboard/AR site is HTTPS,
the backend MUST be HTTPS too.

AR camera note: `frontend/ar/targets/targets.mind` (5 targets) is checked in
and deploys with the AR site as a plain static file. Recompile only if marker
images change (`tools/mindar-compiler`, see `frontend/ar/targets/README.md`). Camera needs HTTPS + a
camera-capable mobile browser; denial falls back to manual mode (TEST_PLAN N).

## 4. Unity Android APK (alternative native path, legacy `Assets/`)

1. Hub + Unity **2022.3.9f1**, Android module (SDK/NDK via Hub), ARCore XR
   Plug-in from `Packages/manifest.json` (AR Foundation 5.0.7).
2. Build Settings → Android → add `Boot`, `Dashboard`, `ARScanner` scenes,
   `Boot` first. Texture compression ASTC.
3. Player Settings: package `com.team.arpassport`, **min API 26**, target 33,
   IL2CPP/ARM64, **camera permission**, Internet access required.
4. API URL = deployed origin `https://<svc>` (no `/api` suffix): set
   `AppBootstrapper.apiBaseUrl`, or `Resources/api_config.json`. Release builds
   with `localhost` are blocked by the prod-URL guard.
5. XR Plug-in Management → Android → ARCore checked. Build → install APK →
   grant camera on first scan; denial path in TEST_PLAN N.
6. Print markers from `Assets/StreamingAssets/markers/` + `Assets/Markers/`
   (`MARK-001..005`) at a fixed physical size for tracking stability.

## 5. Env vars (all documented in root `.env.example`)

| Var | Where | Notes |
|-----|-------|-------|
| `PORT` | Backend | `3000` local; platform injects prod port |
| `DATABASE_URL` | Backend | Pooled Postgres string, or empty for in-memory seed store |
| `DB_CONNECT_TIMEOUT_MS` | Backend (opt) | Default 10000; DB connect attempts fail with `503 DB_UNAVAILABLE` after this |
| `PGSSL` | Backend (opt) | `false` forces SSL off (auto-off for `localhost`/`127.0.0.1`/`sslmode=disable`; on for hosted DBs) |
| `CORS_ORIGIN` | Backend | Empty = allow all (local dev, incl. `localhost:<any-port>` frontends); prod = deployed frontend origin(s), comma-separated |
| `API_BASE_URL` / `AR_API_BASE_URL` | Frontends + Unity + CI | Deployed backend origin `https://<svc>`; `http://localhost:3000` local-only; web apps resolve `window.__API_BASE_URL` → `localStorage "AR_API_BASE_URL"` → dev default |
| `SUPABASE_URL` / `SUPABASE_KEY` | Backend (opt) | Only if using Supabase client libs |

No secrets in git (`.gitignore` covers `.env`, keystores). No `localhost` in
production frontend config — use `__API_BASE_URL` or the in-page setting.

## 6. Health checks

- `GET {backend}/health` → `200 {"ok":true,"db":"up"}` (backend + DB).
- `node tests/e2e_flow.js https://<svc>` → PASS (full API flow; creates one issue).
- `GET {backend}/api/assets/marker/MARK-001` → `{"assetId":"AST-001"}` (DB/seed).
- AR site `?marker=MARK-001` renders passport (frontend→backend wiring).
- Dashboard Issues tab reflects a newly posted issue after refresh (full loop).

## 7. Rollback / rebuild from fresh clone

```powershell
git clone <repo-url>; cd ca3-nimi
cd Backend; npm install; npm test            # jest over in-memory seed, no DB — must pass 10/10
copy ..\.env.example .env                    # set DATABASE_URL for real DB; leave CORS_ORIGIN empty for local dev
npm start                                     # http://localhost:3000, GET /health
psql $env:DATABASE_URL -f ../database/schema.sql -f ../database/seed.sql   # real DB only
# Frontend smoke (no deploy needed):
cd ../frontend/ar; python3 -m http.server 8000        # open http://localhost:8000/?marker=MARK-001
cd ../dashboard; python3 -m http.server 8001          # open http://localhost:8001/
```

Rollback of a bad deploy: redeploy the previous good commit on the host
(Vercel/Netlify instant rollback, Render manual redeploy), or point the
frontend's in-page API URL back at the last good backend origin. The API is
stateless (except the DB); the in-memory seed mode recovers by restart.

# AR Passport API Reference

Base URL: `http://localhost:3000` for local dev. Web frontends never hardcode
it — `frontend/ar/config.js` and `frontend/dashboard/config.js` resolve
`window.__API_BASE_URL` → `localStorage "AR_API_BASE_URL"` → dev default
(AR page also takes `?api=` once). Unity clients use `ApiClient` (`Assets/Scripts/Core/ApiClient.cs`) against these routes.

Error envelope (all 4xx/5xx):
```json
{ "error": { "code": "ASSET_NOT_FOUND", "message": "Asset \"AST-999\" not found." } }
```
Codes: `ASSET_NOT_FOUND`, `MARKER_NOT_FOUND`, `ISSUE_NOT_FOUND`, `VALIDATION_ERROR`,
`ASSET_CONFLICT`, `MARKER_CONFLICT`, `NOT_FOUND`, `INTERNAL_ERROR`.

Asset shape:
```json
{
  "id": "AST-001", "name": "Epson EB-X49 Projector", "category": "AV Equipment",
  "location": "CL-6", "status": "operational",
  "description": "...", "imageUrl": "https://...", "modelUrl": "",
  "lastMaintenance": "2026-07-15", "nextMaintenance": "2026-10-15",
  "markerId": "MARK-001"
}
```
`status` ∈ `operational|under_maintenance|faulty|retired`.

---

## GET /api/assets
List assets. Optional filters: `?status=&location=&category=`.
- 200:
```json
[
  { "id": "AST-001", "name": "Epson EB-X49 Projector", "category": "AV Equipment", "location": "CL-6", "status": "operational", "description": "...", "imageUrl": "https://example.com/images/ast-001.jpg", "modelUrl": "", "lastMaintenance": "2026-07-15", "nextMaintenance": "2026-10-15", "markerId": "MARK-001" },
  { "id": "AST-002", "name": "Creality Ender-3 3D Printer", "...": "..." }
]
```
- 400 `VALIDATION_ERROR` on bad `status` filter.

## GET /api/assets/:id
- 200: single asset object. 404 `ASSET_NOT_FOUND`.

## GET /api/assets/marker/:markerId
Resolve a physical marker (QR / image target id) to its asset.
- 200: `{ "assetId": "AST-001" }`
- 404: `{ "error": { "code": "MARKER_NOT_FOUND", "message": "No asset linked to marker \"MARK-999\"." } }`

## GET /api/assets/:id/maintenance
- 200:
```json
[
  { "id": "MNT-001", "assetId": "AST-001", "date": "2026-07-15", "type": "preventive", "technician": "R. Kumar", "notes": "Filter cleaned..." }
]
```
- 404 `ASSET_NOT_FOUND` if the asset does not exist.

## GET /api/issues[?assetId=&status=]
- 200:
```json
[
  { "id": "ISS-001", "assetId": "AST-003", "title": "Channel 2 reads high", "description": "...", "status": "in_progress", "reportedAt": "2026-09-18T10:30:00Z", "reportedBy": "lab-assistant" }
]
```
`status` filter ∈ `open|in_progress|resolved|rejected|closed` (`rejected` added by DEV4 integration to match the project spec; `closed` kept for backward compatibility).

## POST /api/issues
Request:
```json
{ "assetId": "AST-001", "title": "Lamp flicker", "description": "Flickers on HDMI-1", "reportedBy": "student-cl6" }
```
- 201: created issue (`id` + `reportedAt` assigned, `status: "open"`).
- 400 `VALIDATION_ERROR` (missing/short title, missing assetId). 404 `ASSET_NOT_FOUND`.

## PATCH /api/issues/:id/status
Request: `{ "status": "in_progress" }`
- 200: updated issue. 400 `VALIDATION_ERROR` on bad status. 404 `ISSUE_NOT_FOUND`.

## POST /api/assets
Request: full asset object (same shape as above; `id`, `name`, `category`, `location` required).
- 201: created asset. 400 `VALIDATION_ERROR` (missing fields, bad status, unknown location).
- 409 `ASSET_CONFLICT` (id exists) / `MARKER_CONFLICT` (marker taken).

## PATCH /api/assets/:id
Request: partial asset fields, e.g. `{ "status": "retired", "location": "AR-LAB" }`.
- 200: updated asset. 400/404/409 as above.

## DELETE /api/assets/:id
- 204 (empty body). 404 `ASSET_NOT_FOUND`.

## GET /api/locations
- 200:
```json
[
  { "id": "CL-6", "name": "Computer Lab 6", "floor": 2, "building": "C-Block" },
  { "id": "FAB-LAB", "name": "Fabrication Lab", "floor": 1, "building": "Workshop Block" },
  { "id": "AR-LAB", "name": "AR/VR Lab", "floor": 2, "building": "C-Block" }
]
```

## GET /health (utility, not part of Unity contract)
- 200: `{ "ok": true, "time": "..." }`

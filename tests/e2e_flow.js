#!/usr/bin/env node
'use strict';
/**
 * DEV4 end-to-end API acceptance test — the README "target user flow" as HTTP calls.
 * Runs against ANY backend origin (local in-memory, local Postgres, or deployed):
 *
 *   node tests/e2e_flow.js                              # http://localhost:3000
 *   node tests/e2e_flow.js https://<backend>.onrender.com
 *   API_BASE_URL=https://... node tests/e2e_flow.js
 *
 * Needs Node 18+ (global fetch). No dependencies.
 * Side effect: creates ONE issue on AST-001 (there is no DELETE /api/issues);
 * it is left in status "in_progress", exactly like the live demo.
 */

const BASE = String(process.argv[2] || process.env.API_BASE_URL || 'http://localhost:3000').replace(/\/+$/, '');
const ORIGIN_HEADER = process.env.E2E_ORIGIN || ''; // optional: check CORS for a deployed frontend origin

let passed = 0;
let failed = 0;
const results = [];

function check(name, ok, detail) {
  results.push({ name, ok, detail });
  if (ok) passed++; else failed++;
  console.log(`  [${ok ? 'PASS' : 'FAIL'}] ${name}${!ok && detail ? ' — ' + detail : ''}`);
}

async function call(method, path, body) {
  const headers = { Accept: 'application/json' };
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  if (ORIGIN_HEADER) headers.Origin = ORIGIN_HEADER;
  const res = await fetch(BASE + path, {
    method,
    headers,
    body: body === undefined ? undefined : (typeof body === 'string' ? body : JSON.stringify(body))
  });
  let json = null;
  try { json = await res.json(); } catch (e) { /* non-JSON */ }
  return { status: res.status, json, headers: res.headers };
}

const isEnvelope = (j) => j && j.error && typeof j.error.code === 'string' && typeof j.error.message === 'string';

async function main() {
  console.log(`E2E flow against ${BASE}\n`);

  console.log('B. Backend startup');
  const health = await call('GET', '/health').catch((e) => ({ status: 0, err: e }));
  check('GET /health -> 200 {ok:true}', health.status === 200 && health.json && health.json.ok === true,
    health.err ? `unreachable: ${health.err.cause ? health.err.cause.code : health.err.message}` : `status ${health.status}`);
  if (health.status !== 200) { summary(); return; }
  if (ORIGIN_HEADER) {
    const acao = health.headers.get('access-control-allow-origin');
    check(`CORS allows ${ORIGIN_HEADER}`, acao === '*' || acao === ORIGIN_HEADER, `access-control-allow-origin=${acao}`);
  }

  console.log('C. Seed data present');
  const assets = await call('GET', '/api/assets');
  check('GET /api/assets -> >=5 assets', assets.status === 200 && Array.isArray(assets.json) && assets.json.length >= 5,
    `status ${assets.status}, n=${assets.json && assets.json.length}`);
  const locs = await call('GET', '/api/locations');
  check('GET /api/locations -> >=3 locations', locs.status === 200 && Array.isArray(locs.json) && locs.json.length >= 3);
  const markers = (assets.json || []).map((a) => a.markerId).filter(Boolean);
  check('>=3 assets carry a markerId', markers.length >= 3, `markers=${markers.join(',')}`);

  console.log('F/G. Marker recognition -> asset retrieval (AR flow)');
  for (const marker of ['MARK-001', 'MARK-002', 'MARK-003']) {
    const m = await call('GET', `/api/assets/marker/${marker}`);
    const ok = m.status === 200 && m.json && /^AST-/.test(m.json.assetId);
    check(`GET /api/assets/marker/${marker} -> {assetId}`, ok, `status ${m.status}`);
    if (!ok) continue;
    const a = await call('GET', `/api/assets/${m.json.assetId}`);
    const fields = ['id', 'name', 'category', 'location', 'status', 'lastMaintenance', 'nextMaintenance'];
    const missing = fields.filter((f) => !(a.json && f in a.json));
    check(`GET /api/assets/${m.json.assetId} has passport fields`, a.status === 200 && missing.length === 0, `missing ${missing}`);
  }

  console.log('H. Maintenance retrieval');
  const mnt = await call('GET', '/api/assets/AST-001/maintenance');
  check('GET /api/assets/AST-001/maintenance -> >=1 record', mnt.status === 200 && Array.isArray(mnt.json) && mnt.json.length >= 1);

  console.log('I. Issue creation (AR "Report Issue")');
  const title = `E2E lamp flicker ${new Date().toISOString()}`;
  const created = await call('POST', '/api/issues', { assetId: 'AST-001', title, description: 'Created by tests/e2e_flow.js', reportedBy: 'e2e' });
  const issueId = created.json && created.json.id;
  check('POST /api/issues -> 201 open', created.status === 201 && /^ISS-/.test(issueId || '') && created.json.status === 'open', `status ${created.status}`);

  console.log('J. Dashboard sees the issue');
  const list = await call('GET', '/api/issues');
  check('GET /api/issues contains the new issue', list.status === 200 && (list.json || []).some((i) => i.id === issueId));
  const byAsset = await call('GET', '/api/issues?assetId=AST-001');
  check('GET /api/issues?assetId=AST-001 contains it', (byAsset.json || []).some((i) => i.id === issueId));

  console.log('K. Admin changes status -> persists');
  const patched = await call('PATCH', `/api/issues/${issueId}/status`, { status: 'in_progress' });
  check('PATCH /api/issues/:id/status in_progress -> 200', patched.status === 200 && patched.json && patched.json.status === 'in_progress');
  const reread = await call('GET', '/api/issues?status=in_progress');
  check('re-read shows in_progress (state persisted)', (reread.json || []).some((i) => i.id === issueId && i.status === 'in_progress'));
  for (const s of ['open', 'in_progress', 'resolved', 'rejected']) {
    const r = await call('GET', `/api/issues?status=${s}`);
    check(`issue status "${s}" accepted by API`, r.status === 200, `status ${r.status}`);
  }

  console.log('Dashboard asset CRUD (create -> edit -> conflicts -> delete)');
  const tmpId = 'AST-9' + String(Date.now()).slice(-5);
  const tmp = { id: tmpId, name: 'E2E Temp Asset', category: 'Test', location: 'CL-6', status: 'operational' };
  const mk = await call('POST', '/api/assets', tmp);
  check(`POST /api/assets ${tmpId} -> 201`, mk.status === 201, `status ${mk.status}`);
  const dup = await call('POST', '/api/assets', tmp);
  check('duplicate asset id -> 409 ASSET_CONFLICT', dup.status === 409 && isEnvelope(dup.json) && dup.json.error.code === 'ASSET_CONFLICT', `status ${dup.status}`);
  const steal = await call('PATCH', `/api/assets/${tmpId}`, { markerId: 'MARK-001' });
  check('PATCH with marker of another asset -> 409 MARKER_CONFLICT', steal.status === 409 && isEnvelope(steal.json), `status ${steal.status}`);
  const still = await call('GET', '/api/assets/marker/MARK-001');
  check('MARK-001 still resolves to AST-001 after conflict', still.json && still.json.assetId === 'AST-001', `assetId=${still.json && still.json.assetId}`);
  const ed = await call('PATCH', `/api/assets/${tmpId}`, { status: 'under_maintenance' });
  check('PATCH /api/assets/:id status -> 200', ed.status === 200 && ed.json && ed.json.status === 'under_maintenance');
  const del = await call('DELETE', `/api/assets/${tmpId}`);
  check('DELETE /api/assets/:id -> 204', del.status === 204, `status ${del.status}`);
  const gone = await call('GET', `/api/assets/${tmpId}`);
  check('deleted asset -> 404', gone.status === 404);

  console.log('L. Error handling (must fail gracefully with the error envelope)');
  const badMarker = await call('GET', '/api/assets/marker/MARK-999');
  check('invalid marker -> 404 envelope', badMarker.status === 404 && isEnvelope(badMarker.json));
  const noAsset = await call('GET', '/api/assets/AST-999');
  check('nonexistent asset -> 404 ASSET_NOT_FOUND', noAsset.status === 404 && isEnvelope(noAsset.json) && noAsset.json.error.code === 'ASSET_NOT_FOUND');
  const noTitle = await call('POST', '/api/issues', { assetId: 'AST-001' });
  check('malformed issue (no title) -> 400 envelope', noTitle.status === 400 && isEnvelope(noTitle.json));
  const badJson = await call('POST', '/api/issues', '{not json');
  check('malformed JSON body -> 400 envelope', badJson.status === 400 && isEnvelope(badJson.json));
  const badAssetIssue = await call('POST', '/api/issues', { assetId: 'AST-999', title: 'ghost asset' });
  check('issue on nonexistent asset -> 404 envelope', badAssetIssue.status === 404 && isEnvelope(badAssetIssue.json));
  const badStatus = await call('PATCH', `/api/issues/${issueId}/status`, { status: 'exploded' });
  check('invalid issue status -> 400 envelope', badStatus.status === 400 && isEnvelope(badStatus.json));
  const unknown = await call('GET', '/api/nope');
  check('unknown /api route -> 404 envelope', unknown.status === 404 && isEnvelope(unknown.json));

  summary();
}

function summary() {
  console.log(`\n---- ${passed} passed, ${failed} failed ----`);
  console.log(failed ? 'FAIL' : 'PASS');
  process.exitCode = failed ? 1 : 0;
}

main().catch((e) => { console.error('E2E crashed:', e); process.exitCode = 2; });

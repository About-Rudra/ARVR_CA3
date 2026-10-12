'use strict';
/**
 * API contract tests (run from Backend/: npm test).
 * Uses the in-memory seed store (no DATABASE_URL) so results are reproducible.
 */
const request = require('supertest');
const app = require('../server');
const db = require('../db');

beforeEach(() => db.__reset());

describe('assets', () => {
  test('GET /api/assets returns the 5 seeded assets', async () => {
    const res = await request(app).get('/api/assets');
    expect(res.status).toBe(200);
    expect(Array.isArray(res.body)).toBe(true);
    expect(res.body.length).toBe(5);
    expect(res.body.map((a) => a.id)).toEqual(
      expect.arrayContaining(['AST-001', 'AST-002', 'AST-003', 'AST-004', 'AST-005']));
  });

  test('GET /api/assets/:id returns one asset; 404 uses error envelope', async () => {
    const ok = await request(app).get('/api/assets/AST-001');
    expect(ok.status).toBe(200);
    expect(ok.body.id).toBe('AST-001');
    expect(ok.body.markerId).toBe('MARK-001');

    const miss = await request(app).get('/api/assets/AST-999');
    expect(miss.status).toBe(404);
    expect(miss.body.error.code).toBe('ASSET_NOT_FOUND');
  });

  test('GET /api/assets/marker/:markerId resolves; unknown marker 404', async () => {
    const ok = await request(app).get('/api/assets/marker/MARK-002');
    expect(ok.status).toBe(200);
    expect(ok.body).toEqual({ assetId: 'AST-002' });

    const miss = await request(app).get('/api/assets/marker/MARK-999');
    expect(miss.status).toBe(404);
    expect(miss.body.error.code).toBe('MARKER_NOT_FOUND');
  });

  test('GET /api/assets/:id/maintenance returns records', async () => {
    const res = await request(app).get('/api/assets/AST-001/maintenance');
    expect(res.status).toBe(200);
    expect(res.body.length).toBeGreaterThanOrEqual(2);
    expect(res.body[0]).toMatchObject({ assetId: 'AST-001' });
  });

  test('POST/PATCH/DELETE asset lifecycle + validation', async () => {
    const payload = {
      id: 'AST-010', name: 'Test Bench PSU', category: 'Lab Equipment',
      location: 'FAB-LAB', status: 'operational', description: 't',
      imageUrl: '', modelUrl: '', lastMaintenance: '2026-09-01',
      nextMaintenance: '2026-12-01', markerId: 'MARK-010'
    };
    const created = await request(app).post('/api/assets').send(payload);
    expect(created.status).toBe(201);
    expect(created.body.id).toBe('AST-010');

    const dup = await request(app).post('/api/assets').send(payload);
    expect(dup.status).toBe(409);
    expect(dup.body.error.code).toBe('ASSET_CONFLICT');

    const bad = await request(app).post('/api/assets').send({ id: 'AST-011' });
    expect(bad.status).toBe(400);
    expect(bad.body.error.code).toBe('VALIDATION_ERROR');

    const patched = await request(app).patch('/api/assets/AST-010').send({ status: 'retired' });
    expect(patched.status).toBe(200);
    expect(patched.body.status).toBe('retired');

    const badStatus = await request(app).patch('/api/assets/AST-010').send({ status: 'nope' });
    expect(badStatus.status).toBe(400);

    const del = await request(app).delete('/api/assets/AST-010');
    expect(del.status).toBe(204);
    const gone = await request(app).get('/api/assets/AST-010');
    expect(gone.status).toBe(404);
  });
});

describe('issues', () => {
  test('GET /api/issues lists seed issues', async () => {
    const res = await request(app).get('/api/issues');
    expect(res.status).toBe(200);
    expect(res.body.length).toBeGreaterThanOrEqual(3);
  });

  test('POST /api/issues creates; PATCH /:id/status updates; invalid rejected', async () => {
    const created = await request(app).post('/api/issues').send({
      assetId: 'AST-001', title: 'Lamp flicker', description: 'Flickers on HDMI-1', reportedBy: 'jest'
    });
    expect(created.status).toBe(201);
    expect(created.body.status).toBe('open');
    const id = created.body.id;

    const updated = await request(app).patch(`/api/issues/${id}/status`).send({ status: 'in_progress' });
    expect(updated.status).toBe(200);
    expect(updated.body.status).toBe('in_progress');

    const badStatus = await request(app).patch(`/api/issues/${id}/status`).send({ status: 'bogus' });
    expect(badStatus.status).toBe(400);
    expect(badStatus.body.error.code).toBe('VALIDATION_ERROR');

    const missing = await request(app).patch('/api/issues/ISS-999/status').send({ status: 'closed' });
    expect(missing.status).toBe(404);
    expect(missing.body.error.code).toBe('ISSUE_NOT_FOUND');

    const noTitle = await request(app).post('/api/issues').send({ assetId: 'AST-001', title: 'x' });
    expect(noTitle.status).toBe(400);

    const noAsset = await request(app).post('/api/issues').send({ assetId: 'AST-999', title: 'Valid title' });
    expect(noAsset.status).toBe(404);
    expect(noAsset.body.error.code).toBe('ASSET_NOT_FOUND');
  });
});

describe('integration additions (DEV4)', () => {
  test('issue status "rejected" is accepted and persisted', async () => {
    const res = await request(app).patch('/api/issues/ISS-001/status').send({ status: 'rejected' });
    expect(res.status).toBe(200);
    expect(res.body.status).toBe('rejected');
    const list = await request(app).get('/api/issues?status=rejected');
    expect(list.body.map((i) => i.id)).toContain('ISS-001');
  });

  test('GET /health reports db mode', async () => {
    const res = await request(app).get('/health');
    expect(res.status).toBe(200);
    expect(res.body).toMatchObject({ ok: true, db: 'memory' });
  });
});

describe('locations', () => {
  test('GET /api/locations returns 3 seeded locations', async () => {
    const res = await request(app).get('/api/locations');
    expect(res.status).toBe(200);
    expect(res.body.length).toBe(3);
    expect(res.body.map((l) => l.id)).toEqual(expect.arrayContaining(['CL-6', 'FAB-LAB', 'AR-LAB']));
  });
});

'use strict';
/**
 * Root-level mirror of Backend/tests/api.test.js so the deliverable path
 * tests/api.test.js exists as specified. Run via: npx jest tests/api.test.js
 * from the repo root (requires Backend/ dependencies installed).
 */
const request = require('supertest');
const app = require('../Backend/server');
const db = require('../Backend/db');

beforeEach(() => db.__reset());

describe('assets', () => {
  test('GET /api/assets returns the 5 seeded assets', async () => {
    const res = await request(app).get('/api/assets');
    expect(res.status).toBe(200);
    expect(res.body.length).toBe(5);
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
  });
});

describe('issues', () => {
  test('POST /api/issues creates; PATCH /:id/status updates', async () => {
    const created = await request(app).post('/api/issues').send({
      assetId: 'AST-001', title: 'Lamp flicker', reportedBy: 'jest'
    });
    expect(created.status).toBe(201);
    const updated = await request(app).patch(`/api/issues/${created.body.id}/status`).send({ status: 'resolved' });
    expect(updated.status).toBe(200);
    expect(updated.body.status).toBe('resolved');
  });
  test('invalid issue payload rejected with VALIDATION_ERROR', async () => {
    const r = await request(app).post('/api/issues').send({ assetId: 'AST-001', title: 'x' });
    expect(r.status).toBe(400);
    expect(r.body.error.code).toBe('VALIDATION_ERROR');
  });
});

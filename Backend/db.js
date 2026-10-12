'use strict';
/**
 * Postgres wrapper with in-memory fallback.
 * - If DATABASE_URL is set, uses node-postgres (pg) against Postgres/Supabase.
 * - Otherwise (local demo, CI, jest) uses an in-memory store cloned from seedData.js.
 * The store API below is what routes use, so swapping backends needs no route changes.
 */
require('dotenv').config();
const { deepClone } = require('./seedData');

const usePostgres = Boolean(process.env.DATABASE_URL);
let pool = null;
if (usePostgres) {
  const { Pool } = require('pg');
  // Hosted Postgres (Supabase/Neon/Render) needs SSL; a local Postgres usually has none.
  // SSL is skipped for localhost/127.0.0.1, sslmode=disable, or PGSSL=false.
  const url = process.env.DATABASE_URL;
  const noSsl = /sslmode=disable/i.test(url) || process.env.PGSSL === 'false' ||
    /@(localhost|127\.0\.0\.1)(:\d+)?\//i.test(url);
  // Fail fast (-> 503 DB_UNAVAILABLE) instead of hanging clients when the DB host is unreachable.
  pool = new Pool({
    connectionString: url,
    ssl: noSsl ? false : { rejectUnauthorized: false },
    connectionTimeoutMillis: Number(process.env.DB_CONNECT_TIMEOUT_MS) || 10000
  });
  pool.on('error', (e) => console.error('[db] pool error', e.message));
}

// In-memory store (also used by tests via __reset for isolation)
let mem = deepClone();
let issueCounter = 100;
let assetCounter = 100;

function __reset() {
  mem = deepClone();
  issueCounter = 100;
  assetCounter = 100;
}

function isPostgres() { return usePostgres && pool !== null; }

// Row -> API shape mappers (snake_case -> camelCase)
function mapAsset(r) {
  if (!r) return null;
  return {
    id: r.id, name: r.name, category: r.category,
    location: r.location_id !== undefined ? r.location_id : r.location,
    status: r.status, description: r.description || '',
    imageUrl: r.image_url !== undefined ? (r.image_url || '') : (r.imageUrl || ''),
    modelUrl: r.model_url !== undefined ? (r.model_url || '') : (r.modelUrl || ''),
    lastMaintenance: r.last_maintenance ? String(r.last_maintenance).slice(0, 10) : (r.lastMaintenance || ''),
    nextMaintenance: r.next_maintenance ? String(r.next_maintenance).slice(0, 10) : (r.nextMaintenance || ''),
    markerId: r.marker_id !== undefined ? (r.marker_id || '') : (r.markerId || '')
  };
}

function mapMaintenance(r) {
  return { id: r.id, assetId: r.asset_id !== undefined ? r.asset_id : r.assetId, date: r.date ? String(r.date).slice(0, 10) : r.date, type: r.type, technician: r.technician || '', notes: r.notes || '' };
}

function mapIssue(r) {
  return {
    id: r.id, assetId: r.asset_id !== undefined ? r.asset_id : r.assetId,
    title: r.title, description: r.description || '',
    status: r.status,
    reportedAt: r.reported_at ? new Date(r.reported_at).toISOString() : r.reportedAt,
    reportedBy: r.reported_by !== undefined ? (r.reported_by || 'anonymous') : (r.reportedBy || 'anonymous')
  };
}

function mapLocation(r) { return { id: r.id, name: r.name, floor: Number(r.floor) || 0, building: r.building || '' }; }

// Runs fn(client) in one transaction. Maps Postgres unique violations (23505) to the
// same DUPLICATE / DUPLICATE_MARKER codes the in-memory store throws, so routes
// return 409 instead of 500 and a taken marker is never silently reassigned.
async function inTransaction(fn) {
  const client = await pool.connect();
  try {
    await client.query('BEGIN');
    await fn(client);
    await client.query('COMMIT');
  } catch (e) {
    await client.query('ROLLBACK').catch(() => {});
    if (e.code === '23505') {
      const dup = new Error(e.constraint && e.constraint.startsWith('asset_markers') ? 'duplicate marker' : 'duplicate');
      dup.code = dup.message === 'duplicate' ? 'DUPLICATE' : 'DUPLICATE_MARKER';
      throw dup;
    }
    throw e;
  } finally {
    client.release();
  }
}

const store = {
  isPostgres,
  __reset,
  getPool: () => pool,

  // For GET /health: 'memory' (no DATABASE_URL), 'up', or 'down'.
  async ping() {
    if (!isPostgres()) return 'memory';
    try { await pool.query('SELECT 1'); return 'up'; } catch (e) { return 'down'; }
  },

  async listAssets({ status, location, category } = {}) {
    if (isPostgres()) {
      const conds = []; const vals = [];
      if (status) { vals.push(status); conds.push(`a.status = $${vals.length}`); }
      if (location) { vals.push(location); conds.push(`a.location_id = $${vals.length}`); }
      if (category) { vals.push(category); conds.push(`a.category = $${vals.length}`); }
      const where = conds.length ? 'WHERE ' + conds.join(' AND ') : '';
      const { rows } = await pool.query(
        `SELECT a.*, m.marker_id FROM assets a LEFT JOIN asset_markers m ON m.asset_id = a.id ${where} ORDER BY a.id`, vals);
      return rows.map(mapAsset);
    }
    return mem.assets.filter((a) =>
      (!status || a.status === status) && (!location || a.location === location) && (!category || a.category === category));
  },

  async getAsset(id) {
    if (isPostgres()) {
      const { rows } = await pool.query(
        'SELECT a.*, m.marker_id FROM assets a LEFT JOIN asset_markers m ON m.asset_id = a.id WHERE a.id = $1', [id]);
      return rows.length ? mapAsset(rows[0]) : null;
    }
    return mem.assets.find((a) => a.id === id) || null;
  },

  async resolveMarker(markerId) {
    if (isPostgres()) {
      const { rows } = await pool.query('SELECT asset_id FROM asset_markers WHERE marker_id = $1', [markerId]);
      return rows.length ? rows[0].asset_id : null;
    }
    const a = mem.assets.find((x) => x.markerId === markerId);
    return a ? a.id : null;
  },

  async listMaintenance(assetId) {
    if (isPostgres()) {
      const { rows } = await pool.query('SELECT * FROM maintenance_records WHERE asset_id = $1 ORDER BY date DESC', [assetId]);
      return rows.map(mapMaintenance);
    }
    return mem.maintenanceRecords.filter((m) => m.assetId === assetId);
  },

  async listIssues({ assetId, status } = {}) {
    if (isPostgres()) {
      const conds = []; const vals = [];
      if (assetId) { vals.push(assetId); conds.push(`asset_id = $${vals.length}`); }
      if (status) { vals.push(status); conds.push(`status = $${vals.length}`); }
      const where = conds.length ? 'WHERE ' + conds.join(' AND ') : '';
      const { rows } = await pool.query(`SELECT * FROM issues ${where} ORDER BY reported_at DESC`, vals);
      return rows.map(mapIssue);
    }
    return mem.issues.filter((i) => (!assetId || i.assetId === assetId) && (!status || i.status === status));
  },

  async getIssue(id) {
    if (isPostgres()) {
      const { rows } = await pool.query('SELECT * FROM issues WHERE id = $1', [id]);
      return rows.length ? mapIssue(rows[0]) : null;
    }
    const i = mem.issues.find((x) => x.id === id);
    return i ? { ...i } : null;
  },

  async createIssue({ assetId, title, description, reportedBy }) {
    if (isPostgres()) {
      const id = 'ISS-' + Date.now().toString().slice(-6);
      const { rows } = await pool.query(
        'INSERT INTO issues (id, asset_id, title, description, reported_by) VALUES ($1,$2,$3,$4,$5) RETURNING *',
        [id, assetId, title, description || '', reportedBy || 'anonymous']);
      return mapIssue(rows[0]);
    }
    const issue = {
      id: 'ISS-' + String(issueCounter++).padStart(3, '0'),
      assetId, title, description: description || '',
      status: 'open', reportedAt: new Date().toISOString(), reportedBy: reportedBy || 'anonymous'
    };
    mem.issues.push(issue);
    return { ...issue };
  },

  async updateIssueStatus(id, status) {
    if (isPostgres()) {
      const { rows } = await pool.query('UPDATE issues SET status = $1 WHERE id = $2 RETURNING *', [status, id]);
      return rows.length ? mapIssue(rows[0]) : null;
    }
    const i = mem.issues.find((x) => x.id === id);
    if (!i) return null;
    i.status = status;
    return { ...i };
  },

  async createAsset(data) {
    if (isPostgres()) {
      await inTransaction(async (c) => {
        await c.query(
          'INSERT INTO assets (id, name, category, location_id, status, description, image_url, model_url, last_maintenance, next_maintenance) VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)',
          [data.id, data.name, data.category, data.location, data.status || 'operational', data.description || '', data.imageUrl || '', data.modelUrl || '', data.lastMaintenance || null, data.nextMaintenance || null]);
        if (data.markerId) await c.query('INSERT INTO asset_markers (marker_id, asset_id) VALUES ($1,$2)', [data.markerId, data.id]);
      });
      return this.getAsset(data.id);
    }
    if (mem.assets.some((a) => a.id === data.id)) { const e = new Error('duplicate'); e.code = 'DUPLICATE'; throw e; }
    if (data.markerId && mem.assets.some((a) => a.markerId === data.markerId)) { const e = new Error('duplicate marker'); e.code = 'DUPLICATE_MARKER'; throw e; }
    const asset = {
      id: data.id, name: data.name, category: data.category, location: data.location,
      status: data.status || 'operational', description: data.description || '',
      imageUrl: data.imageUrl || '', modelUrl: data.modelUrl || '',
      lastMaintenance: data.lastMaintenance || '', nextMaintenance: data.nextMaintenance || '',
      markerId: data.markerId || ''
    };
    mem.assets.push(asset);
    return { ...asset };
  },

  async updateAsset(id, patch) {
    if (isPostgres()) {
      const allowed = { name: 'name', category: 'category', location: 'location_id', status: 'status', description: 'description', imageUrl: 'image_url', modelUrl: 'model_url', lastMaintenance: 'last_maintenance', nextMaintenance: 'next_maintenance' };
      const sets = []; const vals = [];
      for (const k of Object.keys(allowed)) {
        if (patch[k] !== undefined) { vals.push(patch[k]); sets.push(`${allowed[k]} = $${vals.length}`); }
      }
      await inTransaction(async (c) => {
        if (sets.length) { vals.push(id); await c.query(`UPDATE assets SET ${sets.join(', ')} WHERE id = $${vals.length}`, vals); }
        if (patch.markerId !== undefined) {
          await c.query('DELETE FROM asset_markers WHERE asset_id = $1', [id]);
          if (patch.markerId) await c.query('INSERT INTO asset_markers (marker_id, asset_id) VALUES ($1,$2)', [patch.markerId, id]);
        }
      });
      return this.getAsset(id);
    }
    const a = mem.assets.find((x) => x.id === id);
    if (!a) return null;
    if (patch.markerId !== undefined && patch.markerId !== a.markerId &&
        patch.markerId && mem.assets.some((x) => x.id !== id && x.markerId === patch.markerId)) {
      const e = new Error('duplicate marker'); e.code = 'DUPLICATE_MARKER'; throw e;
    }
    for (const k of ['name', 'category', 'location', 'status', 'description', 'imageUrl', 'modelUrl', 'lastMaintenance', 'nextMaintenance', 'markerId']) {
      if (patch[k] !== undefined) a[k] = patch[k];
    }
    return { ...a };
  },

  async deleteAsset(id) {
    if (isPostgres()) {
      const r = await pool.query('DELETE FROM assets WHERE id = $1', [id]);
      return r.rowCount > 0;
    }
    const ix = mem.assets.findIndex((a) => a.id === id);
    if (ix < 0) return false;
    mem.assets.splice(ix, 1);
    mem.maintenanceRecords = mem.maintenanceRecords.filter((m) => m.assetId !== id);
    return true;
  },

  async listLocations() {
    if (isPostgres()) {
      const { rows } = await pool.query('SELECT * FROM locations ORDER BY id');
      return rows.map(mapLocation);
    }
    return mem.locations.map((l) => ({ ...l }));
  },

  async assetExists(id) { return (await this.getAsset(id)) !== null; }
};

module.exports = store;

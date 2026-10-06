'use strict';
const express = require('express');
const db = require('../db');

const router = express.Router();
const ASSET_STATUSES = ['operational', 'under_maintenance', 'faulty', 'retired'];
const err = (code, message) => ({ error: { code, message } });

function validateAssetBody(b, forCreate) {
  if (forCreate) {
    if (!b.id || typeof b.id !== 'string') return 'Field "id" (e.g. AST-001) is required.';
    if (!b.name || typeof b.name !== 'string') return 'Field "name" is required.';
    if (!b.category) return 'Field "category" is required.';
    if (!b.location) return 'Field "location" is required.';
  }
  if (b.status !== undefined && !ASSET_STATUSES.includes(b.status))
    return `Invalid status "${b.status}". Allowed: ${ASSET_STATUSES.join('|')}.`;
  if (b.markerId !== undefined && b.markerId !== '' && typeof b.markerId !== 'string')
    return 'Field "markerId" must be a string.';
  return null;
}

// IMPORTANT: marker route must be registered BEFORE /:id so "marker" isn't treated as an id.
router.get('/marker/:markerId', async (req, res) => {
  const assetId = await db.resolveMarker(req.params.markerId);
  if (!assetId) return res.status(404).json(err('MARKER_NOT_FOUND', `No asset linked to marker "${req.params.markerId}".`));
  return res.json({ assetId });
});

router.get('/', async (req, res, next) => {
  try {
    const { status, location, category } = req.query;
    if (status && !ASSET_STATUSES.includes(status))
      return res.status(400).json(err('VALIDATION_ERROR', `Invalid status filter "${status}".`));
    const assets = await db.listAssets({ status, location, category });
    return res.json(assets);
  } catch (e) { return next(e); }
});

router.get('/:id', async (req, res, next) => {
  try {
    const a = await db.getAsset(req.params.id);
    if (!a) return res.status(404).json(err('ASSET_NOT_FOUND', `Asset "${req.params.id}" not found.`));
    return res.json(a);
  } catch (e) { return next(e); }
});

router.get('/:id/maintenance', async (req, res, next) => {
  try {
    const a = await db.getAsset(req.params.id);
    if (!a) return res.status(404).json(err('ASSET_NOT_FOUND', `Asset "${req.params.id}" not found.`));
    const rows = await db.listMaintenance(req.params.id);
    return res.json(rows);
  } catch (e) { return next(e); }
});

router.post('/', async (req, res, next) => {
  try {
    const problem = validateAssetBody(req.body || {}, true);
    if (problem) return res.status(400).json(err('VALIDATION_ERROR', problem));
    if (!(await db.listLocations()).some((l) => l.id === req.body.location))
      return res.status(400).json(err('VALIDATION_ERROR', `Unknown location "${req.body.location}". See GET /api/locations.`));
    try {
      const created = await db.createAsset(req.body);
      return res.status(201).json(created);
    } catch (e) {
      if (e.code === 'DUPLICATE') return res.status(409).json(err('ASSET_CONFLICT', `Asset "${req.body.id}" already exists.`));
      if (e.code === 'DUPLICATE_MARKER') return res.status(409).json(err('MARKER_CONFLICT', `Marker "${req.body.markerId}" is already linked to another asset.`));
      throw e;
    }
  } catch (e) { return next(e); }
});

router.patch('/:id', async (req, res, next) => {
  try {
    const existing = await db.getAsset(req.params.id);
    if (!existing) return res.status(404).json(err('ASSET_NOT_FOUND', `Asset "${req.params.id}" not found.`));
    const problem = validateAssetBody(req.body || {}, false);
    if (problem) return res.status(400).json(err('VALIDATION_ERROR', problem));
    if (req.body.location !== undefined && !(await db.listLocations()).some((l) => l.id === req.body.location))
      return res.status(400).json(err('VALIDATION_ERROR', `Unknown location "${req.body.location}".`));
    try {
      const updated = await db.updateAsset(req.params.id, req.body || {});
      return res.json(updated);
    } catch (e) {
      if (e.code === 'DUPLICATE_MARKER') return res.status(409).json(err('MARKER_CONFLICT', `Marker "${req.body.markerId}" is already linked to another asset.`));
      throw e;
    }
  } catch (e) { return next(e); }
});

router.delete('/:id', async (req, res) => {
  const ok = await db.deleteAsset(req.params.id);
  if (!ok) return res.status(404).json(err('ASSET_NOT_FOUND', `Asset "${req.params.id}" not found.`));
  return res.status(204).send();
});

module.exports = router;

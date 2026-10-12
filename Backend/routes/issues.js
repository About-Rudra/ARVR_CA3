'use strict';
const express = require('express');
const db = require('../db');

const router = express.Router();
const ISSUE_STATUSES = ['open', 'in_progress', 'resolved', 'rejected', 'closed'];
const err = (code, message) => ({ error: { code, message } });

router.get('/', async (req, res, next) => {
  try {
    const { assetId, status } = req.query;
    if (status && !ISSUE_STATUSES.includes(status))
      return res.status(400).json(err('VALIDATION_ERROR', `Invalid status filter "${status}".`));
    return res.json(await db.listIssues({ assetId, status }));
  } catch (e) { return next(e); }
});

router.post('/', async (req, res, next) => {
  try {
    const { assetId, title, description, reportedBy } = req.body || {};
    if (!assetId) return res.status(400).json(err('VALIDATION_ERROR', 'Field "assetId" is required.'));
    if (!title || String(title).trim().length < 3)
      return res.status(400).json(err('VALIDATION_ERROR', 'Field "title" is required (min 3 chars).'));
    if (!(await db.assetExists(assetId)))
      return res.status(404).json(err('ASSET_NOT_FOUND', `Asset "${assetId}" not found.`));
    const created = await db.createIssue({ assetId, title: String(title).trim(), description, reportedBy });
    return res.status(201).json(created);
  } catch (e) { return next(e); }
});

router.patch('/:id/status', async (req, res, next) => {
  try {
    const { status } = req.body || {};
    if (!status || !ISSUE_STATUSES.includes(status))
      return res.status(400).json(err('VALIDATION_ERROR', `Field "status" must be one of ${ISSUE_STATUSES.join('|')}.`));
    const updated = await db.updateIssueStatus(req.params.id, status);
    if (!updated) return res.status(404).json(err('ISSUE_NOT_FOUND', `Issue "${req.params.id}" not found.`));
    return res.json(updated);
  } catch (e) { return next(e); }
});

module.exports = router;

'use strict';
/**
 * AR Passport REST API — Express entry point.
 * Unity -> Express -> Postgres (Supabase-compatible via DATABASE_URL),
 * with in-memory seed fallback when DATABASE_URL is unset.
 */
require('dotenv').config();
const express = require('express');
const cors = require('cors');

const assetsRouter = require('./routes/assets');
const issuesRouter = require('./routes/issues');
const locationsRouter = require('./routes/locations');

const app = express();
app.use(cors()); // UnityWebRequest / editor tooling: allow all origins (LAN device testing)
app.use(express.json({ limit: '1mb' }));

app.get('/health', (req, res) => res.json({ ok: true, time: new Date().toISOString() }));

app.use('/api/assets', assetsRouter);
app.use('/api/issues', issuesRouter);
app.use('/api/locations', locationsRouter);

// 404 for unknown /api routes (consistent error envelope)
app.use('/api', (req, res) => res.status(404).json({ error: { code: 'NOT_FOUND', message: `Route ${req.method} ${req.path} not found.` } }));

// Central error handler (consistent envelope, never leaks stack to Unity clients)
app.use((err, req, res, next) => {
  if (res.headersSent) return next(err);
  if (err && err.type === 'entity.parse.failed')
    return res.status(400).json({ error: { code: 'VALIDATION_ERROR', message: 'Malformed JSON body.' } });
  console.error('[api]', err && err.message ? err.message : err);
  return res.status(500).json({ error: { code: 'INTERNAL_ERROR', message: 'Unexpected server error.' } });
});

const PORT = Number(process.env.PORT) || 3000;
if (require.main === module) {
  app.listen(PORT, () => console.log(`[ar-passport] listening on http://localhost:${PORT} (postgres: ${Boolean(process.env.DATABASE_URL)})`));
}

module.exports = app;

-- AR Digital Passport for Physical Assets — PostgreSQL schema
-- Supabase-compatible (plain Postgres DDL, no extensions required).

CREATE TABLE IF NOT EXISTS locations (
  id          TEXT PRIMARY KEY,
  name        TEXT NOT NULL,
  floor       INTEGER NOT NULL DEFAULT 0,
  building    TEXT NOT NULL DEFAULT '',
  created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS assets (
  id                TEXT PRIMARY KEY CHECK (id LIKE 'AST-%'),
  name              TEXT NOT NULL,
  category          TEXT NOT NULL,
  location_id       TEXT NOT NULL REFERENCES locations(id) ON UPDATE CASCADE ON DELETE RESTRICT,
  status            TEXT NOT NULL DEFAULT 'operational'
                    CHECK (status IN ('operational','under_maintenance','faulty','retired')),
  description       TEXT NOT NULL DEFAULT '',
  image_url         TEXT NOT NULL DEFAULT '',
  model_url         TEXT NOT NULL DEFAULT '',
  last_maintenance  DATE,
  next_maintenance  DATE,
  created_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  CHECK (next_maintenance IS NULL OR last_maintenance IS NULL OR next_maintenance >= last_maintenance)
);

-- One marker per asset and vice versa (physical QR/image-target <-> asset).
CREATE TABLE IF NOT EXISTS asset_markers (
  marker_id   TEXT PRIMARY KEY CHECK (marker_id LIKE 'MARK-%'),
  asset_id    TEXT NOT NULL UNIQUE REFERENCES assets(id) ON UPDATE CASCADE ON DELETE CASCADE,
  created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS maintenance_records (
  id          TEXT PRIMARY KEY CHECK (id LIKE 'MNT-%'),
  asset_id    TEXT NOT NULL REFERENCES assets(id) ON UPDATE CASCADE ON DELETE CASCADE,
  date        DATE NOT NULL,
  type        TEXT NOT NULL DEFAULT 'preventive'
              CHECK (type IN ('preventive','corrective','calibration','inspection')),
  technician  TEXT NOT NULL DEFAULT '',
  notes       TEXT NOT NULL DEFAULT '',
  created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS issues (
  id          TEXT PRIMARY KEY CHECK (id LIKE 'ISS-%'),
  asset_id    TEXT NOT NULL REFERENCES assets(id) ON UPDATE CASCADE ON DELETE CASCADE,
  title       TEXT NOT NULL CHECK (char_length(title) >= 3),
  description TEXT NOT NULL DEFAULT '',
  status      TEXT NOT NULL DEFAULT 'open'
              CHECK (status IN ('open','in_progress','resolved','rejected','closed')),
  reported_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  reported_by TEXT NOT NULL DEFAULT 'anonymous',
  created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Migration (idempotent): databases created before 'rejected' was added keep the
-- old CHECK because CREATE TABLE IF NOT EXISTS skips them; replace it here.
ALTER TABLE issues DROP CONSTRAINT IF EXISTS issues_status_check;
ALTER TABLE issues ADD CONSTRAINT issues_status_check
  CHECK (status IN ('open','in_progress','resolved','rejected','closed'));

CREATE TABLE IF NOT EXISTS admins (
  id            SERIAL PRIMARY KEY,
  username      TEXT NOT NULL UNIQUE,
  password_hash TEXT NOT NULL,
  created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Indexes for common lookups
CREATE INDEX IF NOT EXISTS idx_assets_location ON assets(location_id);
CREATE INDEX IF NOT EXISTS idx_assets_status ON assets(status);
CREATE INDEX IF NOT EXISTS idx_maintenance_asset ON maintenance_records(asset_id);
CREATE INDEX IF NOT EXISTS idx_maintenance_date ON maintenance_records(date DESC);
CREATE INDEX IF NOT EXISTS idx_issues_asset ON issues(asset_id);
CREATE INDEX IF NOT EXISTS idx_issues_status ON issues(status);

-- Auto-bump updated_at on UPDATE
CREATE OR REPLACE FUNCTION set_updated_at()
RETURNS TRIGGER AS $$
BEGIN
  NEW.updated_at = NOW();
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_assets_updated ON assets;
CREATE TRIGGER trg_assets_updated BEFORE UPDATE ON assets
  FOR EACH ROW EXECUTE FUNCTION set_updated_at();

DROP TRIGGER IF EXISTS trg_maintenance_updated ON maintenance_records;
CREATE TRIGGER trg_maintenance_updated BEFORE UPDATE ON maintenance_records
  FOR EACH ROW EXECUTE FUNCTION set_updated_at();

DROP TRIGGER IF EXISTS trg_issues_updated ON issues;
CREATE TRIGGER trg_issues_updated BEFORE UPDATE ON issues
  FOR EACH ROW EXECUTE FUNCTION set_updated_at();

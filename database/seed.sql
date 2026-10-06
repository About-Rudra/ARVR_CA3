-- AR Digital Passport — idempotent seed (matches Assets/Resources/seed_assets.json)
-- Run: psql $DATABASE_URL -f database/schema.sql -f database/seed.sql

INSERT INTO locations (id, name, floor, building) VALUES
  ('CL-6',     'Computer Lab 6',  2, 'C-Block'),
  ('FAB-LAB',  'Fabrication Lab', 1, 'Workshop Block'),
  ('AR-LAB',   'AR/VR Lab',       2, 'C-Block')
ON CONFLICT (id) DO UPDATE SET name=EXCLUDED.name, floor=EXCLUDED.floor, building=EXCLUDED.building;

INSERT INTO assets (id, name, category, location_id, status, description, image_url, model_url, last_maintenance, next_maintenance) VALUES
  ('AST-001','Epson EB-X49 Projector','AV Equipment','CL-6','operational',
   '3LCD XGA projector, 3600 lumens. Ceiling-mounted in Computer Lab 6, used for lectures and demos.',
   'https://example.com/images/ast-001.jpg','', '2026-07-15','2026-10-15'),
  ('AST-002','Creality Ender-3 3D Printer','Fabrication','FAB-LAB','operational',
   'FDM 3D printer, 220x220x250mm build volume. Shared fabrication-lab asset.',
   'https://example.com/images/ast-002.jpg','', '2026-08-20','2026-11-20'),
  ('AST-003','Tektronix TBS1052B Oscilloscope','Lab Equipment','FAB-LAB','under_maintenance',
   '50 MHz 2-channel digital storage oscilloscope on electronics bench. Currently in calibration.',
   'https://example.com/images/ast-003.jpg','', '2026-06-01','2026-10-10'),
  ('AST-004','Meta Quest 2 VR Headset','AR/VR','AR-LAB','operational',
   '128GB standalone VR headset, demo unit for AR/VR lab coursework.',
   'https://example.com/images/ast-004.jpg','', '2026-09-01','2026-12-01'),
  ('AST-005','Daikin 1.5T Split Air Conditioner','HVAC','CL-6','faulty',
   '1.5 ton 5-star split AC serving Computer Lab 6. Reported weak cooling, suspected gas leak.',
   'https://example.com/images/ast-005.jpg','', '2026-05-12','2026-08-12')
ON CONFLICT (id) DO UPDATE SET
  name=EXCLUDED.name, category=EXCLUDED.category, location_id=EXCLUDED.location_id,
  status=EXCLUDED.status, description=EXCLUDED.description,
  image_url=EXCLUDED.image_url, model_url=EXCLUDED.model_url,
  last_maintenance=EXCLUDED.last_maintenance, next_maintenance=EXCLUDED.next_maintenance;

INSERT INTO asset_markers (marker_id, asset_id) VALUES
  ('MARK-001','AST-001'),
  ('MARK-002','AST-002'),
  ('MARK-003','AST-003'),
  ('MARK-004','AST-004'),
  ('MARK-005','AST-005')
ON CONFLICT (marker_id) DO UPDATE SET asset_id=EXCLUDED.asset_id;

INSERT INTO maintenance_records (id, asset_id, date, type, technician, notes) VALUES
  ('MNT-001','AST-001','2026-07-15','preventive','R. Kumar','Filter cleaned, lamp hours checked (812h). Focus recalibrated.'),
  ('MNT-002','AST-001','2026-04-10','corrective','R. Kumar','Replaced HDMI cable, fixed intermittent signal drop.'),
  ('MNT-003','AST-002','2026-08-20','preventive','S. Iyer','Bed levelled, nozzle replaced, belts tensioned.'),
  ('MNT-004','AST-003','2026-06-01','calibration','P. Nair','Annual calibration started. Channel 2 probe reads 5% high; sent for probe replacement.'),
  ('MNT-005','AST-004','2026-09-01','preventive','A. Sharma','Factory reset, straps sanitised, controllers paired, firmware v62.'),
  ('MNT-006','AST-005','2026-05-12','preventive','K. Singh','Filters washed, condenser coil cleaned. Cooling weak; flagged for gas-pressure check.')
ON CONFLICT (id) DO UPDATE SET
  asset_id=EXCLUDED.asset_id, date=EXCLUDED.date, type=EXCLUDED.type,
  technician=EXCLUDED.technician, notes=EXCLUDED.notes;

INSERT INTO issues (id, asset_id, title, description, status, reported_at, reported_by) VALUES
  ('ISS-001','AST-003','Channel 2 reads high','Channel 2 shows ~5% higher voltage than calibrated source.','in_progress','2026-09-18T10:30:00Z','lab-assistant'),
  ('ISS-002','AST-005','Weak cooling','Room not cooling below 27C even at 18C setpoint. Suspected refrigerant leak.','open','2026-09-28T14:00:00Z','faculty-cl6'),
  ('ISS-003','AST-002','Filament jam','PLA jammed in extruder during overnight print. Cleared, test print OK.','resolved','2026-08-25T09:15:00Z','student-fablab')
ON CONFLICT (id) DO UPDATE SET
  asset_id=EXCLUDED.asset_id, title=EXCLUDED.title, description=EXCLUDED.description,
  status=EXCLUDED.status, reported_at=EXCLUDED.reported_at, reported_by=EXCLUDED.reported_by;

-- Default admin (password is bcrypt hash of 'admin123' placeholder; replace in production)
INSERT INTO admins (username, password_hash) VALUES
  ('admin', '$2b$10$placeholderhashreplaceinproduction0000000000000')
ON CONFLICT (username) DO NOTHING;

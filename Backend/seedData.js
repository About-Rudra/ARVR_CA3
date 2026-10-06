'use strict';
/**
 * Single source of seed truth for the in-memory fallback store.
 * Mirrors Assets/Resources/seed_assets.json and database/seed.sql.
 */
const seed = {
  locations: [
    { id: 'CL-6', name: 'Computer Lab 6', floor: 2, building: 'C-Block' },
    { id: 'FAB-LAB', name: 'Fabrication Lab', floor: 1, building: 'Workshop Block' },
    { id: 'AR-LAB', name: 'AR/VR Lab', floor: 2, building: 'C-Block' }
  ],
  assets: [
    {
      id: 'AST-001', name: 'Epson EB-X49 Projector', category: 'AV Equipment', location: 'CL-6',
      status: 'operational', description: '3LCD XGA projector, 3600 lumens. Ceiling-mounted in Computer Lab 6, used for lectures and demos.',
      imageUrl: 'https://example.com/images/ast-001.jpg', modelUrl: '', lastMaintenance: '2026-07-15', nextMaintenance: '2026-10-15', markerId: 'MARK-001'
    },
    {
      id: 'AST-002', name: 'Creality Ender-3 3D Printer', category: 'Fabrication', location: 'FAB-LAB',
      status: 'operational', description: 'FDM 3D printer, 220x220x250mm build volume. Shared fabrication-lab asset.',
      imageUrl: 'https://example.com/images/ast-002.jpg', modelUrl: '', lastMaintenance: '2026-08-20', nextMaintenance: '2026-11-20', markerId: 'MARK-002'
    },
    {
      id: 'AST-003', name: 'Tektronix TBS1052B Oscilloscope', category: 'Lab Equipment', location: 'FAB-LAB',
      status: 'under_maintenance', description: '50 MHz 2-channel digital storage oscilloscope on electronics bench. Currently in calibration.',
      imageUrl: 'https://example.com/images/ast-003.jpg', modelUrl: '', lastMaintenance: '2026-06-01', nextMaintenance: '2026-10-10', markerId: 'MARK-003'
    },
    {
      id: 'AST-004', name: 'Meta Quest 2 VR Headset', category: 'AR/VR', location: 'AR-LAB',
      status: 'operational', description: '128GB standalone VR headset, demo unit for AR/VR lab coursework.',
      imageUrl: 'https://example.com/images/ast-004.jpg', modelUrl: '', lastMaintenance: '2026-09-01', nextMaintenance: '2026-12-01', markerId: 'MARK-004'
    },
    {
      id: 'AST-005', name: 'Daikin 1.5T Split Air Conditioner', category: 'HVAC', location: 'CL-6',
      status: 'faulty', description: '1.5 ton 5-star split AC serving Computer Lab 6. Reported weak cooling, suspected gas leak.',
      imageUrl: 'https://example.com/images/ast-005.jpg', modelUrl: '', lastMaintenance: '2026-05-12', nextMaintenance: '2026-08-12', markerId: 'MARK-005'
    }
  ],
  maintenanceRecords: [
    { id: 'MNT-001', assetId: 'AST-001', date: '2026-07-15', type: 'preventive', technician: 'R. Kumar', notes: 'Filter cleaned, lamp hours checked (812h). Focus recalibrated.' },
    { id: 'MNT-002', assetId: 'AST-001', date: '2026-04-10', type: 'corrective', technician: 'R. Kumar', notes: 'Replaced HDMI cable, fixed intermittent signal drop.' },
    { id: 'MNT-003', assetId: 'AST-002', date: '2026-08-20', type: 'preventive', technician: 'S. Iyer', notes: 'Bed levelled, nozzle replaced, belts tensioned.' },
    { id: 'MNT-004', assetId: 'AST-003', date: '2026-06-01', type: 'calibration', technician: 'P. Nair', notes: 'Annual calibration started. Channel 2 probe reads 5% high; sent for probe replacement.' },
    { id: 'MNT-005', assetId: 'AST-004', date: '2026-09-01', type: 'preventive', technician: 'A. Sharma', notes: 'Factory reset, straps sanitised, controllers paired, firmware v62.' },
    { id: 'MNT-006', assetId: 'AST-005', date: '2026-05-12', type: 'preventive', technician: 'K. Singh', notes: 'Filters washed, condenser coil cleaned. Cooling weak; flagged for gas-pressure check.' }
  ],
  issues: [
    { id: 'ISS-001', assetId: 'AST-003', title: 'Channel 2 reads high', description: 'Channel 2 shows ~5% higher voltage than calibrated source.', status: 'in_progress', reportedAt: '2026-09-18T10:30:00Z', reportedBy: 'lab-assistant' },
    { id: 'ISS-002', assetId: 'AST-005', title: 'Weak cooling', description: 'Room not cooling below 27C even at 18C setpoint. Suspected refrigerant leak.', status: 'open', reportedAt: '2026-09-28T14:00:00Z', reportedBy: 'faculty-cl6' },
    { id: 'ISS-003', assetId: 'AST-002', title: 'Filament jam', description: 'PLA jammed in extruder during overnight print. Cleared, test print OK.', status: 'resolved', reportedAt: '2026-08-25T09:15:00Z', reportedBy: 'student-fablab' }
  ]
};

function deepClone() {
  return JSON.parse(JSON.stringify(seed));
}

module.exports = { seed, deepClone };

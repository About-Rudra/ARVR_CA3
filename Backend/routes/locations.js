'use strict';
const express = require('express');
const db = require('../db');

const router = express.Router();

router.get('/', async (req, res, next) => {
  try {
    return res.json(await db.listLocations());
  } catch (e) { return next(e); }
});

module.exports = router;

#!/usr/bin/env node
/*
 * Smoke test for the QA viewer: starts serve.py, drives the page in headless
 * Chromium (Playwright library, no test runner needed) against the synthetic
 * sample, and writes screenshots to tools/qa-viewer/screenshots/.
 *
 *   node tools/qa-viewer/tests/smoke.cjs            # basemap screenshot tries OSM tiles
 *   node tools/qa-viewer/tests/smoke.cjs --offline  # never request OSM tiles
 *
 * Playwright is resolved from NODE_PATH / global modules (it is not a repo
 * dependency). Browsers come from PLAYWRIGHT_BROWSERS_PATH.
 *
 * Behind an HTTPS-intercepting egress proxy (HTTPS_PROXY set, e.g. the agent
 * proxy in CI sandboxes) Chromium's NSS store may not trust the proxy CA. The
 * test then routes the two external hosts the page uses (cdnjs.cloudflare.com
 * for Leaflet, tile.openstreetmap.org for the basemap) through Playwright's
 * Node-side fetch (route.fetch), which verifies TLS against NODE_EXTRA_CA_CERTS.
 * TLS verification is never disabled; the page still checks Leaflet's SRI hash.
 */
'use strict';

process.env.PLAYWRIGHT_DISABLE_FORCED_CHROMIUM_PROXIED_LOOPBACK = '1'; // keep localhost off the proxy

const path = require('path');
const fs = require('fs');
const { spawn, spawnSync } = require('child_process');

let chromium;
try {
  ({ chromium } = require('playwright'));
} catch {
  console.error('playwright is not installed for node (set NODE_PATH to the global node_modules).');
  process.exit(2);
}

const VIEWER = path.resolve(__dirname, '..');
const ROOT = path.resolve(VIEWER, '..', '..');
const SHOTS = path.join(VIEWER, 'screenshots');
const OFFLINE = process.argv.includes('--offline');
const MAX_SHOT_BYTES = 300 * 1024;

const results = [];
let failures = 0;
function check(name, cond, detail) {
  results.push({ name, ok: !!cond, detail });
  if (!cond) failures++;
  console.log(`${cond ? 'PASS' : 'FAIL'}  ${name}${detail !== undefined ? '  ' + (typeof detail === 'string' ? detail : JSON.stringify(detail)) : ''}`);
}

function startServer() {
  return new Promise((resolve, reject) => {
    const proc = spawn('python3', [path.join(VIEWER, 'serve.py'), '--port', '0', '--quiet', '--root', ROOT],
      { stdio: ['ignore', 'pipe', 'pipe'] });
    let out = '';
    const timer = setTimeout(() => reject(new Error('serve.py did not start: ' + out)), 10000);
    proc.stdout.on('data', (d) => {
      out += d;
      const m = out.match(/Serving .* at (http:\/\/localhost:(\d+)\/)/);
      // wait for the dataset list that follows the "Serving" line
      if (m && /\(synthetic sample\)|no pipeline QA export/.test(out)) { clearTimeout(timer); resolve({ proc, base: m[1], banner: out }); }
    });
    proc.stderr.on('data', (d) => { out += d; });
    proc.on('exit', (code) => reject(new Error(`serve.py exited ${code}: ${out}`)));
  });
}

async function newContext(browser, opts = {}) {
  const context = await browser.newContext({ viewport: { width: 1200, height: 750 }, deviceScaleFactor: 1, ...opts });
  const external = { cdn: 0, osm: 0, osmFailed: 0 };
  if (process.env.HTTPS_PROXY) {
    await context.route(/^https:\/\/(cdnjs\.cloudflare\.com|tile\.openstreetmap\.org)\//, async (route) => {
      const url = route.request().url();
      const isOsm = url.includes('tile.openstreetmap.org');
      if (isOsm && OFFLINE) { external.osmFailed++; return route.abort(); }
      try {
        const resp = await route.fetch({ timeout: 20000 });
        if (isOsm) external.osm++; else external.cdn++;
        await route.fulfill({ response: resp });
      } catch {
        if (isOsm) external.osmFailed++;
        await route.abort().catch(() => {});
      }
    });
  } else if (OFFLINE) {
    await context.route(/^https:\/\/tile\.openstreetmap\.org\//, (route) => { external.osmFailed++; return route.abort(); });
  }
  return { context, external };
}

function watch(page) {
  const log = { errors: [], requests: [], all: [] };
  page.on('console', (m) => {
    log.all.push(`[${m.type()}] ${m.text()}`);
    if (m.type() === 'error') log.errors.push(m.text());
  });
  page.on('pageerror', (e) => log.errors.push('pageerror: ' + e.message));
  page.on('request', (r) => log.requests.push(r.url()));
  return log;
}
// Basemap tiles may fail (offline, policy, proxy); that is not a viewer error.
const realErrors = (log) => log.errors.filter((e) => !/Failed to load resource: net::ERR_(FAILED|ABORTED|INTERNET_DISCONNECTED|NAME_NOT_RESOLVED|TIMED_OUT|CONNECTION)/.test(e));

async function settle(page) {
  await page.evaluate(async () => {
    await window.QA.ready;
    for (let k = 0; k < 3; k++) {
      await window.QA.idle();
      await new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r)));
    }
  });
}

let navCounter = 0;
async function open(page, base, query, hash) {
  // A unique query param forces a full page load (a hash-only change would be a same-document navigation).
  await page.goto(`${base}tools/qa-viewer/?${query}&_nav=${++navCounter}${hash ? '#' + hash : ''}`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.QA && window.QA.app, null, { timeout: 20000 });
  await settle(page);
}

async function clickLatLng(page, lat, lon) {
  const pt = await page.evaluate(([la, lo]) => {
    window.QA.app.clearSelection(); // the inspector panel must not cover the click target
    const p = window.QA.map.latLngToContainerPoint([la, lo]);
    const r = window.QA.map.getContainer().getBoundingClientRect();
    return { x: r.left + p.x, y: r.top + p.y };
  }, [lat, lon]);
  await page.mouse.click(pt.x, pt.y);
  await page.waitForTimeout(150);
}

async function shot(page, name) {
  const raw = path.join(SHOTS, name);
  await page.screenshot({ path: raw });
  // Keep committed screenshots small: quantise to a 256-colour PNG when Pillow is available.
  // MAXCOVERAGE keeps small-but-saturated legend colours (median cut turned purple POIs grey).
  const py = spawnSync('python3', ['-c', `
import sys
from PIL import Image
p = sys.argv[1]
im = Image.open(p).convert('RGB').quantize(colors=256, method=Image.Quantize.MAXCOVERAGE)
im.save(p, optimize=True)
`, raw]);
  const size = fs.statSync(raw).size;
  check(`screenshot ${name} < 300 KB`, size < MAX_SHOT_BYTES, `${Math.round(size / 1024)} KB${py.status === 0 ? '' : ' (not quantised: Pillow missing)'}`);
}

(async () => {
  fs.mkdirSync(SHOTS, { recursive: true });
  const server = await startServer();
  const base = server.base;
  console.log(`serve.py at ${base}`);
  check('serve.py prints the sample URL', /tools\/qa-viewer\/\?data=sample\/qa/.test(server.banner));

  const launch = {};
  if (process.env.HTTPS_PROXY) launch.proxy = { server: process.env.HTTPS_PROXY, bypass: 'localhost,127.0.0.1' };
  const browser = await chromium.launch(launch);
  const { context, external } = await newContext(browser);
  const page = await context.newPage();
  const log = watch(page);
  const H16 = '16/27.71550/85.31000/roads,trails,buildings,areas,lines,pois,places,hillshade,biome,grid/base=none';

  try {
    // ------------------------------------------------------------ 1. load + render
    await open(page, base, 'data=sample/qa', H16);
    const s1 = await page.evaluate(() => {
      const idx = window.QA.index;
      const layers = {};
      for (const [k, l] of Object.entries(window.QA.layers)) layers[k] = { state: l.state, count: l.count, drawn: l.rendered.size };
      const panes = {};
      for (const id of ['roads', 'trails', 'buildings', 'areas', 'lines', 'pois', 'places', 'grid']) {
        panes[id] = document.querySelectorAll(`.leaflet-qa-${id}-pane canvas`).length;
      }
      const imgs = {};
      for (const kind of ['hillshade', 'biome']) {
        imgs[kind] = [...document.querySelectorAll(`.qa-raster-${kind} img`)].map((im) => ({ w: im.naturalWidth, t: im.style.transform }));
      }
      return { region: idx.region, expected: Object.fromEntries(Object.entries(idx.layers).map(([k, v]) => [k, v.count])),
        layers, panes, imgs, grid: window.QA.grid.rendered.size, errors: window.QA.errors, zoom: window.QA.map.getZoom(),
        listText: document.getElementById('layer-list').innerText };
    });
    check('index.json loaded (sample_thamel)', s1.region === 'sample_thamel', s1.region);
    check('no viewer errors', s1.errors.length === 0, s1.errors);
    for (const id of ['roads', 'trails', 'buildings', 'areas', 'lines', 'pois', 'places']) {
      check(`${id}: loaded count matches index.json`, s1.layers[id].state === 'ready' && s1.layers[id].count === s1.expected[id],
        `${s1.layers[id].count} / ${s1.expected[id]}`);
      check(`${id}: rendered on a canvas at z16`, s1.layers[id].drawn > 0 && s1.panes[id] === 1, `${s1.layers[id].drawn} drawn`);
    }
    check('hillshade: 2 tiles placed (129 px, affine matrix)', s1.imgs.hillshade.length === 2 &&
      s1.imgs.hillshade.every((i) => i.w === 129 && /^matrix\(/.test(i.t)), s1.imgs.hillshade);
    check('biome: 2 tiles placed (65 px)', s1.imgs.biome.length === 2 && s1.imgs.biome.every((i) => i.w === 65), s1.imgs.biome.map((i) => i.w));
    check('tile grid: 2 leaf tiles drawn', s1.grid === 2 && s1.panes.grid === 1, s1.grid);
    check('layer list shows layer names', /POIs/.test(s1.listText) && /Places/.test(s1.listText) && !/=>/.test(s1.listText));

    // ------------------------------------------------------------ 2. geometry: rasters + projection
    const geo = await page.evaluate(() => {
      const QA = window.QA;
      const t = QA.index.tiles.find((tt) => tt.level === 10 && tt.tx === 516);
      const js = QA.proj.tileCorners(10, 516, 162);
      const cornerErr = Math.max(...t.corners_lonlat.map((c, k) => Math.max(Math.abs(c[0] - js[k][0]), Math.abs(c[1] - js[k][1]))));
      // vertex registration: pixel centre (0.5, 0.5) must land exactly on the NW corner
      const img = [...document.querySelectorAll('.qa-raster-hillshade img')].find((im) => im._tile.id === '10/516/162');
      const [a, b, c, d, e, f] = img._m;
      const lp = (ll) => QA.map.project(ll).subtract(QA.map.getPixelOrigin()); // unrounded, as the viewer places rasters
      const p = lp([t.corners_lonlat[3][1], t.corners_lonlat[3][0]]);
      const q = { x: a * 0.5 + c * 0.5 + e, y: b * 0.5 + d * 0.5 + f };
      // SE corner: pixel centre (w-0.5, h-0.5)
      const w = img.naturalWidth; const h = img.naturalHeight;
      const pse = lp([t.corners_lonlat[1][1], t.corners_lonlat[1][0]]);
      const qse = { x: a * (w - 0.5) + c * (h - 0.5) + e, y: b * (w - 0.5) + d * (h - 0.5) + f };
      return { cornerErr, nwErr: Math.hypot(p.x - q.x, p.y - q.y), seErr: Math.hypot(pse.x - qse.x, pse.y - qse.y),
        rotDeg: Math.atan2(t.corners_lonlat[3][0] - t.corners_lonlat[0][0], 1) };
    });
    check('JS tile corners match index (pyproj) corners < 2e-7 deg', geo.cornerErr < 2e-7, geo.cornerErr);
    check('raster NW pixel centre on tile corner (< 0.05 px)', geo.nwErr < 0.05, geo.nwErr);
    check('raster SE pixel centre on tile corner (< 0.6 px, affine from 3 corners)', geo.seErr < 0.6, geo.seErr);

    const ref = spawnSync('python3', [path.join(__dirname, 'proj_reference.py')], { encoding: 'utf8' });
    if (ref.status === 0) {
      const pts = JSON.parse(ref.stdout).points;
      const err = await page.evaluate((P) => {
        let fwd = 0; let inv = 0;
        for (const [lon, lat, x, z] of P) {
          const [gx, gz] = window.QA.proj.lonLatToGame(lon, lat);
          fwd = Math.max(fwd, Math.hypot(gx - x, gz - z));
          const [lo, la] = window.QA.proj.gameToLonLat(x, z);
          inv = Math.max(inv, Math.abs(lo - lon), Math.abs(la - lat));
        }
        return { fwd, inv };
      }, pts);
      check(`JS NPL-TM84 vs pyproj over Nepal (${pts.length} pts): forward < 1 mm`, err.fwd < 0.001, `${(err.fwd * 1000).toFixed(4)} mm`);
      check('JS NPL-TM84 inverse < 1e-8 deg', err.inv < 1e-8, err.inv);
    } else {
      console.log('SKIP  projection vs pyproj (pyproj not available)');
    }

    // context points (ROAD/LINE pieces cut at a tile edge) must be detected and not drawn
    const ctxr = await page.evaluate(() => {
      const QA = window.QA;
      const out = { stripped: {}, bad: 0, checked: 0 };
      for (const id of ['roads', 'trails', 'lines']) {
        const l = QA.layers[id];
        out.stripped[id] = l.ctxStripped;
        for (let i = 0; i < l.count; i++) {
          const f = l.features[i];
          const n = l.norm[i];
          const [L_, tx, ty] = f.properties.tile.split('/').map(Number);
          const S = QA.proj.tileSize(L_);
          const c = f.geometry.coordinates;
          const ends = [];
          if (n.flags.includes('HAS_PREV_CTX')) ends.push(c[0]);
          if (n.flags.includes('HAS_NEXT_CTX')) ends.push(c[c.length - 1]);
          for (const p of ends) {
            out.checked++;
            const [x, z] = QA.proj.lonLatToGame(p[0], p[1]);
            const dx = Math.min(Math.abs(x - tx * S), Math.abs(x - (tx + 1) * S));
            const dz = Math.min(Math.abs(z - ty * S), Math.abs(z - (ty + 1) * S));
            if (Math.min(dx, dz) > 0.05) out.bad++; // the drawn end must lie on the tile border
          }
          if (ends.length && !f._ctx) out.bad++;
        }
      }
      return out;
    });
    check('context points stripped: drawn ends lie on the tile border', ctxr.checked >= 10 && ctxr.bad === 0 &&
      ctxr.stripped.roads > 0 && ctxr.stripped.lines > 0, ctxr);

    // ------------------------------------------------------------ 3. styling modes
    const modes = await page.evaluate(() => {
      const QA = window.QA;
      const P = QA.palettes;
      const out = {};
      const roads = QA.layers.roads;
      for (const mode of ['source', 'surface', 'class', 'sac']) {
        document.getElementById('road-mode').value = mode;
        document.getElementById('road-mode').dispatchEvent(new Event('change'));
        let bad = 0;
        for (let i = 0; i < roads.count; i++) {
          const n = roads.norm[i];
          const st = QA.styleOf('roads', i);
          const want = mode === 'source' ? P.SOURCE_COLORS[n.source] : mode === 'surface' ? P.SURFACE_COLORS[n.surface]
            : mode === 'class' ? P.CLASS_COLORS[n.cls] : (n.sac === 'UNKNOWN' ? '#9aa0a6' : P.SAC_COLORS[n.sac]);
          if (st.color !== want) bad++;
        }
        out[mode] = { bad, legendRows: document.querySelectorAll('#legend .lg-row[data-key^="road:"]').length, hash: location.hash.includes(`road=${mode}`) };
      }
      const t = QA.layers.trails;
      let trailBad = 0;
      for (let i = 0; i < t.count; i++) if (QA.styleOf('trails', i).color !== P.SAC_COLORS[t.norm[i].sac]) trailBad++;
      out.trailsSac = trailBad;
      const b = QA.layers.buildings;
      for (const mode of ['levels', 'levels_source', 'archetype']) {
        document.getElementById('building-mode').value = mode;
        document.getElementById('building-mode').dispatchEvent(new Event('change'));
        let bad = 0;
        for (let i = 0; i < b.count; i++) {
          const n = b.norm[i];
          const want = mode === 'archetype' ? P.ARCHETYPE_COLORS[n.arche] : mode === 'levels_source'
            ? (n.levelsInferred ? '#fd8d3c' : '#1a9850') : P.LEVEL_COLORS[n.levels >= 8 ? '8+' : String(n.levels)];
          if (b.rendered.has(i) && QA.styleOf('buildings', i).fillColor !== want) bad++;
        }
        out['b_' + mode] = { bad, legendRows: document.querySelectorAll('#legend .lg-row[data-key^="bldg:"]').length };
      }
      const sources = new Set(roads.norm.map((n) => n.source));
      const surfaces = new Set([...roads.norm, ...t.norm].map((n) => n.surface));
      return { out, sources: [...sources], surfaces: [...surfaces] };
    });
    for (const [k, v] of Object.entries(modes.out)) {
      if (typeof v === 'number') check(`trails coloured by sac_scale in sac mode`, v === 0, v);
      else check(`mode ${k}: every drawn feature has its palette colour`, v.bad === 0 && v.legendRows > 0 && (v.hash !== false),
        v);
    }
    check('sample covers all 4 surface sources', modes.sources.length === 4, modes.sources);
    check('sample covers all 14 surfaces', modes.surfaces.length === 14, modes.surfaces);

    // legend click hides a category (DEFAULT roads) and brings it back
    await page.selectOption('#road-mode', 'source');
    await settle(page);
    const before = await page.evaluate(() => window.QA.layers.roads.rendered.size);
    const nDefault = await page.evaluate(() => [...window.QA.layers.roads.rendered.keys()].filter((i) => window.QA.layers.roads.norm[i].source === 'DEFAULT').length);
    await page.click('#legend .lg-row[data-cat="DEFAULT"]');
    await settle(page);
    const afterHide = await page.evaluate(() => window.QA.layers.roads.rendered.size);
    await page.click('#legend .lg-row[data-cat="DEFAULT"]');
    await settle(page);
    const afterShow = await page.evaluate(() => window.QA.layers.roads.rendered.size);
    check('legend click hides/shows a category', nDefault > 0 && afterHide === before - nDefault && afterShow === before,
      { before, nDefault, afterHide, afterShow });

    // ------------------------------------------------------------ 4. inspector
    const targets = await page.evaluate(() => {
      const QA = window.QA;
      const find = (id, pred) => { const l = QA.layers[id]; for (let i = 0; i < l.count; i++) if (pred(l.features[i].properties, l.norm[i])) return i; return -1; };
      const poi = find('pois', (p) => p.name === 'Ganesh Mandir');
      const bld = find('buildings', (p) => p.name === 'Test: TERAI');
      const road = find('roads', (p) => p.name === 'Test lane (snow)');
      const c = (id, i) => { const g = QA.layers[id].features[i].geometry; return g; };
      const ring = c('buildings', bld).coordinates[0];
      const bc = ring.slice(0, -1).reduce((a, p) => [a[0] + p[0] / (ring.length - 1), a[1] + p[1] / (ring.length - 1)], [0, 0]);
      const rl = c('roads', road).coordinates;
      const mid = [(rl[0][0] + rl[1][0]) / 2, (rl[0][1] + rl[1][1]) / 2];
      return { poi: c('pois', poi).coordinates, bld: bc, road: mid };
    });
    await page.evaluate(() => { window.QA.map.setView([27.7155, 85.3135], 16, { animate: false }); });
    await settle(page);
    await clickLatLng(page, targets.poi[1], targets.poi[0]);
    let insp = await page.evaluate(() => ({ title: document.getElementById('inspector-title').textContent,
      hidden: document.getElementById('inspector').hidden, body: document.getElementById('inspector-body').innerText,
      popup: ([...document.querySelectorAll('.leaflet-popup-content')].pop() || {}).innerText || '' }));
    check('click POI -> inspector shows it', !insp.hidden && insp.title === 'Ganesh Mandir' && /TEMPLE_HINDU/.test(insp.body), insp.title);
    check('POI inspector lists raw properties and location', /All properties/.test(insp.body) && /leaf tile/.test(insp.body) && /10\/516\/162/.test(insp.body));
    await clickLatLng(page, targets.bld[1], targets.bld[0]);
    insp = await page.evaluate(() => ({ title: document.getElementById('inspector-title').textContent,
      body: document.getElementById('inspector-body').innerText, popup: ([...document.querySelectorAll('.leaflet-popup-content')].pop() || {}).innerText || '' }));
    check('click building -> archetype + levels in popup and inspector', insp.title === 'Test: TERAI' && /levels/.test(insp.popup) &&
      /TERAI/.test(insp.body) && /levels\s+\d/.test(insp.body), { title: insp.title, popup: insp.popup });
    await page.evaluate(() => { window.QA.setLayerVisible('biome', true); window.QA.setLayerVisible('hillshade', true); });
    await settle(page);
    await clickLatLng(page, targets.road[1], targets.road[0]);
    insp = await page.evaluate(() => ({ title: document.getElementById('inspector-title').textContent, body: document.getElementById('inspector-body').innerText }));
    check('click road -> surface + surface_source', insp.title === 'Test lane (snow)' && /SNOW_ICE/.test(insp.body) && /surface_source\s+DEFAULT/.test(insp.body), insp.title);
    check('inspector samples biome + hillshade PNG under the click', /biome\s+\S+/.test(insp.body) && /hillshade\s+\d+ \/ 255/.test(insp.body) &&
      !/no palette match/.test(insp.body), insp.body.split('\n').filter((l) => /biome|hillshade/.test(l)));
    await page.keyboard.press('Escape');
    check('Escape closes the inspector', await page.evaluate(() => document.getElementById('inspector').hidden));

    // ------------------------------------------------------------ 5. search
    await page.fill('#search', 'ganesh');
    await page.waitForTimeout(250);
    const sr = await page.evaluate(() => [...document.querySelectorAll('#search-results li')].map((li) => li.innerText));
    check('search "ganesh" lists Ganesh Mandir', sr.some((t) => /Ganesh Mandir/.test(t)), sr.slice(0, 3));
    await page.press('#search', 'Enter');
    await page.waitForTimeout(700);
    await settle(page);
    const afterSearch = await page.evaluate(([lon, lat]) => ({ d: window.QA.map.getCenter().distanceTo([lat, lon]),
      title: document.getElementById('inspector-title').textContent }), targets.poi);
    check('Enter pans to the result and selects it', afterSearch.d < 30 && afterSearch.title === 'Ganesh Mandir', afterSearch);
    const deva = await page.evaluate(() => window.QA.search('ठमेल').map((r) => r.name));
    check('Devanagari search finds the place', deva.includes('Thamel'), deva);
    const roadRes = await page.evaluate(() => window.QA.search('madhya').map((r) => `${r.name}|${r.layerId}|${r.idx.length}`));
    check('road names searchable, clipped pieces merged', roadRes.some((r) => /^Madhya Marg\|roads\|2$/.test(r)), roadRes);
    const tileRes = await page.evaluate(() => window.QA.search('10/517/162'));
    check('tile id search', tileRes.length === 1 && tileRes[0].special === 'tile');
    const idRes = await page.evaluate(() => {
      const p = window.QA.layers.roads.features[0].properties;
      return { want: p.osm_way_id, got: window.QA.search('w' + p.osm_way_id).map((r) => r.layerId) };
    });
    check('OSM id search (w<id>)', idRes.got.length >= 1 && idRes.got.every((l) => l === 'roads'), idRes);
    await page.fill('#search', '');

    // ------------------------------------------------------------ 6. URL hash state
    await open(page, base, 'data=sample/qa', '17/27.71400/85.31500/roads,pois/road=surface&base=none&grid=9');
    const h = await page.evaluate(() => ({ z: window.QA.map.getZoom(), c: window.QA.map.getCenter(),
      vis: Object.fromEntries(['roads', 'trails', 'buildings', 'pois', 'grid', 'biome'].map((k) => [k, window.QA.app.isVisible(k)])),
      mode: document.getElementById('road-mode').value, grid: window.QA.grid.level, base: window.QA.app.base }));
    check('hash restores zoom/centre', h.z === 17 && Math.abs(h.c.lat - 27.714) < 1e-5 && Math.abs(h.c.lng - 85.315) < 1e-5, h);
    check('hash restores layers + options', h.vis.roads && h.vis.pois && !h.vis.trails && !h.vis.buildings && !h.vis.grid &&
      h.mode === 'surface' && h.grid === 9 && h.base === 'none', h);
    await page.selectOption('#road-mode', 'class');
    await page.evaluate(() => { window.QA.map.setView([27.716, 85.312], 15, { animate: false }); });
    await settle(page);
    const written = await page.evaluate(() => location.hash);
    check('hash updates on move/mode change', /^#15\/27\.71600\/85\.31200\/roads,pois\/.*road=class/.test(written), written);
    await page.evaluate(() => { location.hash = '#14/27.7150/85.3100/roads,buildings,grid/road=sac&base=none'; });
    await page.waitForTimeout(300);
    await settle(page);
    const hc = await page.evaluate(() => ({ z: window.QA.map.getZoom(), mode: window.QA.app.roadMode,
      b: window.QA.app.isVisible('buildings'), p: window.QA.app.isVisible('pois') }));
    check('hashchange applies a pasted view', hc.z === 14 && hc.mode === 'sac' && hc.b && !hc.p, hc);

    // ------------------------------------------------------------ 7. lazy buildings
    const page2 = await context.newPage();
    const log2 = watch(page2);
    await page2.goto(`${base}tools/qa-viewer/?data=sample/qa&_nav=${++navCounter}#14/27.71550/85.31300/roads,buildings,pois/base=none`);
    await page2.waitForFunction(() => window.QA && window.QA.app, null, { timeout: 20000 });
    const atReady = await page2.evaluate(async () => {
      await window.QA.ready;
      return Object.fromEntries(Object.entries(window.QA.layers).map(([k, l]) => [k, l.state]));
    });
    check('QA.ready resolves once the non-lazy layers are loaded', atReady.roads === 'ready' && atReady.trails === 'ready' &&
      atReady.pois === 'ready' && atReady.areas === 'ready' && atReady.buildings === 'idle', atReady);
    await settle(page2);
    const lazy = await page2.evaluate(() => ({ state: window.QA.layers.buildings.state, msg: window.QA.layers.buildings.message }));
    check('buildings not fetched below zoom 15', lazy.state === 'idle' && !log2.requests.some((u) => u.endsWith('/buildings.geojson')), lazy);
    await page2.evaluate(() => { window.QA.map.setZoom(16, { animate: false }); });
    await settle(page2);
    const lazy2 = await page2.evaluate(() => ({ state: window.QA.layers.buildings.state, drawn: window.QA.layers.buildings.rendered.size }));
    check('buildings load and draw at zoom >= 15', lazy2.state === 'ready' && lazy2.drawn > 0 && log2.requests.some((u) => u.endsWith('/buildings.geojson')), lazy2);
    check('no console errors (lazy page)', realErrors(log2).length === 0, realErrors(log2));
    await page2.close();

    // ------------------------------------------------------------ 8. per-tile building files
    const page3 = await context.newPage();
    const log3 = watch(page3);
    await open(page3, base, 'data=sample/qa&index=index.tiled.json', '15/27.71570/85.31270/buildings/base=none');
    await settle(page3);
    const tiled = await page3.evaluate(() => ({ count: window.QA.layers.buildings.count, want: window.QA.index.layers.buildings.count,
      drawn: window.QA.layers.buildings.rendered.size, loaded: [...window.QA.layers.buildings.tilesLoaded] }));
    check('per-tile buildings: both tile files loaded, counts match', tiled.count === tiled.want && tiled.loaded.length === 2 &&
      log3.requests.some((u) => /buildings\/10\/516_162\.geojson$/.test(u)) && !log3.requests.some((u) => u.endsWith('/buildings.geojson')), tiled);
    check('no console errors (tiled page)', realErrors(log3).length === 0, realErrors(log3));
    await page3.close();

    // ------------------------------------------------------------ 8b. minimal index: the viewer's fallbacks
    const page5 = await context.newPage();
    const log5 = watch(page5);
    await open(page5, base, 'data=sample/qa&index=index.minimal.json', '16/27.71550/85.31300/roads,trails,buildings,pois,hillshade,biome,grid/base=none');
    const mini = await page5.evaluate(() => ({ roads: window.QA.layers.roads.count, trails: window.QA.layers.trails.count,
      trailsState: window.QA.layers.trails.state, trailsDrawn: window.QA.layers.trails.rendered.size, bstate: window.QA.layers.buildings.state,
      pois: window.QA.layers.pois.count, hs: window.QA.rasters.hillshade.countInDom(), bio: window.QA.rasters.biome.countInDom(), grid: window.QA.grid.rendered.size,
      legend: document.getElementById('legend').innerText, corner: window.QA.app.indexTiles[0].corners[3],
      listText: document.getElementById('layer-list').innerText, errors: window.QA.errors }));
    check('minimal index: trails split out of a mixed roads file', mini.roads === 33 && mini.trails === 14 &&
      mini.trailsState === 'ready' && mini.trailsDrawn > 0, { roads: mini.roads, trails: mini.trails });
    check('minimal index: unlisted layers shown as "not in index" and never fetched', mini.bstate === 'absent' &&
      /not in index/.test(mini.listText) && !log5.requests.some((u) => /\/(buildings|areas|lines|trails)\.geojson$/.test(u)));
    check('minimal index: default PNG paths + tile corners from the projection', mini.hs === 2 && mini.bio === 2 &&
      mini.grid === 2 && Math.abs(mini.corner[0] - 85.3023336) < 2e-7 && Math.abs(mini.corner[1] - 27.7204266) < 2e-7, mini.corner);
    check('minimal index: missing biome palette is explained in the legend', /no biome palette/.test(mini.legend));
    check('no console errors (minimal index)', realErrors(log5).length === 0 && mini.errors.length === 0, realErrors(log5));
    await page5.close();

    // ------------------------------------------------------------ 9. tolerant parsing (unit checks in the page)
    const tol = await page.evaluate(() => {
      const U = window.QA.util;
      const r = {};
      r.enumInt = U.normEnum('Surface', 1) === 'ASPHALT' && U.normEnum('Surface', '3') === 'BRICK';
      r.enumLower = U.normEnum('Surface', 'asphalt') === 'ASPHALT' && U.normEnum('RoadClass', 'living_street') === 'LIVING_STREET'
        && U.normEnum('RoadClass', 'Living-Street') === 'LIVING_STREET';
      r.enumAlias = U.normEnum('Surface', 'paving_stones') === 'BRICK' && U.normEnum('SacScale', 'T3') === 'DEMANDING_MOUNTAIN_HIKING'
        && U.normEnum('RoadClass', 'primary_link') === 'PRIMARY';
      r.enumUnknown = U.normEnum('Surface', 99) === '#99' && U.normEnum('Surface', 'weird') === 'WEIRD';
      r.flags = U.flagNames('RoadFlags', 3).join() === 'ONEWAY,BRIDGE' && U.flagNames('RoadFlags', ['oneway']).join() === 'ONEWAY'
        && U.flagNames('RoadFlags', 'ONEWAY|BRIDGE').join() === 'ONEWAY,BRIDGE' && U.flagNames('RoadFlags', 256).join() === '0x100';
      const rd = U.normRoad({ highway: 'track', surface: 7, surfaceSource: 2, bridge: true, sac_scale: 'mountain_hiking' });
      r.road = rd.cls === 'TRACK' && rd.surface === 'DIRT' && rd.source === 'INFERRED' && rd.flags.includes('BRIDGE') && rd.sac === 'MOUNTAIN_HIKING';
      const b = U.normBuilding({ archetype: 1, levels: '3', levels_source: 'tagged' });
      r.building = b.arche === 'NEWAR' && b.levels === 3 && !b.levelsInferred;
      const p1 = U.normPoi({ kind: 101, importance: 0.5 }); const p2 = U.normPoi({ kind: 'peak' }); const p3 = U.normPoi({ kind: 1009 });
      r.poi = p1.kind === 'STUPA' && p1.group === 'religious' && p1.importance === 128 && p2.group === 'nature' && p3.kind === 'NEIGHBOURHOOD' && p3.group === 'place';
      const nm = U.featureNames({ names: { default: 'काठमाडौं', en: 'Kathmandu' } });
      r.names = nm.def === 'काठमाडौं' && nm.en === 'Kathmandu';
      r.colors = U.parseColor([255, 0, 16]) === '#ff0010' && U.parseColor('ABC') === '#aabbcc' && U.parseColor('rgb(1,2,3)') === '#010203';
      const app = window.QA.app;
      const lyr = app.normLayers(['roads', { name: 'pois', file: 'p.geojson', count: 3 }]);
      const lyr2 = app.normLayers({ buildings: { tiles: true }, roads: 'r.geojson', lines: 12 });
      r.layers = lyr.get('roads').path === 'roads.geojson' && lyr.get('pois').path === 'p.geojson' && lyr.get('pois').count === 3
        && lyr2.get('buildings').tiles === 'buildings/{L}/{tx}_{ty}.geojson' && lyr2.get('roads').path === 'r.geojson' && lyr2.get('lines').count === 12;
      const t1 = app.normTile('10/516/162'); const t2 = app.normTile({ z: 10, x: 516, y: 162, bbox: [85.30, 27.71, 85.31, 27.72] });
      r.tiles = t1.id === '10/516/162' && t2.id === '10/516/162' && Math.abs(t1.corners[3][0] - 85.3023336) < 2e-7;
      const pal = app.normPalette([{ id: 1, color: [0, 0, 255] }, { name: 'snow', colour: '#FFFFFF' }]);
      r.palette = pal.length === 2 && pal[0].name === 'WATER' && pal[0].color === '#0000ff' && pal[1].name === 'SNOW';
      const pal2 = app.normPalette({ 11: '#2f6b3a' });
      r.palette2 = pal2[0].name === 'HILL_FOREST';
      r.osm = JSON.stringify(U.osmRefOf('buildings', { osm_ref: 2 * 123 + 1 })) === '{"type":"relation","id":123}'
        && JSON.stringify(U.osmRefOf('pois', { osm_ref: 4 * 77 })) === '{"type":"node","id":77}'
        && JSON.stringify(U.osmRefOf('roads', { osm_way_id: 5 })) === '{"type":"way","id":5}'
        && U.osmRefOf('pois', { osm_ref: 4 * 12345678901 + 1 }).id === 12345678901;
      const gi = new U.GridIndex(0.01);
      gi.insert(0, 85.0, 27.0, 85.001, 27.001); gi.insert(1, 85.5, 27.5, 85.6, 27.6); gi.insert(2, 84.9, 26.9, 85.7, 27.7);
      r.grid = gi.query(84.99, 26.99, 85.01, 27.01).sort().join() === '0,2' && gi.query(85.55, 27.55, 85.56, 27.56).sort().join() === '1,2';
      r.key = window.QA.proj.tileKey(10, 516, 162) === '2882303761517414424';
      return r;
    });
    for (const [k, v] of Object.entries(tol)) check(`tolerant parsing: ${k}`, v === true, v);

    // ------------------------------------------------------------ 10. missing data is reported, not fatal
    const page4 = await context.newPage();
    const log4 = watch(page4);
    await page4.goto(`${base}tools/qa-viewer/?data=does/not/exist#13/27.7/85.3/roads/base=none`, { waitUntil: 'load' });
    await page4.waitForFunction(() => window.QA && window.QA.app, null, { timeout: 20000 });
    await page4.evaluate(() => window.QA.ready);
    const miss = await page4.evaluate(() => ({ banner: document.getElementById('banner').hidden ? '' : document.getElementById('banner').textContent,
      errors: window.QA.errors.length }));
    check('missing dataset -> banner with a hint', /does\/not\/exist\/index\.json/.test(miss.banner) && /sample\/qa/.test(miss.banner) && miss.errors === 1, miss.banner);
    check('missing dataset: only the expected 404 + viewer error, base=none honoured',
      realErrors(log4).every((e) => /404|Could not load/.test(e)) && !log4.requests.some((u) => /tile\.openstreetmap\.org/.test(u)), log4.errors);
    await page4.close();

    const page6 = await context.newPage();
    await page6.goto(`${base}tools/qa-viewer/?region=nope#13/27.7/85.3/roads/base=none`, { waitUntil: 'load' });
    await page6.waitForFunction(() => window.QA && window.QA.app, null, { timeout: 20000 });
    await page6.evaluate(() => window.QA.ready);
    const reg = await page6.evaluate(() => document.getElementById('banner').textContent);
    check('?region=<id> resolves to pipeline/build/regions/<id>/qa', /\/pipeline\/build\/regions\/nope\/qa\/index\.json/.test(reg), reg.slice(0, 120));
    await page6.close();

    check('no console errors (main page)', realErrors(log).length === 0, realErrors(log));
    check('Leaflet loaded from cdnjs with SRI', await page.evaluate(() => !!window.L && window.L.version === '1.9.4'));

    // ------------------------------------------------------------ 11. screenshots
    const shotPage = await context.newPage();
    const logS = watch(shotPage);
    await open(shotPage, base, 'data=sample/qa', '17/27.71640/85.30640/roads,trails,buildings,areas,lines,pois,places,grid/road=surface&base=none');
    await shot(shotPage, 'sample-surface.png');
    await open(shotPage, base, 'data=sample/qa', '16/27.71600/85.31600/roads,trails,areas,lines,pois,hillshade,biome,grid/road=sac&base=none&hs_op=70&bio_op=60');
    await shotPage.evaluate(() => { document.getElementById('legend').closest('.sb-section').scrollIntoView(); });
    await shot(shotPage, 'sample-biome-sac.png');
    // The headline screenshot: OSM basemap (when reachable), surface_source mode, a building in the inspector.
    const baseParam = OFFLINE ? 'none' : 'osm';
    await open(shotPage, base, 'data=sample/qa', `16/27.71580/85.31120/roads,trails,buildings,areas,lines,pois,places,hillshade,grid/road=source&base=${baseParam}&base_op=85&hs_op=45`);
    await shotPage.waitForTimeout(OFFLINE ? 200 : 2500);
    const tb = await shotPage.evaluate(() => {
      const l = window.QA.layers.buildings;
      for (let i = 0; i < l.count; i++) if (l.features[i].properties.name === 'Hotel (sample)') { window.QA.select('buildings', i); return true; }
      return false;
    });
    await shotPage.evaluate(() => { document.getElementById('legend').closest('.sb-section').scrollIntoView(); });
    await shotPage.waitForTimeout(300);
    await shot(shotPage, 'sample.png');
    const tilesShown = await shotPage.evaluate(() => document.querySelectorAll('.qa-basemap img.leaflet-tile-loaded').length);
    console.log(`INFO  OSM basemap tiles loaded in sample.png: ${tilesShown} (routed ok ${external.osm}, failed ${external.osmFailed}; CDN via Node: ${external.cdn})`);
    check('screenshot page selected a building', tb);
    check('no console errors (screenshot pages)', realErrors(logS).length === 0, realErrors(logS));
    await shotPage.close();
  } catch (err) {
    check('test run completed without exceptions', false, err && err.stack ? err.stack : String(err));
  } finally {
    await browser.close();
    server.proc.kill();
  }
  console.log(`\n${results.length - failures} passed, ${failures} failed`);
  process.exit(failures ? 1 : 0);
})();

#!/usr/bin/env node
/*
 * Performance check for the QA viewer at Kathmandu-valley scale.
 *
 *   python3 tools/qa-viewer/tests/make_perf_dataset.py      # ~270 MB into tools/qa-viewer/perf-data/ (gitignored)
 *   node tools/qa-viewer/tests/perf.cjs
 *
 * Prints timings (load, parse+index, first render, pan/zoom updates, click hit
 * test) and the JS heap. Fails only on hard errors or when a pan update takes
 * more than 2 s. Network notes as in smoke.cjs.
 */
'use strict';

process.env.PLAYWRIGHT_DISABLE_FORCED_CHROMIUM_PROXIED_LOOPBACK = '1';
const path = require('path');
const fs = require('fs');
const { spawn } = require('child_process');
const { chromium } = require('playwright');

const VIEWER = path.resolve(__dirname, '..');
const ROOT = path.resolve(VIEWER, '..', '..');
const DATA = path.join(VIEWER, 'perf-data', 'qa', 'index.json');

(async () => {
  if (!fs.existsSync(DATA)) {
    console.error('perf dataset missing: run python3 tools/qa-viewer/tests/make_perf_dataset.py');
    process.exit(2);
  }
  const proc = spawn('python3', [path.join(VIEWER, 'serve.py'), '--port', '0', '--quiet', '--root', ROOT], { stdio: ['ignore', 'pipe', 'inherit'] });
  const base = await new Promise((resolve) => {
    let out = '';
    proc.stdout.on('data', (d) => { out += d; const m = out.match(/Serving .* at (http:\/\/localhost:\d+\/)/); if (m) resolve(m[1]); });
  });
  const launch = process.env.HTTPS_PROXY ? { proxy: { server: process.env.HTTPS_PROXY, bypass: 'localhost,127.0.0.1' } } : {};
  const browser = await chromium.launch({ ...launch, args: ['--js-flags=--expose-gc'] });
  const context = await browser.newContext({ viewport: { width: 1600, height: 1000 } });
  if (process.env.HTTPS_PROXY) {
    await context.route(/^https:\/\/cdnjs\.cloudflare\.com\//, async (route) => route.fulfill({ response: await route.fetch() }));
  }
  const page = await context.newPage();
  const errors = [];
  page.on('pageerror', (e) => errors.push(e.message));
  page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });

  const t0 = Date.now();
  await page.goto(`${base}tools/qa-viewer/?data=perf-data/qa#16/27.70500/85.31500/roads,trails,buildings,areas,pois,grid/base=none`);
  await page.waitForFunction(() => window.QA && window.QA.app, null, { timeout: 60000 });
  await page.evaluate(() => window.QA.ready);
  const tReady = Date.now() - t0;
  await page.waitForFunction(() => window.QA.layers.buildings.state === 'ready' && window.QA.layers.buildings.rendered.size > 0, null, { timeout: 180000 });
  await page.evaluate(() => window.QA.idle());
  const tBuildings = Date.now() - t0;

  const r = await page.evaluate(async () => {
    const QA = window.QA;
    const raf = () => new Promise((res) => requestAnimationFrame(() => requestAnimationFrame(res)));
    const out = { layers: {} };
    for (const [k, l] of Object.entries(QA.layers)) {
      out.layers[k] = { n: l.count, drawn: l.rendered.size, MB: +(l.bytes / 1e6).toFixed(1), loadS: +(l.loadMs / 1000).toFixed(2), cell: l.index && +l.index.cell.toFixed(4) };
    }
    const time = async (fn) => { const a = performance.now(); fn(); const b = performance.now(); await raf(); return { js: +(b - a).toFixed(1), frame: +(performance.now() - a).toFixed(1) }; };
    out.pans = [];
    const c0 = QA.map.getCenter();
    for (let k = 0; k < 8; k++) {
      const dx = 0.004 * Math.cos(k), dy = 0.003 * Math.sin(k);
      out.pans.push(await time(() => { QA.map.setView([c0.lat + dy, c0.lng + dx], 16, { animate: false }); QA.app.updateAll(); }));
    }
    out.z15 = await time(() => { QA.map.setView(c0, 15, { animate: false }); QA.app.updateAll(); });
    out.z15drawn = QA.layers.buildings.rendered.size;
    out.z15msg = QA.layers.buildings.message;
    out.z17 = await time(() => { QA.map.setView(c0, 17, { animate: false }); QA.app.updateAll(); });
    out.restyle = await time(() => QA.setBuildingMode('levels'));
    QA.map.setView(c0, 16, { animate: false }); QA.app.updateAll(); await raf();
    const a = performance.now();
    const b = QA.layers.buildings;
    const someIdx = b.rendered.keys().next().value;
    const ring = b.features[someIdx].geometry.coordinates[0];
    QA.app.onMapClick({ latlng: L.latLng((ring[0][1] + ring[2][1]) / 2, (ring[0][0] + ring[2][0]) / 2) });
    out.clickMs = +(performance.now() - a).toFixed(1);
    out.clickTitle = document.getElementById('inspector-title').textContent;
    const s = performance.now(); const res = QA.search('marg 123'); out.searchMs = +(performance.now() - s).toFixed(1); out.searchN = res.length;
    out.z12 = await time(() => { QA.map.setView(c0, 12, { animate: false }); QA.app.updateAll(); });
    out.z12roads = QA.layers.roads.rendered.size;
    out.z12grid = QA.grid.rendered.size;
    if (window.gc) window.gc();
    out.heapMB = performance.memory ? +(performance.memory.usedJSHeapSize / 1e6).toFixed(0) : null;
    return out;
  });
  console.log(JSON.stringify({ tReadyS: tReady / 1000, tBuildingsRenderedS: tBuildings / 1000, ...r }, null, 1));
  const worstPan = Math.max(...r.pans.map((p) => p.frame));
  const ok = errors.length === 0 && worstPan < 2000 && r.layers.buildings.n === 500000;
  console.log(ok ? 'PERF OK' : 'PERF FAIL', { worstPanMs: worstPan, errors });
  await browser.close();
  proc.kill();
  process.exit(ok ? 0 : 1);
})();

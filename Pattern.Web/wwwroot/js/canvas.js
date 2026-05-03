// canvas.js — Professional Pattern Canvas Editor

(async function initCanvasEditor() {

  // ── 1. FETCH PIECE DATA ──────────────────────────────────────────
  const res  = await fetch(window.PIECE_DATA_URL);
  const DEFS = await res.json();

  const style = window.CANVAS_STYLE
    || new URL(window.PIECE_DATA_URL, location.href).searchParams.get('style')
    || 'skinny';

  let pieces = DEFS.map(def => ({
    name:    def.name,
    cut:     def.cut,
    col:     def.col,
    pts:     (def.pts    || []).map(([x, y]) => ({ x, y })),
    grain:   (def.grain  || []).map(a => [...a]),
    cf:      (def.cf     || []).map(a => [...a]),
    notches: (def.notches|| []).map(a => [...a]),
    ox: def.ox, oy: def.oy,
  }));

  // ── 2. STATE ─────────────────────────────────────────────────────
  let selectedPiece = window.CANVAS_INIT_PIECE || 0;
  const patternId   = window.CANVAS_PATTERN_ID || 0;
  if (patternId > 0) {
    try { sessionStorage.setItem('pp_last_canvas_pattern_id', String(patternId)); } catch (_) {}
  }
  let tool          = 'select';
  let scale = 1, panX = 0, panY = 0;
  let showSA = true, showGrain = true, showLabels = true, showNotches = true;
  let drag      = null;
  let isPanning = false, panSX = 0, panSY = 0;
  let lastWorld = { x: 0, y: 0 };
  let history   = [];
  let saveTimer = null;
  const SAVE_DELAY = 1200;

  // draft-mode state
  let draftedSizes = null;   // { 'S': pieces[], 'M': pieces[], ... }
  const DRAFT_SIZE_COLORS = {
    XS: '#dc2626', S: '#d97706', M: '#16a34a',
    L:  '#0284c7', XL: '#7c3aed', XXL: '#0891b2',
  };

  // draw-tool state
  let drawPoints      = [];   // world coords placed so far
  let drawCursor      = null; // live cursor world position
  let pendingNewPiece = null; // { pts, ox, oy } waiting for name input

  // ── 3. CANVAS SETUP ──────────────────────────────────────────────
  const canvas = document.getElementById('cv');
  const ctx    = canvas?.getContext('2d');
  if (!canvas || !ctx) return;

  function resize() {
    const p = canvas.parentElement;
    canvas.width  = p.clientWidth;
    canvas.height = p.clientHeight;
    draw();
  }
  resize();
  new ResizeObserver(resize).observe(canvas.parentElement);

  // ── 4. PIECE LIST ─────────────────────────────────────────────────
  pieces.forEach((p, i) => {
    const dot = document.getElementById(`cpl-dot-${i}`);
    if (dot) dot.style.background = p.col;
    const cut = document.getElementById(`cpl-cut-${i}`);
    if (cut) cut.textContent = p.cut;
  });

  document.getElementById('cpl-list')?.addEventListener('click', e => {
    const item = e.target.closest('[data-index]');
    if (item) selectPiece(parseInt(item.dataset.index));
  });

  function selectPiece(i) {
    if (!pieces.length) { draw(); return; }
    selectedPiece = i;
    document.querySelectorAll('.cpl-item').forEach((el, idx) =>
      el.classList.toggle('active', idx === i));
    updatePropsPanel(i);
    fetchMeasurements(i);
    draw();
  }

  function updatePieceCount() {
    const n   = pieces.length;
    const badge = document.getElementById('cv-piece-count');
    const cbadge = document.getElementById('cpl-count-badge');
    if (badge)  badge.textContent  = n === 0 ? 'Empty' : `${n} piece${n !== 1 ? 's' : ''}`;
    if (cbadge) cbadge.textContent = n;
  }

  function showEmptyState() {
    const el = document.getElementById('cv-empty-state');
    if (el) el.style.display = '';
    const em = document.getElementById('cpl-empty-msg');
    if (em) em.style.display = '';
  }

  function hideEmptyState() {
    const el = document.getElementById('cv-empty-state');
    if (el) el.style.display = 'none';
    const em = document.getElementById('cpl-empty-msg');
    if (em) em.style.display = 'none';
  }

  // ── 5. TOOLBAR ───────────────────────────────────────────────────
  const TOOL_MAP = {
    'ctb-select': 'select',
    'ctb-move':   'move',
    'ctb-point':  'point',
    'ctb-notch':  'notch',
    'ctb-draw':   'draw',
  };
  Object.entries(TOOL_MAP).forEach(([id, t]) => {
    document.getElementById(id)?.addEventListener('click', () => setTool(t));
  });

  document.getElementById('tog-sa-btn')?.addEventListener('click',     () => { showSA      = !showSA;      draw(); });
  document.getElementById('tog-grain-btn')?.addEventListener('click',  () => { showGrain   = !showGrain;   draw(); });
  document.getElementById('tog-labels-btn')?.addEventListener('click', () => { showLabels  = !showLabels;  draw(); });

  ['tog-sa','tog-grain','tog-labels','tog-notch'].forEach(id => {
    document.getElementById(id)?.addEventListener('click', () => {
      const el = document.getElementById(id);
      if (id === 'tog-sa')     { showSA      = !showSA;      el.classList.toggle('on', showSA);      }
      if (id === 'tog-grain')  { showGrain   = !showGrain;   el.classList.toggle('on', showGrain);   }
      if (id === 'tog-labels') { showLabels  = !showLabels;  el.classList.toggle('on', showLabels);  }
      if (id === 'tog-notch')  { showNotches = !showNotches; el.classList.toggle('on', showNotches); }
      draw();
    });
  });

  document.getElementById('btn-zoom-in')?.addEventListener('click',  () => zoom(0.1));
  document.getElementById('btn-zoom-out')?.addEventListener('click', () => zoom(-0.1));
  document.getElementById('btn-undo')?.addEventListener('click',     applyUndo);
  document.getElementById('btn-fit-all')?.addEventListener('click',  fitAll);
  document.getElementById('btn-save-all')?.addEventListener('click', saveAllPieces);
  document.getElementById('btn-clear-canvas')?.addEventListener('click', clearCanvas);

  // ── 6. MOUSE ─────────────────────────────────────────────────────
  canvas.addEventListener('mousedown', e => {
    const [sx, sy] = screenPos(e);
    const [wx, wy] = toWorld(sx, sy);
    lastWorld = { x: wx, y: wy };

    if (e.button === 1 || tool === 'move') {
      isPanning = true; panSX = sx - panX; panSY = sy - panY;
      canvas.style.cursor = 'grabbing'; return;
    }

    if (tool === 'select') {
      const gh = getGrainHandle(sx, sy, selectedPiece);
      if (gh) { pushHistory(selectedPiece); drag = gh; return; }

      const hi = getVertexHandle(sx, sy, selectedPiece);
      if (hi >= 0) { pushHistory(selectedPiece); drag = { type: 'handle', idx: hi }; return; }

      const pi = getPieceAt(sx, sy);
      if (pi >= 0) {
        if (pi !== selectedPiece) { selectPiece(pi); return; }
        pushHistory(selectedPiece);
        const p = pieces[selectedPiece];
        drag = { type: 'piece', startWx: wx, startWy: wy, origOx: p.ox, origOy: p.oy };
      }
    }

    if (tool === 'point')  insertPoint(sx, sy);
    if (tool === 'draw')   handleDrawClick(sx, sy);
    if (tool === 'notch') {
      const ni = getNearestNotch(sx, sy, selectedPiece);
      if (ni >= 0) removeNotch(selectedPiece, ni);
      else         addNotchOnEdge(sx, sy, selectedPiece);
    }
  });

  canvas.addEventListener('mousemove', e => {
    const [sx, sy] = screenPos(e);
    const [wx, wy] = toWorld(sx, sy);

    if (tool === 'draw') { drawCursor = { x: wx, y: wy }; draw(); return; }
    if (isPanning) { panX = sx - panSX; panY = sy - panSY; draw(); return; }
    if (!drag)     { lastWorld = { x: wx, y: wy }; return; }

    const p  = pieces[selectedPiece];
    const lx = wx - p.ox, ly = wy - p.oy;

    if      (drag.type === 'handle')           { p.pts[drag.idx] = { x: lx, y: ly }; }
    else if (drag.type === 'piece')            { p.ox = drag.origOx + (wx - drag.startWx); p.oy = drag.origOy + (wy - drag.startWy); }
    else if (drag.type === 'grain' && p.grain) { p.grain[drag.idx][0] = Math.round(lx); p.grain[drag.idx][1] = Math.round(ly); }
    else if (drag.type === 'cf'    && p.cf)    { p.cf[drag.idx][0]    = Math.round(lx); p.cf[drag.idx][1]    = Math.round(ly); }

    lastWorld = { x: wx, y: wy };
    draw();
  });

  canvas.addEventListener('mouseup', () => {
    const wasDrag = drag !== null;
    drag = null; isPanning = false;
    canvas.style.cursor = tool === 'move' ? 'grab' : tool === 'point' ? 'crosshair' : tool === 'notch' ? 'cell' : 'default';
    if (wasDrag) { updatePropsPanel(selectedPiece); scheduleSave(selectedPiece); }
  });

  canvas.addEventListener('dblclick', e => {
    if (tool === 'draw' && drawPoints.length >= 3) {
      drawPoints.pop(); // first click of dblclick already added a point
      closeDraw();
    }
  });

  canvas.addEventListener('contextmenu', e => {
    e.preventDefault();
    if (tool === 'select') {
      const [sx, sy] = screenPos(e);
      const hi = getVertexHandle(sx, sy, selectedPiece);
      if (hi >= 0) deleteVertex(selectedPiece, hi);
    }
  });

  canvas.addEventListener('wheel', e => {
    e.preventDefault();
    const [sx, sy] = screenPos(e);
    const [wx, wy] = toWorld(sx, sy);
    scale  = clamp(scale + (e.deltaY > 0 ? -0.08 : 0.08), 0.1, 5);
    panX   = sx - wx * scale; panY = sy - wy * scale;
    updateZoomLabel(); draw();
  }, { passive: false });

  // ── 7. KEYBOARD ──────────────────────────────────────────────────
  document.addEventListener('keydown', e => {
    if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;
    if (e.ctrlKey && e.key === 'z') { applyUndo(); return; }
    if (e.ctrlKey && e.key === 's') { e.preventDefault(); saveAllPieces(); return; }
    switch (e.key) {
      case 's': case 'S': setTool('select'); break;
      case 'p': case 'P': setTool('move');   break;
      case 'a': case 'A': setTool('point');  break;
      case 'n': case 'N': setTool('notch');  break;
      case 'd': case 'D': setTool('draw');   break;
      case 'Escape':
        if (tool === 'draw') { drawPoints = []; drawCursor = null; hideCreateForm(); setTool('select'); draw(); }
        break;
      case ' ': e.preventDefault(); fitAll(); break;
      case '+': case '=': zoom(0.1);  break;
      case '-':            zoom(-0.1); break;
      case 'Delete': case 'Backspace': {
        const [sx, sy] = toScreen(lastWorld.x, lastWorld.y);
        const hi = getVertexHandle(sx, sy, selectedPiece);
        if (hi >= 0) deleteVertex(selectedPiece, hi);
        break;
      }
    }
  });

  // ── 8. EDITING ───────────────────────────────────────────────────
  function insertPoint(sx, sy) {
    const p = pieces[selectedPiece];
    const [wx, wy] = toWorld(sx, sy);
    const lx = wx - p.ox, ly = wy - p.oy;
    let bestDist = Infinity, bestIdx = -1, bestT = 0;

    for (let i = 0; i < p.pts.length; i++) {
      const a = p.pts[i], b = p.pts[(i + 1) % p.pts.length];
      const [t, d] = closestOnSeg(lx, ly, a.x, a.y, b.x, b.y);
      if (d < bestDist) { bestDist = d; bestIdx = i; bestT = t; }
    }

    if (bestIdx < 0 || bestDist > 25 / scale) return;
    pushHistory(selectedPiece);
    const a = p.pts[bestIdx], b = p.pts[(bestIdx + 1) % p.pts.length];
    p.pts.splice(bestIdx + 1, 0, { x: a.x + (b.x - a.x) * bestT, y: a.y + (b.y - a.y) * bestT });
    draw(); updatePropsPanel(selectedPiece); scheduleSave(selectedPiece);
  }

  function deleteVertex(pieceIdx, vertIdx) {
    if (pieces[pieceIdx].pts.length <= 3) { showSaveStatus('Need ≥ 3 points', 'warn'); return; }
    pushHistory(pieceIdx);
    pieces[pieceIdx].pts.splice(vertIdx, 1);
    draw(); updatePropsPanel(pieceIdx); scheduleSave(pieceIdx);
  }

  function addNotchOnEdge(sx, sy, pieceIdx) {
    const p = pieces[pieceIdx];
    const [wx, wy] = toWorld(sx, sy);
    const lx = wx - p.ox, ly = wy - p.oy;
    let bestDist = Infinity, bestT = 0, bestA = null, bestB = null;

    for (let i = 0; i < p.pts.length; i++) {
      const a = p.pts[i], b = p.pts[(i + 1) % p.pts.length];
      const [t, d] = closestOnSeg(lx, ly, a.x, a.y, b.x, b.y);
      if (d < bestDist) { bestDist = d; bestT = t; bestA = a; bestB = b; }
    }

    if (bestDist > 30 / scale) return;
    pushHistory(pieceIdx);
    p.notches = p.notches || [];
    p.notches.push([Math.round(bestA.x + (bestB.x - bestA.x) * bestT), Math.round(bestA.y + (bestB.y - bestA.y) * bestT)]);
    draw(); scheduleSave(pieceIdx);
  }

  function removeNotch(pieceIdx, ni) {
    pushHistory(pieceIdx);
    pieces[pieceIdx].notches.splice(ni, 1);
    draw(); scheduleSave(pieceIdx);
  }

  // ── 9. UNDO ──────────────────────────────────────────────────────
  function pushHistory(idx) {
    const p = pieces[idx];
    history.push({
      idx,
      pts:     p.pts.map(pt => ({ ...pt })),
      ox: p.ox, oy: p.oy,
      grain:   p.grain   ? p.grain.map(a => [...a])   : null,
      cf:      p.cf      ? p.cf.map(a => [...a])      : null,
      notches: p.notches ? p.notches.map(a => [...a]) : [],
    });
    if (history.length > 60) history.shift();
  }

  function applyUndo() {
    if (!history.length) return;
    const snap = history.pop();
    const p    = pieces[snap.idx];
    p.pts = snap.pts; p.ox = snap.ox; p.oy = snap.oy;
    p.grain = snap.grain; p.cf = snap.cf; p.notches = snap.notches;
    selectedPiece = snap.idx;
    selectPiece(snap.idx);
    showSaveStatus('Undone', 'info');
  }

  function clearCanvas() {
    if (pieces.length > 0 && !confirm('Clear all pieces from the canvas?')) return;
    pieces.length = 0;
    history.length = 0;
    drawPoints = []; drawCursor = null;
    hideCreateForm();
    draftedSizes = null;
    const list = document.getElementById('cpl-list');
    if (list) list.innerHTML = '';
    ['cp-piece','cp-pts','cp-w','cp-h','cp-perim'].forEach(id => {
      const el = document.getElementById(id); if (el) el.textContent = '—';
    });
    const eEl = document.getElementById('cp-edges'); if (eEl) eEl.innerHTML = '—';
    setTool('select');
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    updatePieceCount();
    showEmptyState();
    showSaveStatus('Canvas cleared', 'info');
  }

  // ── 10. SAVE ─────────────────────────────────────────────────────
  function scheduleSave(idx) {
    clearTimeout(saveTimer);
    showSaveStatus('Unsaved…', 'pending');
    saveTimer = setTimeout(() => savePiece(idx), SAVE_DELAY);
  }

  async function savePiece(idx) {
    const p = pieces[idx];
    showSaveStatus('Saving…', 'pending');
    try {
      const r = await fetch(window.SAVE_URL, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          patternId,
          style:   style,
          name:    p.name,
          pts:     p.pts.map(pt => [Math.round(pt.x), Math.round(pt.y)]),
          ox:      Math.round(p.ox), oy: Math.round(p.oy),
          grain:   p.grain, cf: p.cf, notches: p.notches,
        }),
      });
      showSaveStatus(r.ok ? 'Saved ✓' : 'Save failed', r.ok ? 'ok' : 'error');
    } catch { showSaveStatus('Save failed', 'error'); }
  }

  async function saveAllPieces() {
    showSaveStatus('Saving all…', 'pending');
    try {
      const r = await fetch(window.SAVE_ALL_URL, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          patternId,
          style:  style,
          pieces: pieces.map(p => ({
            name:    p.name,
            pts:     p.pts.map(pt => [Math.round(pt.x), Math.round(pt.y)]),
            ox:      Math.round(p.ox), oy: Math.round(p.oy),
            grain:   p.grain, cf: p.cf, notches: p.notches,
          })),
        }),
      });
      const data = await r.json();
      showSaveStatus(r.ok ? `Saved ${data.saved} pieces ✓` : 'Save failed', r.ok ? 'ok' : 'error');
    } catch { showSaveStatus('Save failed', 'error'); }
  }

  function showSaveStatus(msg, state) {
    const el = document.getElementById('save-indicator');
    if (!el) return;
    el.textContent = msg;
    el.className   = `save-indicator si-${state}`;
    if (state === 'ok') setTimeout(() => { if (el.textContent === msg) el.textContent = ''; }, 2500);
  }

  // ── 11. MEASUREMENTS ─────────────────────────────────────────────
  async function fetchMeasurements(idx) {
    try {
      const p   = pieces[idx];
      if (!p) return;
      const url = `${window.MEASUREMENTS_URL}?patternId=${encodeURIComponent(patternId)}&style=${encodeURIComponent(style)}&piece=${encodeURIComponent(p.name)}`;
      const r   = await fetch(url);
      if (!r.ok) return;
      const m = await r.json();
      const set = (id, v) => { const el = document.getElementById(id); if (el) el.textContent = v; };
      set('cp-w',     m.bboxW + ' px');
      set('cp-h',     m.bboxH + ' px');
      set('cp-perim', m.perimeter + ' px');
      const eEl = document.getElementById('cp-edges');
      if (eEl) eEl.innerHTML = m.edgeLengths.map((l, i) => `<div>E${i + 1}: <b>${l}</b> px</div>`).join('');
    } catch { /* silent */ }
  }

  function updatePropsPanel(idx) {
    const p = pieces[idx];
    const set = (id, v) => { const el = document.getElementById(id); if (el) el.textContent = v; };
    if (!p) { set('cp-piece', '—'); set('cp-pts', '—'); return; }
    set('cp-piece', p.name);
    set('cp-pts',   p.pts.length);
  }

  // ── 12. DRAW ─────────────────────────────────────────────────────
  function draw() {
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    if (draftedSizes) drawAllDraftSizes();
    pieces.forEach((p, i) => drawPiece(p, i === selectedPiece));
    if (tool === 'draw') drawInProgress();
  }

  function drawInProgress() {
    if (drawPoints.length === 0 && !pendingNewPiece) return;
    ctx.save();
    ctx.strokeStyle = '#1a1a6e'; ctx.lineWidth = 2; ctx.setLineDash([6, 3]);
    ctx.beginPath();

    const all = [...drawPoints];
    if (all.length === 0) { ctx.restore(); return; }

    const [fx, fy] = toScreen(all[0].x, all[0].y);
    ctx.moveTo(fx, fy);
    all.slice(1).forEach(p => { const [x, y] = toScreen(p.x, p.y); ctx.lineTo(x, y); });

    // ghost line to cursor
    if (drawCursor) { const [cx, cy] = toScreen(drawCursor.x, drawCursor.y); ctx.lineTo(cx, cy); }
    ctx.stroke(); ctx.setLineDash([]);

    // vertex dots
    all.forEach((p, i) => {
      const [px, py] = toScreen(p.x, p.y);
      ctx.beginPath(); ctx.arc(px, py, i === 0 ? 7 : 4, 0, Math.PI * 2);
      ctx.fillStyle = i === 0 ? '#b91c1c' : '#1a1a6e'; ctx.fill();
      if (i === 0) {
        ctx.strokeStyle = '#fff'; ctx.lineWidth = 1.5; ctx.stroke();
        // label ①
        ctx.font = 'bold 10px monospace'; ctx.fillStyle = '#fff'; ctx.textAlign = 'center';
        ctx.fillText('①', px, py + 3.5);
      }
    });
    ctx.restore();
  }

  function drawPiece(p, sel) {
    const { ox, oy, pts, grain, cf, notches, col } = p;
    if (!pts.length) return;
    const ts = (x, y) => toScreen(x + ox, y + oy);
    ctx.save();

    // Seam allowance ghost
    if (showSA) {
      ctx.beginPath();
      const [sx0, sy0] = ts(pts[0].x, pts[0].y); ctx.moveTo(sx0, sy0);
      pts.forEach(pt => { const [x, y] = ts(pt.x, pt.y); ctx.lineTo(x, y); });
      ctx.closePath();
      ctx.strokeStyle = 'rgba(185,28,28,0.12)'; ctx.lineWidth = 16 * scale;
      ctx.lineJoin = 'miter'; ctx.miterLimit = 1.5; ctx.stroke();
      ctx.strokeStyle = 'rgba(248,247,245,0.88)'; ctx.lineWidth = 16 * scale - 2; ctx.stroke();
    }

    // Shape
    ctx.beginPath();
    const [sx0, sy0] = ts(pts[0].x, pts[0].y); ctx.moveTo(sx0, sy0);
    pts.forEach(pt => { const [x, y] = ts(pt.x, pt.y); ctx.lineTo(x, y); });
    ctx.closePath();
    ctx.fillStyle   = sel ? 'rgba(23,23,23,0.07)' : 'rgba(23,23,23,0.03)'; ctx.fill();
    ctx.strokeStyle = sel ? col : 'rgba(23,23,23,0.28)'; ctx.lineWidth = sel ? 2 : 1;
    ctx.setLineDash([]); ctx.stroke();

    // CF line
    if (cf?.length && showGrain) {
      const [x1, y1] = ts(cf[0][0], cf[0][1]);
      const [x2, y2] = ts(cf[cf.length - 1][0], cf[cf.length - 1][1]);
      ctx.beginPath(); ctx.moveTo(x1, y1); ctx.lineTo(x2, y2);
      ctx.strokeStyle = '#b91c1c'; ctx.lineWidth = 1; ctx.setLineDash([5, 3]); ctx.stroke(); ctx.setLineDash([]);
      if (sel) [[x1, y1], [x2, y2]].forEach(([hx, hy]) => {
        ctx.fillStyle = '#b91c1c'; ctx.fillRect(hx - 4, hy - 4, 8, 8);
      });
    }

    // Grain line
    if (grain?.length && showGrain) {
      const [gx1, gy1] = ts(grain[0][0], grain[0][1]);
      const [gx2, gy2] = ts(grain[1][0], grain[1][1]);
      ctx.beginPath(); ctx.moveTo(gx1, gy1); ctx.lineTo(gx2, gy2);
      ctx.strokeStyle = col; ctx.lineWidth = 1.5; ctx.globalAlpha = 0.5; ctx.stroke(); ctx.globalAlpha = 1;
      drawArrow(gx1, gy1, gx2, gy2, col);
      if (sel) [[gx1, gy1], [gx2, gy2]].forEach(([hx, hy]) => {
        ctx.beginPath(); ctx.arc(hx, hy, 5, 0, Math.PI * 2);
        ctx.fillStyle = '#fff'; ctx.fill(); ctx.strokeStyle = col; ctx.lineWidth = 2; ctx.stroke();
      });
    }

    // Notches
    if (showNotches && notches?.length) {
      notches.forEach(([nx, ny]) => {
        const [sx, sy] = ts(nx, ny);
        ctx.beginPath();
        ctx.moveTo(sx - 5 * scale, sy); ctx.lineTo(sx + 5 * scale, sy);
        ctx.moveTo(sx, sy - 5 * scale); ctx.lineTo(sx, sy + 5 * scale);
        ctx.strokeStyle = '#991b1b'; ctx.lineWidth = 1.5; ctx.setLineDash([]); ctx.stroke();
      });
    }

    // Labels
    if (showLabels && scale > 0.3) {
      const cx = pts.reduce((a, pt) => a + pt.x, 0) / pts.length + ox;
      const cy = pts.reduce((a, pt) => a + pt.y, 0) / pts.length + oy;
      const [lx, ly] = toScreen(cx, cy);
      ctx.font = `600 ${Math.max(8, 10 * scale)}px 'IBM Plex Mono',monospace`;
      ctx.fillStyle = '#334155'; ctx.textAlign = 'center'; ctx.fillText(p.name, lx, ly);
      ctx.font = `${Math.max(7, 8 * scale)}px 'IBM Plex Mono',monospace`;
      ctx.fillStyle = '#64748b'; ctx.fillText(p.cut, lx, ly + 12 * scale);
    }

    // Vertex handles
    if (sel) {
      pts.forEach((pt, i) => {
        const [hx, hy] = ts(pt.x, pt.y);
        ctx.beginPath(); ctx.arc(hx, hy, 5.5, 0, Math.PI * 2);
        ctx.fillStyle = '#fff'; ctx.fill(); ctx.strokeStyle = col; ctx.lineWidth = 2; ctx.stroke();
        if (scale > 1.5) {
          ctx.font = '9px monospace'; ctx.fillStyle = '#64748b'; ctx.textAlign = 'center';
          ctx.fillText(i, hx, hy - 8);
        }
      });
    }

    ctx.restore();
  }

  function drawArrow(x1, y1, x2, y2, col) {
    const len = Math.hypot(x2 - x1, y2 - y1);
    if (len < 10) return;
    const ang = Math.atan2(y2 - y1, x2 - x1), sz = Math.min(8, len * 0.2) * scale;
    ctx.save(); ctx.translate(x2, y2); ctx.rotate(ang);
    ctx.beginPath(); ctx.moveTo(0, 0); ctx.lineTo(-sz, -sz / 2); ctx.lineTo(-sz, sz / 2); ctx.closePath();
    ctx.fillStyle = col; ctx.globalAlpha = 0.5; ctx.fill(); ctx.globalAlpha = 1;
    ctx.restore();
  }

  // ── 13. HIT TESTING ──────────────────────────────────────────────
  function getVertexHandle(sx, sy, idx) {
    const p = pieces[idx];
    for (let i = 0; i < p.pts.length; i++) {
      const [hx, hy] = toScreen(p.pts[i].x + p.ox, p.pts[i].y + p.oy);
      if (Math.abs(sx - hx) < 10 && Math.abs(sy - hy) < 10) return i;
    }
    return -1;
  }

  function getGrainHandle(sx, sy, idx) {
    const p = pieces[idx], HIT = 10;
    if (p.grain?.length && showGrain) {
      for (let i = 0; i < p.grain.length; i++) {
        const [hx, hy] = toScreen(p.grain[i][0] + p.ox, p.grain[i][1] + p.oy);
        if (Math.abs(sx - hx) < HIT && Math.abs(sy - hy) < HIT) return { type: 'grain', idx: i };
      }
    }
    if (p.cf?.length && showGrain) {
      for (let i = 0; i < p.cf.length; i++) {
        const [hx, hy] = toScreen(p.cf[i][0] + p.ox, p.cf[i][1] + p.oy);
        if (Math.abs(sx - hx) < HIT && Math.abs(sy - hy) < HIT) return { type: 'cf', idx: i };
      }
    }
    return null;
  }

  function getPieceAt(sx, sy) {
    const [wx, wy] = toWorld(sx, sy);
    for (let i = pieces.length - 1; i >= 0; i--) {
      const p = pieces[i];
      if (pointInPoly(wx, wy, p.pts.map(pt => ({ x: pt.x + p.ox, y: pt.y + p.oy })))) return i;
    }
    return -1;
  }

  function getNearestNotch(sx, sy, idx) {
    const p = pieces[idx];
    if (!p.notches?.length) return -1;
    for (let i = 0; i < p.notches.length; i++) {
      const [nx, ny] = toScreen(p.notches[i][0] + p.ox, p.notches[i][1] + p.oy);
      if (Math.abs(sx - nx) < 12 && Math.abs(sy - ny) < 12) return i;
    }
    return -1;
  }

  function pointInPoly(px, py, pts) {
    let inside = false;
    for (let i = 0, j = pts.length - 1; i < pts.length; j = i++) {
      const xi = pts[i].x, yi = pts[i].y, xj = pts[j].x, yj = pts[j].y;
      if ((yi > py) !== (yj > py) && px < ((xj - xi) * (py - yi)) / (yj - yi) + xi) inside = !inside;
    }
    return inside;
  }

  function closestOnSeg(px, py, ax, ay, bx, by) {
    const dx = bx - ax, dy = by - ay, len2 = dx * dx + dy * dy;
    if (len2 === 0) return [0, Math.hypot(px - ax, py - ay)];
    const t  = Math.max(0, Math.min(1, ((px - ax) * dx + (py - ay) * dy) / len2));
    return [t, Math.hypot(px - (ax + t * dx), py - (ay + t * dy))];
  }

  // ── 14. VIEWPORT ─────────────────────────────────────────────────
  function fitAll() {
    const PAD = 60, W = canvas.width, H = canvas.height;
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    const allForFit = [...pieces];
    if (draftedSizes) Object.values(draftedSizes).forEach(ps => allForFit.push(...ps));
    allForFit.forEach(p => p.pts.forEach(pt => {
      const wx = pt.x + p.ox, wy = pt.y + p.oy;
      if (wx < minX) minX = wx; if (wy < minY) minY = wy;
      if (wx > maxX) maxX = wx; if (wy > maxY) maxY = wy;
    }));
    if (!isFinite(minX)) return;
    const bw = maxX - minX || 1, bh = maxY - minY || 1;
    scale = Math.min((W - PAD * 2) / bw, (H - PAD * 2) / bh, 1.5);
    panX  = (W - bw * scale) / 2 - minX * scale;
    panY  = (H - bh * scale) / 2 - minY * scale;
    updateZoomLabel(); draw();
  }

  function zoom(delta) {
    scale = clamp(scale + delta, 0.1, 5);
    updateZoomLabel(); draw();
  }

  function toScreen(wx, wy) { return [wx * scale + panX, wy * scale + panY]; }
  function toWorld(sx, sy)  { return [(sx - panX) / scale, (sy - panY) / scale]; }
  function screenPos(e)     { const r = canvas.getBoundingClientRect(); return [e.clientX - r.left, e.clientY - r.top]; }
  function clamp(v, lo, hi) { return Math.max(lo, Math.min(hi, v)); }
  function updateZoomLabel() { const el = document.getElementById('cv-zoom-label'); if (el) el.textContent = Math.round(scale * 100) + '%'; }

  function setTool(t) {
    tool = t;
    const MAP = { select: 'ctb-select', move: 'ctb-move', point: 'ctb-point', notch: 'ctb-notch' };
    document.querySelectorAll('.ctb-btn').forEach(b => b.classList.remove('active'));
    document.getElementById(MAP[t])?.classList.add('active');
    canvas.style.cursor = t === 'move' ? 'grab' : t === 'point' ? 'crosshair' : t === 'notch' ? 'cell' : 'default';
  }

  // ── 15. DRAW TOOL ─────────────────────────────────────────────────
  function handleDrawClick(sx, sy) {
    const [wx, wy] = toWorld(sx, sy);

    // Close shape if clicking near first point
    if (drawPoints.length >= 3) {
      const [fx, fy] = toScreen(drawPoints[0].x, drawPoints[0].y);
      if (Math.abs(sx - fx) < 14 && Math.abs(sy - fy) < 14) { closeDraw(); return; }
    }

    drawPoints.push({ x: wx, y: wy });
    updateDrawHint();
    draw();
  }

  function closeDraw() {
    if (drawPoints.length < 3) return;

    const minX = Math.min(...drawPoints.map(p => p.x));
    const minY = Math.min(...drawPoints.map(p => p.y));

    pendingNewPiece = {
      pts: drawPoints.map(p => [Math.round(p.x - minX), Math.round(p.y - minY)]),
      ox:  Math.round(minX),
      oy:  Math.round(minY),
    };
    drawPoints  = [];
    drawCursor  = null;

    showCreateForm();
    draw();
  }

  function updateDrawHint() {
    const el = document.querySelector('#create-piece-form .draw-hint');
    if (el) el.innerHTML = drawPoints.length < 3
      ? `${drawPoints.length} point${drawPoints.length !== 1 ? 's' : ''} placed — need at least 3.`
      : `${drawPoints.length} points — double-click or click ① to close.`;
  }

  function showCreateForm() {
    const f = document.getElementById('create-piece-form');
    if (f) { f.style.display = 'block'; document.getElementById('new-piece-name')?.focus(); }
    updateDrawHint();
  }

  function hideCreateForm() {
    const f = document.getElementById('create-piece-form');
    if (f) f.style.display = 'none';
    pendingNewPiece = null;
  }

  document.getElementById('btn-confirm-piece')?.addEventListener('click', async () => {
    if (!pendingNewPiece) return;
    const name = document.getElementById('new-piece-name')?.value.trim();
    if (!name) { document.getElementById('new-piece-name')?.focus(); return; }

    const category = document.getElementById('new-piece-category')?.value || 'Body Panels';
    const cut      = document.getElementById('new-piece-cut')?.value      || 'Cut 2';
    const color    = document.getElementById('new-piece-color')?.value    || '#a78bfa';

    showSaveStatus('Creating…', 'pending');
    try {
      const r = await fetch(window.CREATE_URL, {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          patternId,
          style: style, name, category, cut, color,
          pts: pendingNewPiece.pts,
          ox:  pendingNewPiece.ox,
          oy:  pendingNewPiece.oy,
        }),
      });

      if (!r.ok) { const e = await r.json(); showSaveStatus(e.error || 'Failed', 'error'); return; }

      const def = await r.json();
      const newPiece = {
        name:    def.name,    cut:     def.cut,    col:     def.col,
        pts:     (def.pts     || []).map(([x, y]) => ({ x, y })),
        grain:   (def.grain   || []).map(a => [...a]),
        cf:      (def.cf      || []).map(a => [...a]),
        notches: (def.notches || []).map(a => [...a]),
        ox: def.ox, oy: def.oy,
      };

      pieces.push(newPiece);
      addPieceToSidebar(pieces.length - 1, newPiece);
      hideCreateForm();
      setTool('select');
      selectPiece(pieces.length - 1);
      showSaveStatus('Piece created ✓', 'ok');
    } catch { showSaveStatus('Create failed', 'error'); }
  });

  document.getElementById('btn-cancel-piece')?.addEventListener('click', () => {
    drawPoints = []; drawCursor = null;
    hideCreateForm(); setTool('select'); draw();
  });

  function addPieceToSidebar(idx, p) {
    const list = document.getElementById('cpl-list');
    if (!list) return;
    const el = document.createElement('div');
    el.className     = 'cpl-item';
    el.dataset.index = idx;
    el.id            = `cpl-item-${idx}`;
    el.innerHTML     = `<span class="cpl-num">${idx + 1}</span>
      <div class="cpl-dot" style="background:${p.col}"></div>
      <div><div class="cpl-name">${p.name}</div><div class="cpl-cut" id="cpl-cut-${idx}">${p.cut}</div></div>`;
    list.appendChild(el);
    hideEmptyState();
    updatePieceCount();
  }

  // ── 16. INIT ─────────────────────────────────────────────────────
  selectPiece(selectedPiece);
  fitAll();

  // ── 17. AUTO-DRAFT SIZES ─────────────────────────────────────────
  document.getElementById('btn-generate-draft')?.addEventListener('click', generateDraft);
  document.getElementById('btn-clear-draft')?.addEventListener('click',    clearDraft);
  document.getElementById('btn-export-draft')?.addEventListener('click',   exportDraftSizes);
  document.getElementById('use-custom-measurements')?.addEventListener('change', e => {
    const panel = document.getElementById('custom-measurements');
    const checks = document.getElementById('draft-size-checks');
    const autoPill = document.getElementById('auto-size-pill');
    if (panel) panel.style.display = e.target.checked ? '' : 'none';
    if (checks) checks.style.display = e.target.checked ? 'none' : 'flex';
    if (!e.target.checked && autoPill) autoPill.style.display = 'none';
  });
  document.getElementById('btn-recommend-size')?.addEventListener('click', recommendSize);
  document.getElementById('btn-save-profile')?.addEventListener('click', saveProfile);
  document.getElementById('btn-load-profile')?.addEventListener('click', loadProfile);
  syncDraftModeUi();
  loadProfiles();

  async function generateDraft() {
    const msg   = document.getElementById('draft-msg');
    const useCustom = document.getElementById('use-custom-measurements')?.checked;
    let sizes = [...document.querySelectorAll('.size-cb:checked')].map(cb => cb.value);

    msg.textContent = 'Generating…';
    try {
      if (useCustom) {
        const autoSize = await getRecommendedSize(msg);
        if (!autoSize) return;
        sizes = [autoSize];
        updateSizeChecks(autoSize);
        showAutoSize(autoSize);
      } else if (!sizes.length) {
        msg.textContent = 'Select at least one size.';
        return;
      }

      const r = useCustom
        ? await generateFromCustomMeasurements(sizes, msg)
        : await generateFromSizeChart(sizes);
      if (!r) return;
      if (!r.ok) { msg.textContent = 'Server error.'; return; }

      const data = await r.json();

      // Find rightmost x of existing pieces to place draft to the right
      let maxExistingX = 0;
      pieces.forEach(p => p.pts.forEach(pt => {
        const wx = pt.x + p.ox;
        if (wx > maxExistingX) maxExistingX = wx;
      }));
      const xShift = maxExistingX + 200;

      draftedSizes = {};
      for (const [size, defs] of Object.entries(data)) {
        const col = DRAFT_SIZE_COLORS[size] ?? '#64748b';
        draftedSizes[size] = defs.map(def => ({
          name:    def.name,
          cut:     def.cut,
          col,
          pts:     (def.pts     || []).map(([x, y]) => ({ x, y })),
          grain:   (def.grain   || []).map(a => [...a]),
          cf:      (def.cf      || []).map(a => [...a]),
          notches: (def.notches || []).map(a => [...a]),
          ox: def.ox + xShift,
          oy: def.oy,
          sizeLabel: size,
        }));
      }

      document.getElementById('btn-clear-draft').style.display = '';
      document.getElementById('btn-export-draft').style.display = '';
      buildApplyButtons(sizes);
      const total = Object.values(draftedSizes).reduce((s, ps) => s + ps.length, 0);
      if (useCustom) {
        msg.textContent = `${total} pieces generated automatically for size ${sizes[0]}.`;
      } else {
        msg.textContent = `${total} pieces across ${sizes.length} size${sizes.length > 1 ? 's' : ''} — ready.`;
      }
      fitAll();
    } catch { msg.textContent = 'Failed to generate.'; }
  }

  async function generateFromSizeChart(sizes) {
    const qs = sizes.map(s => `sizes=${encodeURIComponent(s)}`).join('&');
    const url = `${window.DRAFT_SIZES_URL}?style=${encodeURIComponent(style)}&${qs}`;
    return fetch(url);
  }

  async function generateFromCustomMeasurements(sizes, msg) {
    const baseSize = document.getElementById('custom-base-size')?.value || 'M';
    const payload = {
      style,
      baseSize,
      sizes,
      waist: readMeasurementValue('ms-waist'),
      hip: readMeasurementValue('ms-hip'),
      frontRise: readMeasurementValue('ms-front-rise'),
      backRise: readMeasurementValue('ms-back-rise'),
      thigh: readMeasurementValue('ms-thigh'),
      knee: readMeasurementValue('ms-knee'),
      ankle: readMeasurementValue('ms-ankle'),
      inseam: readMeasurementValue('ms-inseam'),
    };

    if (Object.values(payload).some(v => typeof v === 'number' && v <= 0)) {
      msg.textContent = 'Enter all body measurements in cm.';
      return null;
    }

    return fetch(window.DRAFT_FROM_BODY_URL, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
  }

  function readMeasurementValue(id) {
    const value = parseFloat(document.getElementById(id)?.value || '0');
    return Number.isFinite(value) ? value : 0;
  }

  function clearDraft() {
    draftedSizes = null;
    document.getElementById('btn-clear-draft').style.display        = 'none';
    document.getElementById('btn-export-draft').style.display       = 'none';
    document.getElementById('draft-apply-list').style.display       = 'none';
    document.getElementById('draft-apply-list').innerHTML           = '';
    document.getElementById('draft-msg').textContent                = '';
    const autoPill = document.getElementById('auto-size-pill');
    if (autoPill && !document.getElementById('use-custom-measurements')?.checked) autoPill.style.display = 'none';
    fitAll();
  }

  async function recommendSize() {
    const msg = document.getElementById('recommended-size');
    const size = await getRecommendedSize(msg);
    if (!size) return;
    msg.textContent = `Suggested: ${size}`;
  }

  async function getRecommendedSize(msgEl) {
    const payload = {
      baseSize: document.getElementById('custom-base-size')?.value || 'M',
      waist: readMeasurementValue('ms-waist'),
      hip: readMeasurementValue('ms-hip'),
      frontRise: readMeasurementValue('ms-front-rise'),
      backRise: readMeasurementValue('ms-back-rise'),
      thigh: readMeasurementValue('ms-thigh'),
      knee: readMeasurementValue('ms-knee'),
      ankle: readMeasurementValue('ms-ankle'),
      inseam: readMeasurementValue('ms-inseam'),
    };
    if (Object.values(payload).some(v => typeof v === 'number' && v <= 0)) {
      if (msgEl) msgEl.textContent = 'Fill all measurements first.';
      return null;
    }

    const r = await fetch(window.RECOMMEND_SIZE_URL, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    if (!r.ok) {
      if (msgEl) msgEl.textContent = 'Could not suggest size.';
      return null;
    }

    const data = await r.json();
    const size = data?.size || null;
    const baseSize = document.getElementById('custom-base-size');
    if (baseSize && size) baseSize.value = size;
    return size;
  }

  function updateSizeChecks(targetSize) {
    document.querySelectorAll('.size-cb').forEach(cb => {
      cb.checked = cb.value === targetSize;
    });
  }

  function showAutoSize(size) {
    const pill = document.getElementById('auto-size-pill');
    const val = document.getElementById('auto-size-value');
    if (!pill || !val) return;
    val.textContent = size || '—';
    pill.style.display = '';
  }

  function syncDraftModeUi() {
    const customOn = !!document.getElementById('use-custom-measurements')?.checked;
    const panel = document.getElementById('custom-measurements');
    const checks = document.getElementById('draft-size-checks');
    const autoPill = document.getElementById('auto-size-pill');
    if (panel) panel.style.display = customOn ? '' : 'none';
    if (checks) checks.style.display = customOn ? 'none' : 'flex';
    if (!customOn && autoPill) autoPill.style.display = 'none';
  }

  async function saveProfile() {
    const name = document.getElementById('profile-name')?.value?.trim();
    const draftMsg = document.getElementById('draft-msg');
    if (!name) { draftMsg.textContent = 'Enter profile name.'; return; }
    const payload = {
      name,
      waist: readMeasurementValue('ms-waist'),
      hip: readMeasurementValue('ms-hip'),
      frontRise: readMeasurementValue('ms-front-rise'),
      backRise: readMeasurementValue('ms-back-rise'),
      thigh: readMeasurementValue('ms-thigh'),
      knee: readMeasurementValue('ms-knee'),
      ankle: readMeasurementValue('ms-ankle'),
      inseam: readMeasurementValue('ms-inseam'),
    };
    if (Object.values(payload).some(v => typeof v === 'number' && v <= 0)) {
      draftMsg.textContent = 'Enter all measurements before saving profile.';
      return;
    }
    const r = await fetch(window.SAVE_PROFILE_URL, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    if (!r.ok) { draftMsg.textContent = 'Could not save profile.'; return; }
    draftMsg.textContent = `Profile "${name}" saved.`;
    await loadProfiles(name);
  }

  async function loadProfiles(selectName = null) {
    const select = document.getElementById('profile-list');
    if (!select) return;
    const r = await fetch(window.PROFILES_URL);
    if (!r.ok) return;
    const profiles = await r.json();
    select.innerHTML = '<option value="">Load profile...</option>';
    profiles.forEach((p, idx) => {
      const opt = document.createElement('option');
      opt.value = String(idx);
      opt.textContent = p.name;
      opt.dataset.profile = JSON.stringify(p);
      select.appendChild(opt);
    });
    if (selectName) {
      const found = [...select.options].find(o => o.textContent === selectName);
      if (found) select.value = found.value;
    }
  }

  function loadProfile() {
    const select = document.getElementById('profile-list');
    if (!select || !select.value) return;
    const selected = select.options[select.selectedIndex];
    const profile = JSON.parse(selected.dataset.profile || '{}');
    setMeasurementField('ms-waist', profile.waist);
    setMeasurementField('ms-hip', profile.hip);
    setMeasurementField('ms-front-rise', profile.frontRise);
    setMeasurementField('ms-back-rise', profile.backRise);
    setMeasurementField('ms-thigh', profile.thigh);
    setMeasurementField('ms-knee', profile.knee);
    setMeasurementField('ms-ankle', profile.ankle);
    setMeasurementField('ms-inseam', profile.inseam);
    const msg = document.getElementById('draft-msg');
    if (msg) msg.textContent = `Profile "${profile.name}" loaded.`;
  }

  function setMeasurementField(id, value) {
    const el = document.getElementById(id);
    if (el) el.value = value > 0 ? value : '';
  }

  function exportDraftSizes() {
    if (!draftedSizes) return;
    const sizes = Object.keys(draftedSizes);
    if (!sizes.length) return;
    const csv = encodeURIComponent(sizes.join(','));
    location.href = `/Export/Index?patternId=${encodeURIComponent(patternId)}&style=${encodeURIComponent(style)}&sizes=${csv}&source=draft`;
  }

  function buildApplyButtons(sizes) {
    const container = document.getElementById('draft-apply-list');
    container.innerHTML = '<div style="font-size:11px;color:#64748b;margin-bottom:4px">Apply to editor:</div>';
    sizes.forEach(size => {
      const btn         = document.createElement('button');
      btn.className     = 'btn btn-outline btn-sm';
      btn.style.cssText = `margin:2px 2px 2px 0;border-color:${DRAFT_SIZE_COLORS[size] ?? '#64748b'};color:${DRAFT_SIZE_COLORS[size] ?? '#64748b'}`;
      btn.textContent   = `Use ${size}`;
      btn.addEventListener('click', () => applyDraftSize(size));
      container.appendChild(btn);
    });
    container.style.display = '';
  }

  function applyDraftSize(size) {
    if (!draftedSizes?.[size]) return;

    // Replace pieces with the drafted size (normalise ox to start at 30)
    const sizepieces = draftedSizes[size];
    const minOx = Math.min(...sizepieces.map(p => p.ox));
    const shift  = minOx - 30;

    pieces.length = 0;
    sizepieces.forEach(p => pieces.push({
      name:    p.name,
      cut:     p.cut,
      col:     p.col,
      pts:     p.pts.map(pt => ({ ...pt })),
      grain:   p.grain.map(a => [...a]),
      cf:      p.cf.map(a => [...a]),
      notches: p.notches.map(a => [...a]),
      ox:      p.ox - shift,
      oy:      p.oy,
    }));

    // Rebuild sidebar
    const list = document.getElementById('cpl-list');
    if (list) {
      list.innerHTML = '';
      pieces.forEach((p, i) => {
        const el          = document.createElement('div');
        el.className      = 'cpl-item';
        el.dataset.index  = i;
        el.id             = `cpl-item-${i}`;
        el.innerHTML      = `<div class="cpl-dot" style="background:${p.col}"></div>
          <div><div class="cpl-name">${p.name}</div><div class="cpl-cut" id="cpl-cut-${i}">${p.cut}</div></div>`;
        list.appendChild(el);
      });
    }

    clearDraft();
    history.length = 0;
    selectPiece(0);
    saveAllPieces();
    showSaveStatus(`Applied size ${size} ✓`, 'ok');
  }

  function drawAllDraftSizes() {
    for (const [size, sizepieces] of Object.entries(draftedSizes)) {
      if (!sizepieces.length) continue;
      const col = DRAFT_SIZE_COLORS[size] ?? '#64748b';

      // Size banner above first piece
      const fp        = sizepieces[0];
      const [lx, ly]  = toScreen(fp.ox, fp.oy - 18);
      ctx.save();
      ctx.font        = `bold ${Math.max(10, 12 * scale)}px 'IBM Plex Mono',monospace`;
      ctx.fillStyle   = col;
      ctx.globalAlpha = 0.9;
      ctx.textAlign   = 'left';
      ctx.fillText(`SIZE ${size}`, lx, ly);
      ctx.restore();

      sizepieces.forEach(p => drawDraftPiece(p, col));
    }
  }

  function drawDraftPiece(p, col) {
    const { ox, oy, pts, grain } = p;
    if (!pts.length) return;
    const ts = (x, y) => toScreen(x + ox, y + oy);
    ctx.save();

    ctx.beginPath();
    const [sx0, sy0] = ts(pts[0].x, pts[0].y);
    ctx.moveTo(sx0, sy0);
    pts.forEach(pt => { const [x, y] = ts(pt.x, pt.y); ctx.lineTo(x, y); });
    ctx.closePath();
    ctx.fillStyle   = col + '18';
    ctx.fill();
    ctx.strokeStyle = col;
    ctx.lineWidth   = 1.5;
    ctx.globalAlpha = 0.75;
    ctx.stroke();

    if (grain?.length >= 2 && showGrain) {
      const [gx1, gy1] = ts(grain[0][0], grain[0][1]);
      const [gx2, gy2] = ts(grain[1][0], grain[1][1]);
      ctx.beginPath(); ctx.moveTo(gx1, gy1); ctx.lineTo(gx2, gy2);
      ctx.strokeStyle = col; ctx.lineWidth = 1; ctx.globalAlpha = 0.35; ctx.stroke();
    }

    if (showLabels && scale > 0.25) {
      const cx = pts.reduce((a, pt) => a + pt.x, 0) / pts.length + ox;
      const cy = pts.reduce((a, pt) => a + pt.y, 0) / pts.length + oy;
      const [tlx, tly] = toScreen(cx, cy);
      ctx.font      = `600 ${Math.max(7, 9 * scale)}px 'IBM Plex Mono',monospace`;
      ctx.fillStyle = col;
      ctx.globalAlpha = 0.85;
      ctx.textAlign = 'center';
      ctx.fillText(p.name, tlx, tly);
    }

    ctx.globalAlpha = 1;
    ctx.restore();
  }

})();

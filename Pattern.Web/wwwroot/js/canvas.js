// canvas.js — Pattern Canvas Editor engine (fetches piece geometry from server)

(async function initCanvasEditor() {

  // ── Fetch piece geometry from server ──────────────────────────────
  const res = await fetch(window.PIECE_DATA_URL);
  const PIECES_DEF = await res.json();

  const LAYOUT_OFFSETS = [
    {x:30,y:20},{x:310,y:20},{x:620,y:20},{x:1060,y:20},
    {x:1195,y:20},{x:1320,y:20},{x:1060,y:260},{x:1320,y:260},{x:1440,y:260}
  ];

  let pieces = PIECES_DEF.map((def, i) => {
    const off = LAYOUT_OFFSETS[i] || {x: 30 + i * 200, y: 20};
    return { ...def, pts: def.pts.map(([x, y]) => ({x, y})), ox: off.x, oy: off.y };
  });

  let selectedPiece = window.CANVAS_INIT_PIECE || 0;
  let scale = 1, panX = 0, panY = 0;
  let showSA = true, showGrain = true, showLabels = true, tool = 'select';
  let draggingHandle = null, isPanning = false, panSX = 0, panSY = 0;
  let history = [];

  const canvas = document.getElementById('cv');
  const ctx    = canvas?.getContext('2d');
  if (!canvas || !ctx) return;

  function resize() {
    const p = canvas.parentElement;
    canvas.width  = p.clientWidth;
    canvas.height = p.clientHeight;
  }
  resize();
  new ResizeObserver(resize).observe(canvas.parentElement);

  // ── Colour dots in piece list ──────────────────────────────────────
  PIECES_DEF.forEach((def, i) => {
    const dot = document.getElementById(`cpl-dot-${i}`);
    if (dot) dot.style.background = def.col;
  });

  // ── Piece list click ───────────────────────────────────────────────
  document.getElementById('cpl-list')?.addEventListener('click', e => {
    const item = e.target.closest('[data-index]');
    if (item) selectPiece(parseInt(item.dataset.index));
  });

  function selectPiece(i) {
    selectedPiece = i;
    document.querySelectorAll('.cpl-item').forEach((el, idx) =>
      el.classList.toggle('active', idx === i));
    const p = pieces[i];
    const cpPiece = document.getElementById('cp-piece');
    const cpPts   = document.getElementById('cp-pts');
    if (cpPiece) cpPiece.textContent = p.name;
    if (cpPts)   cpPts.textContent   = p.pts.length;
    draw();
  }

  // ── Toolbar buttons ────────────────────────────────────────────────
  const toolBtns = { 'ctb-select': 'select', 'ctb-move': 'move', 'ctb-point': 'point' };
  Object.entries(toolBtns).forEach(([id, t]) => {
    document.getElementById(id)?.addEventListener('click', e => {
      tool = t;
      document.querySelectorAll('.ctb-btn').forEach(b => b.classList.remove('active'));
      e.currentTarget.classList.add('active');
    });
  });

  document.getElementById('tog-sa-btn')?.addEventListener('click',     () => { showSA     = !showSA;     draw(); });
  document.getElementById('tog-grain-btn')?.addEventListener('click',  () => { showGrain  = !showGrain;  draw(); });
  document.getElementById('tog-labels-btn')?.addEventListener('click', () => { showLabels = !showLabels; draw(); });

  // ── Toggle buttons in props panel ─────────────────────────────────
  ['tog-sa','tog-grain','tog-labels'].forEach(id => {
    document.getElementById(id)?.setAttribute('data-managed', '1');
  });
  document.getElementById('tog-sa')?.addEventListener('click',     () => { showSA     = !showSA;     document.getElementById('tog-sa').classList.toggle('on',showSA);     draw(); });
  document.getElementById('tog-grain')?.addEventListener('click',  () => { showGrain  = !showGrain;  document.getElementById('tog-grain').classList.toggle('on',showGrain);   draw(); });
  document.getElementById('tog-labels')?.addEventListener('click', () => { showLabels = !showLabels; document.getElementById('tog-labels').classList.toggle('on',showLabels); draw(); });

  document.getElementById('btn-zoom-in')?.addEventListener('click',  () => { scale = Math.max(0.15, Math.min(4, scale + 0.1)); updateZoom(); draw(); });
  document.getElementById('btn-zoom-out')?.addEventListener('click', () => { scale = Math.max(0.15, Math.min(4, scale - 0.1)); updateZoom(); draw(); });
  document.getElementById('btn-undo')?.addEventListener('click',     () => { if (history.length) { pieces[selectedPiece].pts = history.pop(); draw(); toast('Undo','Last edit undone','info','↺'); } });
  document.getElementById('btn-fit-all')?.addEventListener('click',  fitAll);

  // ── Mouse events ───────────────────────────────────────────────────
  canvas.addEventListener('mousedown', e => {
    const r = canvas.getBoundingClientRect();
    const sx = e.clientX - r.left, sy = e.clientY - r.top;
    if (tool === 'move' || e.button === 1) { isPanning = true; panSX = sx - panX; panSY = sy - panY; return; }
    if (tool === 'select') {
      const hi = getHandle(sx, sy);
      if (hi >= 0) { history.push(pieces[selectedPiece].pts.map(pt => ({...pt}))); draggingHandle = hi; return; }
      const pi = getPieceAt(sx, sy);
      if (pi >= 0) selectPiece(pi);
    }
  });

  canvas.addEventListener('mousemove', e => {
    const r = canvas.getBoundingClientRect();
    const sx = e.clientX - r.left, sy = e.clientY - r.top;
    if (isPanning)           { panX = sx - panSX; panY = sy - panSY; draw(); return; }
    if (draggingHandle !== null) {
      const p = pieces[selectedPiece];
      const [wx, wy] = toWorld(sx, sy);
      p.pts[draggingHandle] = { x: wx - p.ox, y: wy - p.oy };
      draw();
    }
  });

  canvas.addEventListener('mouseup',  () => { draggingHandle = null; isPanning = false; });
  canvas.addEventListener('wheel', e => {
    e.preventDefault();
    const r = canvas.getBoundingClientRect();
    const sx = e.clientX - r.left, sy = e.clientY - r.top;
    const [wx, wy] = toWorld(sx, sy);
    scale = Math.max(0.15, Math.min(4, scale + (e.deltaY > 0 ? -0.08 : 0.08)));
    panX = sx - wx * scale; panY = sy - wy * scale;
    updateZoom(); draw();
  }, { passive: false });

  // ── Draw ───────────────────────────────────────────────────────────
  function draw() {
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    pieces.forEach((p, i) => drawPiece(p, i === selectedPiece));
  }

  function drawPiece(p, sel) {
    const { ox, oy, pts, grain, cf, notches, col } = p;
    if (!pts.length) return;
    const ts = (x, y) => toScreen(x + ox, y + oy);
    ctx.save();

    if (showSA) {
      ctx.beginPath();
      const [sx0,sy0] = ts(pts[0].x, pts[0].y); ctx.moveTo(sx0, sy0);
      pts.forEach(pt => { const [x,y] = ts(pt.x, pt.y); ctx.lineTo(x,y); }); ctx.closePath();
      ctx.strokeStyle = 'rgba(185,28,28,0.14)'; ctx.lineWidth = 8*scale*2; ctx.lineJoin = 'miter'; ctx.miterLimit = 1.5; ctx.stroke();
      ctx.strokeStyle = 'rgba(247,246,244,0.92)'; ctx.lineWidth = 8*scale*2 - 2; ctx.stroke();
    }

    ctx.beginPath();
    const [sx0,sy0] = ts(pts[0].x, pts[0].y); ctx.moveTo(sx0, sy0);
    pts.forEach(pt => { const [x,y] = ts(pt.x, pt.y); ctx.lineTo(x,y); }); ctx.closePath();
    ctx.fillStyle = sel ? 'rgba(23,23,23,0.07)' : 'rgba(23,23,23,0.03)'; ctx.fill();
    ctx.strokeStyle = sel ? col : 'rgba(23,23,23,0.3)'; ctx.lineWidth = sel ? 2 : 1; ctx.setLineDash([]); ctx.stroke();

    if (cf && showGrain) {
      const [x1,y1] = ts(cf[0][0], cf[0][1]), [x2,y2] = ts(cf[cf.length-1][0], cf[cf.length-1][1]);
      ctx.beginPath(); ctx.moveTo(x1,y1); ctx.lineTo(x2,y2);
      ctx.strokeStyle = '#b91c1c'; ctx.lineWidth = 0.9; ctx.setLineDash([4,3]); ctx.stroke(); ctx.setLineDash([]);
    }

    if (grain && showGrain) {
      const [gx1,gy1] = ts(grain[0][0], grain[0][1]), [gx2,gy2] = ts(grain[1][0], grain[1][1]);
      ctx.beginPath(); ctx.moveTo(gx1,gy1); ctx.lineTo(gx2,gy2);
      ctx.strokeStyle = col; ctx.lineWidth = 0.9; ctx.globalAlpha = 0.35; ctx.stroke(); ctx.globalAlpha = 1;
    }

    if (notches) notches.forEach(([nx,ny]) => {
      const [sx,sy] = ts(nx,ny);
      ctx.beginPath(); ctx.moveTo(sx-4*scale,sy); ctx.lineTo(sx+4*scale,sy);
      ctx.strokeStyle = '#991b1b'; ctx.lineWidth = 1.5; ctx.setLineDash([]); ctx.stroke();
      ctx.beginPath(); ctx.moveTo(sx,sy-4*scale); ctx.lineTo(sx,sy+4*scale); ctx.stroke();
    });

    if (showLabels && scale > 0.35) {
      const cx = pts.reduce((a,pt) => a+pt.x, 0)/pts.length + ox;
      const cy = pts.reduce((a,pt) => a+pt.y, 0)/pts.length + oy;
      const [lx,ly] = toScreen(cx, cy);
      ctx.font = `600 ${Math.max(8, 10*scale)}px 'IBM Plex Mono',ui-monospace,monospace`;
      ctx.fillStyle = '#334155'; ctx.textAlign = 'center'; ctx.fillText(p.name, lx, ly);
      ctx.font = `${Math.max(7, 8*scale)}px 'IBM Plex Mono',ui-monospace,monospace`;
      ctx.fillStyle = '#64748b'; ctx.fillText(p.cut, lx, ly + 11*scale);
    }

    if (sel) pts.forEach(pt => {
      const [hx,hy] = ts(pt.x, pt.y);
      ctx.beginPath(); ctx.arc(hx,hy,5.5,0,Math.PI*2);
      ctx.fillStyle = '#fff'; ctx.fill(); ctx.strokeStyle = col; ctx.lineWidth = 2; ctx.stroke();
    });

    ctx.restore();
  }

  function fitAll() {
    const PAD = 40, W = canvas.width, H = canvas.height;
    let minX=Infinity, minY=Infinity, maxX=-Infinity, maxY=-Infinity;
    pieces.forEach(p => p.pts.forEach(pt => {
      const wx = pt.x + p.ox, wy = pt.y + p.oy;
      if (wx<minX) minX=wx; if (wy<minY) minY=wy; if (wx>maxX) maxX=wx; if (wy>maxY) maxY=wy;
    }));
    const bw = maxX-minX, bh = maxY-minY;
    scale = Math.min((W-PAD*2)/bw, (H-PAD*2)/bh, 1.2);
    panX = (W-bw*scale)/2 - minX*scale; panY = (H-bh*scale)/2 - minY*scale;
    updateZoom(); draw();
  }

  function toScreen(wx, wy) { return [wx*scale+panX, wy*scale+panY]; }
  function toWorld(sx, sy)  { return [(sx-panX)/scale, (sy-panY)/scale]; }

  function getHandle(sx, sy) {
    const p = pieces[selectedPiece];
    for (let i=0; i<p.pts.length; i++) {
      const [hx,hy] = toScreen(p.pts[i].x+p.ox, p.pts[i].y+p.oy);
      if (Math.abs(sx-hx)<9 && Math.abs(sy-hy)<9) return i;
    }
    return -1;
  }

  function getPieceAt(sx, sy) {
    const [wx, wy] = toWorld(sx, sy);
    for (let i=pieces.length-1; i>=0; i--) {
      const p = pieces[i];
      if (pointInPoly(wx, wy, p.pts.map(pt => ({x:pt.x+p.ox, y:pt.y+p.oy})))) return i;
    }
    return -1;
  }

  function pointInPoly(px, py, pts) {
    let inside = false;
    for (let i=0, j=pts.length-1; i<pts.length; j=i++) {
      const xi=pts[i].x, yi=pts[i].y, xj=pts[j].x, yj=pts[j].y;
      if (((yi>py)!==(yj>py)) && (px<(xj-xi)*(py-yi)/(yj-yi)+xi)) inside=!inside;
    }
    return inside;
  }

  function updateZoom() {
    const el = document.getElementById('cv-zoom-label');
    if (el) el.textContent = Math.round(scale*100) + '%';
  }

  // ── Initial render ─────────────────────────────────────────────────
  selectPiece(selectedPiece);
  fitAll();

})();

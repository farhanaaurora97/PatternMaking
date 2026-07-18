// nest.js — Graded Nest canvas renderer (fetches base piece from server)

(async function initNest() {

  const res = await fetch(window.NEST_BASE_URL);
  const basePts = await res.json();

  const colors  = window.NEST_COLORS || ['#57534e','#b91c1c','#171717','#15803d','#78716c','#991b1b'];
  const labels  = window.NEST_LABELS || ['XS','S','M','L','XL','XXL'];
  const gscales = [0.82, 0.91, 1, 1.09, 1.18, 1.28];
  let nestScale = 1;

  const canvas = document.getElementById('nest-cv');
  if (!canvas) return;
  const ctx = canvas.getContext('2d');

  function resize() {
    canvas.width  = canvas.parentElement.clientWidth;
    canvas.height = 420;
    canvas.style.height = '420px';
    draw();
  }
  resize();
  new ResizeObserver(resize).observe(canvas.parentElement);

  function draw() {
    const W = canvas.width, H = canvas.height;
    ctx.clearRect(0, 0, W, H);
    const cx = W/2, cy = H/2 + 20;
    const sc = Math.min(W, H) / 420 * nestScale;

    labels.forEach((label, si) => {
      const gsc = gscales[si];
      ctx.beginPath();
      basePts.forEach(([x, y], i) => {
        const wx = (x-178)*gsc*sc + cx;
        const wy = (y-220)*gsc*sc + cy;
        i === 0 ? ctx.moveTo(wx, wy) : ctx.lineTo(wx, wy);
      });
      ctx.closePath();
      ctx.strokeStyle = colors[si];
      ctx.lineWidth   = si === 2 ? 2.5 : 1.2;
      ctx.setLineDash(si === 2 ? [] : [4,3]);
      ctx.stroke();
      ctx.setLineDash([]);

      const lx = (basePts[0][0]-178)*gsc*sc + cx;
      const ly = (basePts[0][1]-220)*gsc*sc + cy;
      ctx.font = `700 ${Math.max(9, 10*sc)}px 'IBM Plex Mono',ui-monospace,monospace`;
      ctx.fillStyle = colors[si];
      ctx.fillText(label, lx+4, ly+4);
    });
  }

  // ── Zoom ───────────────────────────────────────────────────────────
  document.getElementById('btn-nest-zoom-in')?.addEventListener('click', () => {
    nestScale = Math.min(2.5, nestScale + 0.1);
    updateZoomLabel(); draw();
  });
  document.getElementById('btn-nest-zoom-out')?.addEventListener('click', () => {
    nestScale = Math.max(0.4, nestScale - 0.1);
    updateZoomLabel(); draw();
  });

  function updateZoomLabel() {
    const el = document.getElementById('nest-zoom-lbl');
    if (el) el.textContent = Math.round(nestScale * 100) + '%';
  }

  // ── Toggle sizes ───────────────────────────────────────────────────
  document.getElementById('btn-toggle-sizes')?.addEventListener('click',
    () => toast('Sizes', 'Toggle sizes coming soon.', 'info', '👁️'));

})();

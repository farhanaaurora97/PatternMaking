// export.js — Export page: format selection, animated progress

(function initExport() {

  let selectedFormat = 'DXF';

  // ── Format card selection ──────────────────────────────────────────
  document.querySelectorAll('.export-card').forEach(card => {
    card.addEventListener('click', () => {
      document.querySelectorAll('.export-card').forEach(c => c.classList.remove('selected'));
      card.classList.add('selected');
      selectedFormat = card.dataset.format;

      const el = document.getElementById('exp-format');
      if (el) {
        el.textContent = selectedFormat;
        el.className = 'tag ' + (selectedFormat === 'DXF' ? 'tag-green' : selectedFormat === 'PDF' ? 'tag-gold' : 'tag-purple');
      }
      toast('Format Selected', `${selectedFormat} selected as export format`, 'info', '📁');
    });
  });

  // ── Toggle buttons ─────────────────────────────────────────────────
  document.querySelectorAll('.tog').forEach(tog => {
    tog.setAttribute('data-managed', '1');
    tog.addEventListener('click', () => tog.classList.toggle('on'));
  });

  // ── Preview ────────────────────────────────────────────────────────
  document.getElementById('btn-preview')?.addEventListener('click',
    () => toast('Preview', 'Preview feature coming soon.', 'info', '👁️'));

  // ── Download / run export ──────────────────────────────────────────
  document.getElementById('btn-download-export')?.addEventListener('click', runExport);

  async function runExport() {
    const wrap    = document.getElementById('export-progress-wrap');
    const bar     = document.getElementById('export-bar');
    const txt     = document.getElementById('export-progress-text');
    const stepsEl = document.getElementById('export-steps');
    if (!wrap) return;

    const res = await fetch(window.EXPORT_URL, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ format: selectedFormat }),
    });
    if (!res.ok) return;
    const steps = await res.json();

    wrap.classList.add('active');
    stepsEl.innerHTML = steps.map(s =>
      `<div class="export-step" id="xstep-${s.step}">
        <div class="step-icon" id="xstep-icon-${s.step}">○</div>
        <span>${s.label}</span>
       </div>`).join('');

    bar.style.width = '0%';
    let i = 0;

    function tick() {
      if (i >= steps.length) {
        bar.style.width = '100%';
        txt.textContent = `✅ Export complete — ${window.EXPORT_TOTAL_FILES} files ready`;
        markStep(i - 1, true);
        toast('Export Complete', `${selectedFormat} files packaged and ready to download`, 'success', '📦');
        triggerDownload();
        return;
      }
      if (i > 0) markStep(i - 1, true);
      activateStep(i);
      bar.style.width = ((i + 1) / steps.length * 100) + '%';
      txt.textContent = steps[i].label + '...';
      i++;
      setTimeout(tick, 700 + Math.random() * 400);
    }
    tick();
  }

  function markStep(idx, done) {
    const step = document.getElementById(`xstep-${idx}`);
    const icon = document.getElementById(`xstep-icon-${idx}`);
    if (!step) return;
    step.classList.remove('active-step');
    if (done) { step.classList.add('done'); if (icon) icon.textContent = '✓'; }
  }

  function activateStep(idx) {
    const step = document.getElementById(`xstep-${idx}`);
    const icon = document.getElementById(`xstep-icon-${idx}`);
    step?.classList.add('active-step');
    if (icon) icon.textContent = '◌';
  }

  function triggerDownload() {
    const params = new URLSearchParams({
      patternId: String(window.EXPORT_PATTERN_ID || 0),
      style: window.EXPORT_STYLE || 'skinny',
      format: selectedFormat,
      sizes: window.EXPORT_SIZES_CSV || 'XS,S,M,L,XL,XXL',
    });
    const url = `${window.DOWNLOAD_PACKAGE_URL}?${params.toString()}`;
    const a = document.createElement('a');
    a.href = url;
    a.download = '';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
  }

})();

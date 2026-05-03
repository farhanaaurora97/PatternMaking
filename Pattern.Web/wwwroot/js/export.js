// export.js — Export page: format selection, animated progress

(function initExport() {

  // After editing on Canvas, sidebar "Export" may land here without ?patternId=.
  // Prefer the pattern you had open on Canvas (session) when navigation came from there.
  (function syncPatternIdFromCanvasSession() {
    try {
      const ref = document.referrer || '';
      if (!/\/Canvas/i.test(ref)) return;
      const last = parseInt(sessionStorage.getItem('pp_last_canvas_pattern_id') || '0', 10);
      if (last > 0) window.EXPORT_PATTERN_ID = last;
    } catch (_) { /* ignore */ }
  })();

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

  /** Path-only action for GET form (e.g. /Export/DownloadPackage or /MyApp/Export/DownloadPackage). */
  function exportDownloadFormAction() {
    let p = (window.DOWNLOAD_PACKAGE_URL || '/Export/DownloadPackage').trim();
    if (/^https?:\/\//i.test(p)) {
      try {
        return new URL(p).pathname || '/Export/DownloadPackage';
      } catch (_) {
        return '/Export/DownloadPackage';
      }
    }
    if (!p.startsWith('/')) p = '/' + p.replace(/^\/+/, '');
    return p || '/Export/DownloadPackage';
  }

  /**
   * Real browser GET (form submit) into a named iframe — same as a normal file link download.
   * Avoids Chrome/Edge "File wasn't available on site" from programmatic fetch/blob or anchor download on live URLs.
   */
  function triggerExportDownload() {
    let frame = document.getElementById('pp-export-dl');
    if (!frame) {
      frame = document.createElement('iframe');
      frame.id = 'pp-export-dl';
      frame.name = 'pp-export-dl';
      frame.title = 'Export download';
      frame.setAttribute('aria-hidden', 'true');
      frame.style.cssText = 'position:absolute;width:0;height:0;border:0;left:-9999px;visibility:hidden';
      document.body.appendChild(frame);
    }

    const form = document.createElement('form');
    form.method = 'GET';
    form.action = exportDownloadFormAction();
    form.target = 'pp-export-dl';
    form.style.display = 'none';
    form.setAttribute('aria-hidden', 'true');

    const add = (name, value) => {
      const input = document.createElement('input');
      input.type = 'hidden';
      input.name = name;
      input.value = String(value);
      form.appendChild(input);
    };

    add('patternId', window.EXPORT_PATTERN_ID || 0);
    add('style', window.EXPORT_STYLE || 'skinny');
    add('format', selectedFormat);
    add('sizes', window.EXPORT_SIZES_CSV || 'XS,S,M,L,XL,XXL');
    add('_ts', Date.now());

    document.body.appendChild(form);
    form.submit();
    document.body.removeChild(form);
  }

  async function runExport() {
    const wrap    = document.getElementById('export-progress-wrap');
    const bar     = document.getElementById('export-bar');
    const txt     = document.getElementById('export-progress-text');
    const stepsEl = document.getElementById('export-steps');
    if (!wrap) return;

    try {
      triggerExportDownload();
    } catch (err) {
      toast('Download failed', err?.message || 'Could not start download.', 'error', '⚠️');
    }

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
        toast('Export Complete', `${selectedFormat} package downloading (check your Downloads folder)`, 'success', '📦');
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

})();

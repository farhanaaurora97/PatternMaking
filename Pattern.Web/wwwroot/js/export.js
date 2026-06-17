// export.js — Export page: format selection, production QC, animated progress

(function initExport() {

  (function syncPatternIdFromCanvasSession() {
    try {
      const urlPid = parseInt(new URL(location.href).searchParams.get('patternId') || '0', 10);
      if (urlPid > 0) return; // explicit pattern in URL wins over canvas session
      const ref = document.referrer || '';
      if (!/\/Canvas/i.test(ref)) return;
      const last = parseInt(sessionStorage.getItem('pp_last_canvas_pattern_id') || '0', 10);
      if (last > 0) window.EXPORT_PATTERN_ID = last;
    } catch (_) { /* ignore */ }
  })();

  let selectedFormat = 'DXF';
  let canExportFactory = window.CAN_EXPORT_FACTORY === true || window.CAN_EXPORT_FACTORY === 'true';

  document.querySelectorAll('.export-card').forEach(card => {
    card.addEventListener('click', () => {
      document.querySelectorAll('.export-card').forEach(c => c.classList.remove('selected'));
      card.classList.add('selected');
      selectedFormat = card.dataset.format;

      const el = document.getElementById('exp-format');
      if (el) {
        el.textContent = selectedFormat;
        el.className = 'tag ' + (
          selectedFormat === 'DXF' ? 'tag-green'
            : selectedFormat === 'HPGL' ? 'tag-gold'
            : selectedFormat === 'PLT' ? 'tag-purple'
            : selectedFormat === 'PDF' ? 'tag-blue'
            : 'tag-purple');
      }
      toast('Format Selected', `${selectedFormat} selected as export format`, 'info', '📁');
    });
  });

  document.querySelectorAll('.tog').forEach(tog => {
    tog.setAttribute('data-managed', '1');
    tog.addEventListener('click', () => tog.classList.toggle('on'));
  });

  document.getElementById('btn-preview')?.addEventListener('click',
    () => toast('Preview', 'Preview feature coming soon.', 'info', '👁️'));

  document.getElementById('btn-download-export')?.addEventListener('click', () => runExport('factory'));
  document.getElementById('btn-download-clo')?.addEventListener('click', () => runExport('clo'));
  document.getElementById('btn-download-draft')?.addEventListener('click', () => runExport('draft'));

  document.getElementById('btn-approve-cutting')?.addEventListener('click', approveForCutting);
  document.getElementById('btn-revoke-approval')?.addEventListener('click', revokeApproval);
  document.getElementById('btn-cutter-pass')?.addEventListener('click', () => recordCutterTest(true));
  document.getElementById('btn-cutter-fail')?.addEventListener('click', () => recordCutterTest(false));
  document.getElementById('btn-save-shrinkage')?.addEventListener('click', saveShrinkage);
  document.getElementById('btn-complete-certification')?.addEventListener('click', completeFactoryCertification);

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

  function triggerExportDownload(purpose) {
    const params = new URLSearchParams({
      patternId: String(window.EXPORT_PATTERN_ID || 0),
      style: window.EXPORT_STYLE || 'skinny',
      format: selectedFormat,
      sizes: window.EXPORT_SIZES_CSV || 'XS,S,M,L,XL,XXL',
      purpose: purpose || 'factory',
      _ts: String(Date.now()),
    });
    // Direct navigation is more reliable on Windows than a hidden iframe.
    window.location.assign(`${exportDownloadFormAction()}?${params}`);
  }

  async function runExport(purpose) {
    if (purpose === 'factory' && !canExportFactory && (window.EXPORT_PATTERN_ID || 0) > 0) {
      toast('Factory export blocked', 'Complete QC, approval, and cutter test first.', 'error', '⚠️');
      return;
    }

    const wrap = document.getElementById('export-progress-wrap');
    const bar = document.getElementById('export-bar');
    const txt = document.getElementById('export-progress-text');
    const stepsEl = document.getElementById('export-steps');
    if (!wrap) return;

    try {
      triggerExportDownload(purpose);
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
        const label = purpose === 'factory' ? 'Factory' : purpose === 'clo' ? 'CLO review' : 'Draft';
        txt.textContent = `✅ ${label} export complete`;
        markStep(i - 1, true);
        toast('Export Complete', `${selectedFormat} package downloading`, 'success', '📦');
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

  async function postJson(url, body) {
    const res = await fetch(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    });
    const data = await res.json().catch(() => ({}));
    if (!res.ok) throw new Error(data.message || data.title || res.statusText || 'Request failed');
    return data;
  }

  async function refreshValidation() {
    const pid = window.EXPORT_PATTERN_ID || 0;
    if (pid <= 0 || !window.VALIDATE_FACTORY_URL) return;
    const url = `${window.VALIDATE_FACTORY_URL}?patternId=${pid}&style=${encodeURIComponent(window.EXPORT_STYLE || 'skinny')}`;
    const res = await fetch(url);
    if (!res.ok) return;
    const report = await res.json();
    canExportFactory = !!report.canExportToFactory;
    window.CAN_EXPORT_FACTORY = canExportFactory;
    updateQcUi(report);
  }

  function updateQcUi(report) {
    const statusTag = document.getElementById('qc-status-tag');
    if (statusTag) {
      statusTag.textContent = report.canExportToFactory ? 'Ready for factory' : 'Not production certified';
      statusTag.className = 'tag ' + (report.canExportToFactory ? 'tag-green' : 'tag-gold');
    }

    const approvedTag = document.getElementById('qc-approved-tag');
    if (approvedTag) {
      approvedTag.textContent = report.approvedForCutting ? 'Approved' : 'Pending';
      approvedTag.className = 'tag ' + (report.approvedForCutting ? 'tag-green' : 'tag-purple');
    }

    const cutterTag = document.getElementById('qc-cutter-tag');
    if (cutterTag) {
      cutterTag.textContent = report.cutterTestPassed ? 'Passed' : 'Not recorded';
      cutterTag.className = 'tag ' + (report.cutterTestPassed ? 'tag-green' : 'tag-purple');
    }

    const factoryBtn = document.getElementById('btn-download-export');
    if (factoryBtn && (window.EXPORT_PATTERN_ID || 0) > 0) {
      factoryBtn.disabled = !report.canExportToFactory;
      factoryBtn.title = report.canExportToFactory
        ? 'Production-certified factory export'
        : 'Complete QC, approval, and cutter test first';
    }

    const revokeBtn = document.getElementById('btn-revoke-approval');
    if (revokeBtn) revokeBtn.disabled = !report.approvedForCutting;

    const list = document.getElementById('qc-issues-list');
    if (!list) return;
    const errors = report.issues || [];
    const warnings = report.warnings || [];
    let html = '';
    if (errors.length) {
      html += '<div style="color:#b91c1c;margin-bottom:8px;font-weight:600">Blocking issues</div><ul style="margin:0;padding-left:18px;color:#b91c1c" id="qc-errors-ul">';
      html += errors.map(e => `<li>${escapeHtml(e.message)}${e.detail ? ` <span style="color:var(--ink3)">— ${escapeHtml(e.detail)}</span>` : ''}</li>`).join('');
      html += '</ul>';
    }
    if (warnings.length) {
      html += '<div style="color:var(--ink2);margin-top:10px;font-weight:600">Warnings</div><ul style="margin:4px 0 0;padding-left:18px;color:var(--ink3)" id="qc-warnings-ul">';
      html += warnings.map(w => `<li>${escapeHtml(w.message)}</li>`).join('');
      html += '</ul>';
    }
    if (!errors.length && !warnings.length && (window.EXPORT_PATTERN_ID || 0) > 0) {
      html = '<div style="color:var(--ink3)">No geometry QC issues — complete approval and cutter test to enable factory download.</div>';
    }
    list.innerHTML = html;
  }

  function escapeHtml(s) {
    return String(s || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
  }

  async function approveForCutting() {
    try {
      await postJson(window.APPROVE_CUTTING_URL, {
        patternId: window.EXPORT_PATTERN_ID,
        style: window.EXPORT_STYLE,
        actor: 'Pattern Designer',
      });
      toast('Approved', 'Pattern approved for cutting', 'success', '✓');
      await refreshValidation();
    } catch (e) {
      toast('Approval failed', e.message, 'error', '⚠️');
      await refreshValidation();
    }
  }

  async function revokeApproval() {
    try {
      await postJson(window.REVOKE_APPROVAL_URL, { patternId: window.EXPORT_PATTERN_ID });
      toast('Revoked', 'Cutting approval revoked', 'info', '↩');
      await refreshValidation();
    } catch (e) {
      toast('Revoke failed', e.message, 'error', '⚠️');
    }
  }

  async function recordCutterTest(passed) {
    const notes = passed ? 'Trial cut on factory plotter — dimensions OK' : 'Trial cut failed — adjust before re-test';
    try {
      await postJson(window.RECORD_CUTTER_URL, {
        patternId: window.EXPORT_PATTERN_ID,
        passed,
        actor: 'Factory',
        notes,
      });
      toast(passed ? 'Cutter test passed' : 'Cutter test failed', notes, passed ? 'success' : 'warning', passed ? '✓' : '⚠️');
      await refreshValidation();
    } catch (e) {
      toast('Cutter test', e.message, 'error', '⚠️');
    }
  }

  async function completeFactoryCertification() {
    try {
      const data = await postJson(window.COMPLETE_CERTIFICATION_URL, {
        patternId: window.EXPORT_PATTERN_ID,
        style: window.EXPORT_STYLE,
        actor: 'Pattern Designer',
      });
      if (data.canExportToFactory) {
        toast('Factory ready', 'Pattern is production certified — you can download factory export.', 'success', '✓');
      } else {
        toast('Certification incomplete', 'Fix remaining blocking issues on Canvas.', 'warning', '⚠️');
      }
      await refreshValidation();
    } catch (e) {
      const msg = e.message || 'Certification failed';
      toast('Certification failed', msg, 'error', '⚠️');
      await refreshValidation();
    }
  }

  async function saveShrinkage() {
    const el = document.getElementById('shrinkage-pct');
    const percent = parseFloat(el?.value || '0');
    try {
      await postJson(window.SET_SHRINKAGE_URL, { patternId: window.EXPORT_PATTERN_ID, percent });
      toast('Shrinkage saved', `${percent}% on factory manifest`, 'success', '💧');
    } catch (e) {
      toast('Save failed', e.message, 'error', '⚠️');
    }
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

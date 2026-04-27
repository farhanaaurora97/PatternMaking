// dashboard-charts.js — Chart.js analytics; refreshes after CRUD via /Home/ChartsData

(function initDashboardCharts() {
  if (typeof Chart === 'undefined') return;
  if (typeof ChartDataLabels !== 'undefined') {
    Chart.register(ChartDataLabels);
  }

  /** Fallback count labels when chartjs-plugin-datalabels is unavailable. */
  const pantBarEndLabelsPlugin = {
    id: 'pantBarEndLabels',
    afterDatasetsDraw(chart) {
      if (typeof ChartDataLabels !== 'undefined') return;
      if (chart.canvas.id !== 'chart-pant-types') return;
      const { ctx, chartArea } = chart;
      const meta = chart.getDatasetMeta(0);
      if (!meta?.data?.length) return;
      ctx.save();
      ctx.font = "600 11px 'DM Sans', 'Inter', system-ui, sans-serif";
      ctx.textBaseline = 'middle';
      ctx.fillStyle = '#475569';
      ctx.textAlign = 'left';
      meta.data.forEach((bar, i) => {
        const v = chart.data.datasets[0].data[i];
        if (!v || v <= 0) return;
        const bx = typeof bar.x === 'number' ? bar.x : 0;
        const bbase = typeof bar.base === 'number' ? bar.base : 0;
        const y = typeof bar.y === 'number' ? bar.y : 0;
        const tx = Math.min(chartArea.right - 6, Math.max(bx, bbase) + 8);
        ctx.fillText(String(v), tx, y);
      });
      ctx.restore();
    },
  };
  Chart.register(pantBarEndLabelsPlugin);

  const font = "'DM Sans', 'Inter', system-ui, sans-serif";
  Chart.defaults.font.family = font;
  Chart.defaults.color = '#64748b';

  // Professional neutral grid & tick colors
  const grid = '#f1f5f9';
  const tick = '#94a3b8';

  /**
   * Professional muted palette per product line.
   * Two families:
   *   Cool (blue-navy-slate-teal) — structural cuts: denim, trousers, chinos, cargo, linen
   *   Warm (brown-plum-terracotta) — specialty fabrics: leather, palazzo, corduroy, workwear
   *   Neutral — casual/sport/other: joggers, shorts, sweatpants, dress, other
   */
  function colorForPantCategory(label) {
    const k = String(label || '').trim().toLowerCase();
    const palette = {
      // Cool family — blue / slate / teal
      denim:      '#1e3a5f',   // deep navy
      trousers:   '#2d5282',   // mid navy-blue
      chinos:     '#3a6491',   // steel blue
      cargo:      '#1e5f5a',   // deep teal
      linen:      '#2a7a6e',   // muted teal

      // Warm family — brown / plum / terracotta
      leather:    '#4a3320',   // dark walnut
      palazzo:    '#5a3a5c',   // plum
      corduroy:   '#5e3d1e',   // burnt sienna
      workwear:   '#4a3a1e',   // dark khaki

      // Neutral family — slate / gray-blue
      joggers:    '#3d5166',   // slate
      shorts:     '#5c6f82',   // cool gray-blue
      sweatpants: '#4a5568',   // charcoal slate
      dress:      '#2d3748',   // near-black slate

      other:      '#718096',   // neutral gray
    };
    return palette[k] ?? palette.other;
  }

  /**
   * Returns true if hex color is dark enough to warrant light text.
   * Threshold tuned for the new deep palette (all colors are dark).
   */
  function hexLuminance(hex) {
    const h = hex.replace('#', '');
    if (h.length !== 6) return 0.5;
    const r = parseInt(h.slice(0, 2), 16) / 255;
    const g = parseInt(h.slice(2, 4), 16) / 255;
    const b = parseInt(h.slice(4, 6), 16) / 255;
    return 0.2126 * r + 0.7152 * g + 0.0722 * b;
  }

  function escapeLegendHtml(s) {
    return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/"/g, '&quot;');
  }

  function fillPantTypeLegend(pantTypeList) {
    const el = document.getElementById('chart-pant-types-legend');
    if (!el) return;
    if (!pantTypeList || pantTypeList.length === 0) {
      el.innerHTML = '';
      return;
    }
    const labels = [...new Set(pantTypeList.map(s => s.label))].sort((a, b) => a.localeCompare(b));
    el.innerHTML = labels.map((lab) => {
      const c = colorForPantCategory(lab);
      return `<span class="pt-leg"><span class="pt-swatch" style="background:${c};border-radius:3px;"></span>${escapeLegendHtml(lab)}</span>`;
    }).join('');
  }

  function destroyChart(canvasId) {
    const el = document.getElementById(canvasId);
    if (!el) return;
    const existing = Chart.getChart(el);
    existing?.destroy();
  }

  function destroyAllCharts() {
    destroyChart('chart-status');
    destroyChart('chart-styles');
    destroyChart('chart-pant-types');
  }

  /**
   * Professional status doughnut color map.
   * Server sends s.color — if the server colors are not yet updated,
   * this override map normalises them to the refined palette.
   */
  const STATUS_COLOR_MAP = {
    // Common status key → professional hex
    active:     '#2d6a9a',   // steel blue
    complete:   '#276f5c',   // forest teal
    completed:  '#276f5c',
    draft:      '#a07c2a',   // muted gold
    archived:   '#7a3d2e',   // terracotta
    inactive:   '#718096',   // neutral slate
    pending:    '#5a6880',   // blue-gray
    other:      '#94a3b8',   // light slate
  };

  /**
   * Resolve a professional color for a status segment.
   * Falls back to the server-supplied color if the key is unrecognised.
   */
  function colorForStatus(key, serverColor) {
    const k = String(key || '').trim().toLowerCase();
    return STATUS_COLOR_MAP[k] ?? serverColor ?? STATUS_COLOR_MAP.other;
  }

  /**
   * Steel-blue gradient ramp for the styles bar chart.
   * t ∈ [0, 1] → lightest to darkest.
   */
  function stylesBarColor(t) {
    // From #aec6da (light steel) to #1e3a5f (deep navy)
    const r = Math.round(174 - t * 144);
    const g = Math.round(198 - t * 140);
    const b = Math.round(218 - t * 123);
    return `rgb(${r},${g},${b})`;
  }

  function renderCharts(payload) {
    const statusList  = payload.status    || [];
    const styleList   = payload.styles    || payload.Styles || [];
    const pantTypeList = payload.pantTypes || [];

    // ── Status Doughnut ──────────────────────────────────────────────────────
    const statusFiltered = statusList.filter(s => s.count > 0);
    const canvasStatus   = document.getElementById('chart-status');
    const emptyStatus    = document.getElementById('chart-status-empty');

    if (canvasStatus) {
      if (statusFiltered.length === 0) {
        canvasStatus.style.display = 'none';
        if (emptyStatus) {
          emptyStatus.removeAttribute('hidden');
          emptyStatus.hidden = false;
        }
      } else {
        canvasStatus.style.display = 'block';
        if (emptyStatus) {
          emptyStatus.hidden = true;
          emptyStatus.setAttribute('hidden', '');
        }
        const total = statusFiltered.reduce((a, s) => a + s.count, 0);

        new Chart(canvasStatus, {
          type: 'doughnut',
          data: {
            labels: statusFiltered.map(s => s.label),
            datasets: [{
              data:            statusFiltered.map(s => s.count),
              backgroundColor: statusFiltered.map(s => colorForStatus(s.key, s.color)),
              borderWidth:     2,
              borderColor:     '#ffffff',
              hoverOffset:     6,
              hoverBorderColor: '#ffffff',
            }],
          },
          options: {
            responsive:          true,
            maintainAspectRatio: false,
            cutout:              '65%',
            onHover(e, els) {
              e.native.target.style.cursor = els.length ? 'pointer' : 'default';
            },
            onClick(e, elements) {
              if (!elements.length) {
                window.applyDashboardStatusFilter?.(null);
                return;
              }
              const idx = elements[0].index;
              const k   = statusFiltered[idx]?.key;
              if (k) window.applyDashboardStatusFilter?.(k);
            },
            plugins: {
              legend: {
                position: 'bottom',
                labels: {
                  boxWidth:      10,
                  boxHeight:     10,
                  padding:       16,
                  usePointStyle: true,
                  color:         '#475569',
                  font:          { size: 12, weight: '500', family: font },
                },
              },
              tooltip: {
                backgroundColor: 'rgba(15, 23, 42, 0.92)',
                titleColor:      '#f1f5f9',
                bodyColor:       '#cbd5e1',
                padding:         12,
                cornerRadius:    8,
                callbacks: {
                  label(ctx) {
                    const n   = ctx.raw;
                    const pct = total ? Math.round((n / total) * 100) : 0;
                    return ` ${n} (${pct}%)`;
                  },
                },
              },
            },
          },
        });
      }
    }

    // ── Styles Bar ───────────────────────────────────────────────────────────
    const canvasStyles = document.getElementById('chart-styles');
    if (canvasStyles && styleList.length > 0) {
      const maxVal = Math.max(1, ...styleList.map(s => s.count));
      const n      = Math.max(styleList.length - 1, 1);

      new Chart(canvasStyles, {
        type: 'bar',
        data: {
          labels: styleList.map(s => s.label),
          datasets: [{
            label:           'Patterns',
            data:            styleList.map(s => s.count),
            backgroundColor: styleList.map((_, i) => stylesBarColor(i / n)),
            borderRadius:    5,
            borderSkipped:   false,
            maxBarThickness: 20,
          }],
        },
        options: {
          indexAxis:           'y',
          responsive:          true,
          maintainAspectRatio: false,
          scales: {
            x: {
              beginAtZero: true,
              suggestedMax: maxVal,
              grid:  { color: grid },
              ticks: { stepSize: 1, color: tick, font: { size: 11 } },
              border: { display: false },
            },
            y: {
              grid:  { display: false },
              ticks: {
                color: '#334155',
                font:  { size: 11, weight: '500', family: font },
              },
              border: { display: false },
            },
          },
          plugins: {
            legend: { display: false },
            tooltip: {
              backgroundColor: 'rgba(15, 23, 42, 0.92)',
              titleColor:      '#f1f5f9',
              bodyColor:       '#cbd5e1',
              padding:         12,
              cornerRadius:    8,
              callbacks: {
                label(ctx) {
                  const v = ctx.raw;
                  return ` ${v} pattern${v === 1 ? '' : 's'}`;
                },
              },
            },
          },
        },
      });
    }

    // ── Pant Types Bar ───────────────────────────────────────────────────────
    const canvasPantTypes = document.getElementById('chart-pant-types');
    const pantHint        = document.getElementById('chart-pant-types-hint');

    if (canvasPantTypes && pantTypeList.length > 0) {
      const maxPt        = Math.max(1, ...pantTypeList.map(s => s.count));
      const totalInChart = pantTypeList.reduce((a, s) => a + s.count, 0);
      const nBars        = pantTypeList.length;

      if (pantHint) {
        pantHint.textContent =
          `${totalInChart} pattern${totalInChart === 1 ? '' : 's'} across ${nBars} product line${nBars === 1 ? '' : 's'} · bars sorted by count · each color is fixed to its product line (key below) · hover for % of total`;
      }

      const barColors = pantTypeList.map(s => colorForPantCategory(s.label));
      fillPantTypeLegend(pantTypeList);

      new Chart(canvasPantTypes, {
        type: 'bar',
        data: {
          labels: pantTypeList.map(s => s.label),
          datasets: [{
            label:           'Patterns in workspace',
            data:            pantTypeList.map(s => s.count),
            backgroundColor: barColors,
            borderColor:     'rgba(255,255,255,0.12)',
            borderWidth:     1,
            borderRadius:    7,
            borderSkipped:   false,
            maxBarThickness: 28,
          }],
        },
        options: {
          indexAxis:           'y',
          responsive:          true,
          maintainAspectRatio: false,
          layout: { padding: { right: 38, left: 4, top: 4, bottom: 4 } },
          scales: {
            x: {
              beginAtZero: true,
              suggestedMax: Math.max(maxPt, 1),
              title: {
                display: true,
                text:    'Number of patterns',
                color:   '#94a3b8',
                font:    { size: 11, weight: '500', family: font },
                padding: { top: 8, bottom: 0 },
              },
              grid: {
                color:     'rgba(241, 245, 249, 0.9)',
                drawTicks: true,
              },
              ticks: {
                stepSize: 1,
                color:    '#94a3b8',
                font:     { size: 11, weight: '500' },
              },
              border: { display: false },
            },
            y: {
              grid:  { display: false },
              ticks: {
                color:    '#334155',
                font:     { size: 12, weight: '500', family: font },
                autoSkip: false,
                padding:  8,
              },
              border: { display: false },
            },
          },
          plugins: (() => {
            const base = {
              legend: { display: false },
              tooltip: {
                backgroundColor: 'rgba(15, 23, 42, 0.92)',
                titleColor:      '#f1f5f9',
                bodyColor:       '#cbd5e1',
                padding:         12,
                cornerRadius:    8,
                titleFont:       { size: 13, weight: '600', family: font },
                bodyFont:        { size: 12, family: font },
                callbacks: {
                  title(items) { return items[0]?.label ?? ''; },
                  label(ctx) {
                    const v   = ctx.raw;
                    const pct = totalInChart ? Math.round((v / totalInChart) * 100) : 0;
                    return [
                      `${v} pattern${v === 1 ? '' : 's'}`,
                      `${pct}% of all patterns in workspace`,
                    ];
                  },
                },
              },
            };

            if (typeof ChartDataLabels !== 'undefined') {
              base.datalabels = {
                anchor:    'end',
                align:     'end',
                offset:    6,
                clip:      false,
                // All palette colors are dark — always use light label text
                color:     (ctx) => {
                  const hex = barColors[ctx.dataIndex] ?? '#64748b';
                  return hexLuminance(hex) < 0.35 ? '#f0f4f8' : '#1e293b';
                },
                font:      { weight: '600', size: 11, family: font },
                formatter: (v) => (v > 0 ? String(v) : ''),
              };
            }
            return base;
          })(),
        },
      });
    } else {
      if (pantHint) pantHint.textContent = '';
      fillPantTypeLegend([]);
    }
  }

  function runFromPayload(payload) {
    const dataEl = document.getElementById('dashboard-chart-data');
    if (dataEl) {
      try { dataEl.textContent = JSON.stringify(payload); } catch { /* ignore */ }
    }
    destroyAllCharts();
    renderCharts(payload);
  }

  async function refreshDashboardCharts() {
    try {
      const res = await fetch('/Home/ChartsData', { headers: { Accept: 'application/json' } });
      if (!res.ok) return;
      const payload = await res.json();
      runFromPayload(payload);
    } catch { /* ignore */ }
  }

  window.refreshDashboardCharts = refreshDashboardCharts;

  // Initial load from embedded JSON
  const dataEl = document.getElementById('dashboard-chart-data');
  if (!dataEl) return;
  let payload;
  try {
    payload = JSON.parse(dataEl.textContent);
  } catch {
    return;
  }
  renderCharts(payload);

  window.addEventListener('pattern:created', () => {
    refreshDashboardCharts();
  });
})();
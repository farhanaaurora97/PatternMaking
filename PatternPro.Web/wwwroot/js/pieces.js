// pieces.js — Pieces page: filter, view toggle, animations

// ── Staggered card entry ──────────────────────────────────────────────────────
document.querySelectorAll('.pcs-card').forEach((card, i) => {
  card.style.animationDelay = `${i * 0.045}s`;
});
document.querySelectorAll('.pcs-cut-row').forEach((row, i) => {
  row.style.animationDelay = `${0.15 + i * 0.04}s`;
});

// ── Filter bar ────────────────────────────────────────────────────────────────
document.querySelectorAll('.pcs-filter-btn').forEach(btn => {
  btn.addEventListener('click', () => {
    document.querySelectorAll('.pcs-filter-btn').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
    const filter = btn.dataset.filter;
    document.querySelectorAll('.pcs-group').forEach(grp => {
      if (filter === 'all' || grp.dataset.group === filter) {
        grp.style.display = '';
        grp.style.animation = 'fadeUp .28s ease both';
      } else {
        grp.style.display = 'none';
      }
    });
  });
});

// ── View toggle (grid / list) ─────────────────────────────────────────────────
const body = document.getElementById('pcs-body');
document.querySelectorAll('.pvt-btn').forEach(btn => {
  btn.addEventListener('click', () => {
    document.querySelectorAll('.pvt-btn').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
    if (btn.dataset.view === 'list') {
      body?.classList.add('pcs-list-mode');
    } else {
      body?.classList.remove('pcs-list-mode');
    }
  });
});

// ── Cut bar animation on intersection ────────────────────────────────────────
const bars = document.querySelectorAll('.pcs-cut-bar-fill');
if ('IntersectionObserver' in window) {
  const obs = new IntersectionObserver(entries => {
    entries.forEach(entry => {
      if (entry.isIntersecting) {
        entry.target.style.width = entry.target.style.width; // trigger reflow
        obs.unobserve(entry.target);
      }
    });
  }, { threshold: 0.1 });
  bars.forEach(b => obs.observe(b));
} 

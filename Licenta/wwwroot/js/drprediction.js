document.addEventListener('DOMContentLoaded', function () {
    const root = document.querySelector('[data-dp-root]');

    if (root) {
        root.querySelectorAll('[data-copy]').forEach((b) => {
            b.addEventListener('click', async () => {
                const sel = b.getAttribute('data-copy');
                if (!sel) return;

                const el = root.querySelector(sel);
                if (!el) return;

                const text = (el.textContent || '').trim();
                if (!text) return;

                const old = b.innerHTML;

                try {
                    await navigator.clipboard.writeText(text);
                    b.innerHTML = '<i class="bi bi-check2 me-1"></i>Copied';
                    b.classList.add('btn-success');
                    setTimeout(() => {
                        b.classList.remove('btn-success');
                        b.innerHTML = old;
                    }, 900);
                } catch {
                    const ta = document.createElement('textarea');
                    ta.value = text;
                    document.body.appendChild(ta);
                    ta.select();
                    document.execCommand('copy');
                    document.body.removeChild(ta);
                }
            });
        });

        root.querySelectorAll('[data-toggle]').forEach((b) => {
            b.addEventListener('click', () => {
                const id = b.getAttribute('data-toggle');
                if (!id) return;

                const panel = document.getElementById(id);
                if (!panel) return;

                const isHidden = panel.classList.contains('d-none');
                if (isHidden) {
                    panel.classList.remove('d-none');
                    b.setAttribute('aria-expanded', 'true');
                    b.innerHTML = '<i class="bi bi-chevron-down me-2"></i>Hide';
                } else {
                    panel.classList.add('d-none');
                    b.setAttribute('aria-expanded', 'false');
                    b.innerHTML = '<i class="bi bi-chevron-down me-2"></i>Show';
                }
            });
        });
    }

    const form = document.getElementById('dpRunForm');
    const btn = document.getElementById('dpRunBtn');
    const overlay = document.getElementById('dpRunOverlay');

    if (form) {
        form.addEventListener('submit', function () {
            if (btn) {
                btn.classList.add('dp-running');
                const t = btn.querySelector('.dp-run-text');
                const l = btn.querySelector('.dp-run-loading');
                if (t) t.classList.add('d-none');
                if (l) l.classList.remove('d-none');
            }
            if (overlay) overlay.style.display = 'flex';
        });
    }
});
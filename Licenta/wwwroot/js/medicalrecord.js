document.addEventListener('DOMContentLoaded', function () {
    const copyBtns = document.querySelectorAll('[data-copy]');

    copyBtns.forEach(btn => {
        btn.addEventListener('click', function () {
            const targetSelector = this.getAttribute('data-copy');
            const targetEl = document.querySelector(targetSelector);

            if (targetEl) {
                const text = targetEl.innerText || targetEl.textContent;
                navigator.clipboard.writeText(text).then(() => {
                    const originalHtml = this.innerHTML;
                    this.innerHTML = '<i class="bi bi-check"></i> Copied';
                    this.classList.add('text-success');

                    setTimeout(() => {
                        this.innerHTML = originalHtml;
                        this.classList.remove('text-success');
                    }, 2000);
                });
            }
        });
    });

    const toggleBtns = document.querySelectorAll('[data-toggle]');
    toggleBtns.forEach(btn => {
        btn.addEventListener('click', function () {
            const targetId = this.getAttribute('data-toggle');
            const targetEl = document.getElementById(targetId);
            const isExpanded = this.getAttribute('aria-expanded') === 'true';

            if (targetEl) {
                if (isExpanded) {
                    targetEl.classList.add('d-none');
                    this.innerHTML = '<i class="bi bi-chevron-down me-2"></i>Show';
                    this.setAttribute('aria-expanded', 'false');
                } else {
                    targetEl.classList.remove('d-none');
                    this.innerHTML = '<i class="bi bi-chevron-up me-2"></i>Hide';
                    this.setAttribute('aria-expanded', 'true');
                }
            }
        });
    });

    const acceptForm = document.getElementById('mrAcceptForm');
    const acceptBtn = document.getElementById('mrAcceptBtn');

    if (acceptForm && acceptBtn) {
        acceptForm.addEventListener('submit', function () {
            const textSpan = acceptBtn.querySelector('.mr-accept-text');
            const loadingSpan = acceptBtn.querySelector('.mr-accept-loading');

            if (textSpan) textSpan.classList.add('d-none');
            if (loadingSpan) loadingSpan.classList.remove('d-none');

            acceptBtn.disabled = true;
        });
    }
});
document.addEventListener("DOMContentLoaded", function () {
    initFormHandlers();
    autoResizeTextarea();
});

function initFormHandlers() {
    const form = document.querySelector('form');
    const submitBtn = document.querySelector('button[type="submit"]');

    if (!form || !submitBtn) return;

    form.addEventListener('submit', function (e) {
        if (!form.checkValidity()) return;

        const select = form.querySelector('select');
        if (select && select.value === "") {
            e.preventDefault();
            select.focus();
            return;
        }

        submitBtn.classList.add('btn-loading');
        submitBtn.disabled = true;
        const originalText = submitBtn.textContent;
        submitBtn.innerHTML = '<span class="spinner"></span> Processing...';
    });
}

function autoResizeTextarea() {
    const textarea = document.querySelector('textarea');
    if (!textarea) return;

    textarea.addEventListener('input', function () {
        this.style.height = 'auto';
        this.style.height = (this.scrollHeight + 2) + 'px';
    });
}
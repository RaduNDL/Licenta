document.addEventListener("DOMContentLoaded", function () {
    setupFormSubmission();
    setupTextAreaResize();
});

function setupFormSubmission() {
    const form = document.querySelector('form');
    const submitBtn = document.querySelector('.btn-primary');

    if (!form || !submitBtn) return;

    form.addEventListener('submit', function (e) {
        if (!form.checkValidity()) return;

        submitBtn.classList.add('btn-loading');
        submitBtn.disabled = true;
        const originalText = submitBtn.textContent;
        submitBtn.innerHTML = '<span class="spinner"></span> Assigning...';
    });
}

function setupTextAreaResize() {
    const textarea = document.querySelector('textarea');
    if (textarea) {
        textarea.addEventListener('input', function () {
            this.style.height = 'auto';
            this.style.height = (this.scrollHeight + 2) + 'px';
        });
    }
}
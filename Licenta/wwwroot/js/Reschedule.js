document.addEventListener("DOMContentLoaded", function () {
    setupFormSubmission();
    setupAutoResize();
});

function setupFormSubmission() {
    const form = document.querySelector('form');
    const submitBtn = document.querySelector('.btn-primary');

    if (!form || !submitBtn) return;

    form.addEventListener('submit', function (e) {
        if (!form.checkValidity()) return;

        submitBtn.classList.add('btn-loading');
        submitBtn.setAttribute('disabled', 'disabled');

        const originalText = submitBtn.textContent;
        submitBtn.innerHTML = '<span class="spinner"></span> Processing...';
    });
}

function setupAutoResize() {
    const textareas = document.querySelectorAll('textarea.form-control');

    textareas.forEach(textarea => {
        textarea.addEventListener('input', function () {
            this.style.height = 'auto';
            this.style.height = (this.scrollHeight + 2) + 'px';
        });
    });
}
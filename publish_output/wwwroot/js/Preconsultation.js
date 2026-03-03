document.addEventListener('DOMContentLoaded', function () {
    const textareas = document.querySelectorAll('.auto-expand');
    const form = document.getElementById('consultationForm');
    const submitBtn = document.getElementById('submitBtn');

    textareas.forEach(textarea => {
        textarea.addEventListener('input', function () {
            this.style.height = 'auto';
            this.style.height = (this.scrollHeight) + 'px';
        });
    });

    if (form) {
        form.addEventListener('submit', function (e) {
            if (form.checkValidity()) {
                submitBtn.disabled = true;
                const originalContent = submitBtn.innerHTML;
                submitBtn.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i> Processing...';

                setTimeout(() => {
                    submitBtn.disabled = false;
                    submitBtn.innerHTML = originalContent;
                }, 10000);
            }
        });
    }

    const inputs = document.querySelectorAll('.form-control-modern');
    inputs.forEach(input => {
        input.addEventListener('focus', () => {
            const icon = input.parentElement.querySelector('.input-icon');
            if (icon) icon.style.color = '#1e3a8a';
        });

        input.addEventListener('blur', () => {
            const icon = input.parentElement.querySelector('.input-icon');
            if (icon) icon.style.color = '#9ca3af';
        });
    });
});
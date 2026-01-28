document.addEventListener('DOMContentLoaded', function () {
    const bodyInput = document.getElementById('bodyText');
    const charCountDisplay = document.getElementById('charCount');
    const form = document.getElementById('requestForm');
    const submitBtn = document.getElementById('submitBtn');

    if (bodyInput) {
        bodyInput.addEventListener('input', function () {
            const currentLength = this.value.length;
            charCountDisplay.textContent = currentLength;

            if (currentLength > 0) {
                charCountDisplay.parentElement.classList.remove('text-muted');
                charCountDisplay.parentElement.classList.add('text-primary');
            } else {
                charCountDisplay.parentElement.classList.add('text-muted');
            }
        });
    }

    if (form) {
        form.addEventListener('submit', function (e) {
            if (!form.checkValidity()) {
                return;
            }

            submitBtn.disabled = true;
            submitBtn.innerHTML = '<i class="fa-solid fa-circle-notch fa-spin"></i> Sending...';
            submitBtn.style.opacity = '0.8';
        });
    }
});
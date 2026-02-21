document.addEventListener("DOMContentLoaded", function () {
    enhanceTimeInputs();
    setupAddButtonLoading();
});

function enhanceTimeInputs() {
    const inputs = document.querySelectorAll('input[type="datetime-local"]');
    if (inputs.length > 0) {
        const now = new Date();
        now.setMinutes(now.getMinutes() - now.getTimezoneOffset());
        const minDate = now.toISOString().slice(0, 16);

        inputs.forEach(input => {
            if (!input.value) {
               
                input.min = minDate;
            }
        });
    }
}

function setupAddButtonLoading() {
    const addForm = document.querySelector('form[action*="AddOption"]');
    if (addForm) {
        addForm.addEventListener('submit', function (e) {
            if (!addForm.checkValidity()) return;

            const btn = addForm.querySelector('button[type="submit"]');
            if (btn) {
                const originalWidth = btn.offsetWidth;
                btn.style.width = originalWidth + 'px'; 
                btn.disabled = true;
                btn.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span>';

            }
        });
    }
}
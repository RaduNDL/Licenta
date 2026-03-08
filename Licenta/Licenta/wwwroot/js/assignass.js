document.addEventListener('DOMContentLoaded', function () {
    const removeForms = document.querySelectorAll('form[asp-page-handler="Remove"]');
    const assignForms = document.querySelectorAll('form[asp-page-handler="Assign"]');

    removeForms.forEach(form => {
        form.addEventListener('submit', function (e) {
            const assistantName = this.closest('.assistant-item').querySelector('.ast-name').textContent;
            if (!confirm(`Remove assignment for ${assistantName}?`)) {
                e.preventDefault();
            }
        });
    });

    assignForms.forEach(form => {
        form.addEventListener('submit', function (e) {
            const select = this.querySelector('select');
            const submitBtn = this.querySelector('button');

            if (!select.value) {
                e.preventDefault();
                return;
            }

            submitBtn.disabled = true;
            submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm" role="status"></span>';
        });
    });

    const statusAlert = document.getElementById('statusAlert');
    if (statusAlert) {
        setTimeout(() => {
            statusAlert.style.transition = 'opacity 0.5s ease';
            statusAlert.style.opacity = '0';
            setTimeout(() => statusAlert.remove(), 500);
        }, 3000);
    }
});
document.addEventListener('DOMContentLoaded', function () {
    const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    tooltipTriggerList.map(function (tooltipTriggerEl) {
        if (typeof bootstrap !== 'undefined') {
            return new bootstrap.Tooltip(tooltipTriggerEl);
        }
    });

    const actionForms = document.querySelectorAll('.action-form');

    actionForms.forEach(form => {
        form.addEventListener('submit', function (e) {
            const btn = this.querySelector('button');
            const btnText = btn.querySelector('.btn-text');
            const spinner = btn.querySelector('.spinner-border');

            if (btn.disabled) {
                e.preventDefault();
                return;
            }

            if (this.classList.contains('decline-form')) {
            } else {
                btn.disabled = true;
                btnText.textContent = "Processing...";
                spinner.classList.remove('d-none');
            }
        });
    });

    const declineForms = document.querySelectorAll('.decline-form');
    declineForms.forEach(form => {
        const btn = form.querySelector('button');
        btn.addEventListener('click', function (e) {
            e.preventDefault();

            if (confirm('Are you sure you want to decline this patient request?')) {
                const btnText = btn.querySelector('.btn-text');
                const spinner = btn.querySelector('.spinner-border');

                btn.disabled = true;
                btnText.textContent = "Declining...";
                spinner.classList.remove('d-none');

                form.submit();
            }
        });
    });
});
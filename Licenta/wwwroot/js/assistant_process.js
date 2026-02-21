document.addEventListener('DOMContentLoaded', function () {
    const form = document.getElementById('processForm');

    if (form) {
        form.addEventListener('submit', function (event) {
            const submitButton = document.activeElement;

            if (form.checkValidity()) {
                const buttons = form.querySelectorAll('button[type="submit"]');
                buttons.forEach(btn => {
                    btn.style.opacity = '0.7';
                    btn.style.pointerEvents = 'none';
                });

                if (submitButton && submitButton.type === 'submit') {
                    const originalContent = submitButton.innerHTML;
                    submitButton.innerHTML = '<span class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>Processing...';

                    setTimeout(() => {
                        if (!form.submit) {
                            submitButton.innerHTML = originalContent;
                            buttons.forEach(btn => {
                                btn.style.opacity = '1';
                                btn.style.pointerEvents = 'auto';
                            });
                        }
                    }, 8000);
                }
            } else {
                event.preventDefault();
                event.stopPropagation();
            }

            form.classList.add('was-validated');
        });
    }

    const doctorSelect = document.getElementById('SelectedDoctorId');
    if (doctorSelect) {
        doctorSelect.addEventListener('change', function () {

        });
    }
});
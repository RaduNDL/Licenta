document.addEventListener('DOMContentLoaded', function () {
    const togglePasswordBtn = document.getElementById('togglePassword');
    const passwordInput = document.getElementById('passwordInput');
    const icon = togglePasswordBtn.querySelector('i');

    if (togglePasswordBtn && passwordInput) {
        togglePasswordBtn.addEventListener('click', function () {
            const type = passwordInput.getAttribute('type') === 'password' ? 'text' : 'password';
            passwordInput.setAttribute('type', type);

            icon.classList.toggle('bi-eye');
            icon.classList.toggle('bi-eye-slash');
        });
    }

    const form = document.getElementById('createUserForm');
    form.addEventListener('submit', function (event) {
        if (!form.checkValidity()) {
            event.preventDefault();
            event.stopPropagation();
        }
        form.classList.add('was-validated');
    }, false);
});
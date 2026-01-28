document.addEventListener('DOMContentLoaded', function () {
    const fullNameInput = document.getElementById('fullNameInput');
    const profileNameDisplay = document.getElementById('profileNameDisplay');
    const initialsDisplay = document.getElementById('userInitials');

    function updateIdentity() {
        const name = fullNameInput.value.trim();

        if (name) {
            profileNameDisplay.textContent = name;
        } else {
            profileNameDisplay.textContent = "Assistant";
        }

        const parts = name.split(' ').filter(part => part.length > 0);
        let initials = '';
        if (parts.length >= 2) {
            initials = parts[0][0] + parts[parts.length - 1][0];
        } else if (parts.length === 1) {
            initials = parts[0].substring(0, 2);
        } else {
            initials = 'AS'; 
        }

        initialsDisplay.textContent = initials.toUpperCase();
    }

    updateIdentity();
    fullNameInput.addEventListener('input', updateIdentity);


    const greetingText = document.getElementById('greetingText');
    const greetingIcon = document.getElementById('greetingIcon');
    const hour = new Date().getHours();

    if (hour >= 5 && hour < 12) {
        greetingText.textContent = "Good Morning, Assistant";
        greetingIcon.className = "bi bi-sunrise fs-4 mb-2 d-block text-warning";
    } else if (hour >= 12 && hour < 18) {
        greetingText.textContent = "Good Afternoon, Assistant";
        greetingIcon.className = "bi bi-sun fs-4 mb-2 d-block text-warning";
    } else {
        greetingText.textContent = "Good Evening, Assistant";
        greetingIcon.className = "bi bi-moon-stars fs-4 mb-2 d-block text-primary";
    }
});
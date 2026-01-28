document.addEventListener('DOMContentLoaded', function () {
    const hour = new Date().getHours();
    const greetingEl = document.getElementById('greeting-text');
    if (greetingEl) {
        let msg = 'Welcome';
        if (hour < 12) msg = 'Good Morning';
        else if (hour < 18) msg = 'Good Afternoon';
        else msg = 'Good Evening';
        greetingEl.textContent = msg;
    }
});
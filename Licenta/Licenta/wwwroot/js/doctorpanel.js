document.addEventListener('DOMContentLoaded', () => {
    function updateClock() {
        const timeDisplay = document.getElementById('currentTimeDisplay');
        if (!timeDisplay) return;

        const now = new Date();
        const hours = String(now.getHours()).padStart(2, '0');
        const minutes = String(now.getMinutes()).padStart(2, '0');

        timeDisplay.textContent = `${hours}:${minutes}`;
    }

    function setGreeting() {
        const greetingEl = document.getElementById('dynamicGreeting');
        if (!greetingEl) return;

        const originalText = greetingEl.textContent;
        const namePart = originalText.includes('Dr.') ? originalText.substring(originalText.indexOf('Dr.')) : '';

        const hour = new Date().getHours();
        let greeting = 'Good evening';

        if (hour >= 5 && hour < 12) greeting = 'Good morning';
        else if (hour >= 12 && hour < 18) greeting = 'Good afternoon';

        greetingEl.textContent = `${greeting}, ${namePart}`;
    }

    setGreeting();
    updateClock();
    setInterval(updateClock, 30000);
});
document.addEventListener('DOMContentLoaded', () => {
    initTime();
    animateEntry();
});

function initTime() {
    const greetingEl = document.getElementById('greeting');
    const dateEl = document.getElementById('currentDate');
    const now = new Date();
    const hrs = now.getHours();

    let greet = 'Good Morning';
    if (hrs >= 12 && hrs < 18) greet = 'Good Afternoon';
    else if (hrs >= 18) greet = 'Good Evening';

    if (greetingEl) {
        if (!greetingEl.textContent.includes(',')) {
            greetingEl.textContent = greet;
        }
    }

    if (dateEl) {
        dateEl.textContent = now.toLocaleDateString('en-US', {
            weekday: 'long',
            year: 'numeric',
            month: 'long',
            day: 'numeric'
        });
    }
}

function animateEntry() {
    const cards = document.querySelectorAll('.glass-card');
    cards.forEach((card, index) => {
        card.style.opacity = '0';
        card.style.transform = 'translateY(20px)';
        setTimeout(() => {
            card.style.transition = 'all 0.6s cubic-bezier(0.16, 1, 0.3, 1)';
            card.style.opacity = '1';
            card.style.transform = 'translateY(0)';
        }, index * 100);
    });
}
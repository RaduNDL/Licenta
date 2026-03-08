document.addEventListener("DOMContentLoaded", function () {
    const cards = document.querySelectorAll('.fade-in');

    cards.forEach((card, index) => {
        card.style.opacity = '0';
        card.style.animationDelay = `${index * 0.1}s`;
    });
});
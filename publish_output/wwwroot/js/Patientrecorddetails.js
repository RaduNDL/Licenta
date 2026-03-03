document.addEventListener("DOMContentLoaded", function () {
    const elements = document.querySelectorAll('.fade-in');

    elements.forEach((el, index) => {
        el.style.animationDelay = `${index * 0.15}s`;
    });
});
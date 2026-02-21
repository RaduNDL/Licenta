document.addEventListener("DOMContentLoaded", function () {
    const items = document.querySelectorAll('.fade-in');

    items.forEach((item, index) => {
        item.style.animationDelay = `${index * 0.1}s`;
    });
});
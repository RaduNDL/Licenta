document.addEventListener('DOMContentLoaded', function () {

    const searchInput = document.getElementById('requestSearch');
    const cards = document.querySelectorAll('.request-card');
    const countLabel = document.querySelector('.request-count');

    if (searchInput) {
        searchInput.addEventListener('keyup', function (e) {
            const term = e.target.value.toLowerCase();
            let visibleCount = 0;

            cards.forEach(card => {
                const subject = card.getAttribute('data-subject');

                if (subject.includes(term)) {
                    card.style.display = "flex"; 
                    card.style.animation = 'none';
                    card.offsetHeight; 
                    card.style.animation = 'fadeUp 0.3s ease-out forwards';
                    visibleCount++;
                } else {
                    card.style.display = "none";
                }
            });

            countLabel.textContent = visibleCount === cards.length
                ? `${visibleCount} Records`
                : `${visibleCount} Found`;
        });
    }

    const alertBox = document.getElementById('statusAlert');
    if (alertBox) {
        setTimeout(() => {
            alertBox.style.opacity = '0';
            alertBox.style.transform = 'translateY(-20px)';
            setTimeout(() => alertBox.remove(), 500);
        }, 4000); 
    }
});
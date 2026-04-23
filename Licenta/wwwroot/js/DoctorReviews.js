(function () {
    'use strict';

    const searchInput = document.getElementById('dr-search');
    const sortSelect = document.getElementById('dr-sort');
    const ratingPills = document.querySelectorAll('.pill-btn');
    const reviewsList = document.getElementById('dr-reviews-list');
    const noResults = document.getElementById('dr-no-results');
    const visibleCount = document.getElementById('dr-visible-count');

    if (!reviewsList) return;

    let currentRating = 0;

    function applyFilters() {
        const cards = Array.from(reviewsList.querySelectorAll('.dr-review-card'));
        const searchText = (searchInput?.value || '').toLowerCase().trim();
        const sortBy = sortSelect?.value || 'newest';

        let visible = cards.filter(card => {
            const rating = parseInt(card.dataset.rating, 10);
            const searchData = (card.dataset.search || '').toLowerCase();

            const matchesRating = currentRating === 0 || rating === currentRating;
            const matchesSearch = searchText === '' || searchData.includes(searchText);

            return matchesRating && matchesSearch;
        });

        visible.sort((a, b) => {
            const dateA = parseInt(a.dataset.date, 10);
            const dateB = parseInt(b.dataset.date, 10);
            const ratingA = parseInt(a.dataset.rating, 10);
            const ratingB = parseInt(b.dataset.rating, 10);

            switch (sortBy) {
                case 'oldest': return dateA - dateB;
                case 'highest': return ratingB - ratingA || dateB - dateA;
                case 'lowest': return ratingA - ratingB || dateB - dateA;
                case 'newest':
                default: return dateB - dateA;
            }
        });

        cards.forEach(card => { card.style.display = 'none'; });

        visible.forEach(card => {
            card.style.display = '';
            reviewsList.appendChild(card);
        });

        if (visibleCount) visibleCount.textContent = visible.length;

        if (visible.length === 0 && cards.length > 0) {
            noResults?.classList.remove('d-none');
        } else {
            noResults?.classList.add('d-none');
        }
    }

    if (searchInput) {
        let timer;
        searchInput.addEventListener('input', () => {
            clearTimeout(timer);
            timer = setTimeout(applyFilters, 180);
        });
    }

    if (sortSelect) sortSelect.addEventListener('change', applyFilters);

    ratingPills.forEach(btn => {
        btn.addEventListener('click', () => {
            ratingPills.forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            currentRating = parseInt(btn.dataset.rating, 10);
            applyFilters();
        });
    });
})();
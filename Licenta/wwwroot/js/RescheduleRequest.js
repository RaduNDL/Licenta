document.addEventListener("DOMContentLoaded", function () {
    formatStatusBadges();
    enableRowClick();
});

function formatStatusBadges() {
    const badges = document.querySelectorAll('.table .badge');

    badges.forEach(badge => {
        const status = badge.textContent.trim().toLowerCase();

        badge.className = 'badge';

        if (status === 'requested' || status === 'pending') {
            badge.classList.add('badge-requested');
        } else if (status === 'approved' || status === 'completed') {
            badge.classList.add('badge-approved');
        } else if (status === 'rejected' || status === 'cancelled') {
            badge.classList.add('badge-rejected');
        } else {
            badge.style.backgroundColor = '#f3f4f6';
            badge.style.color = '#4b5563';
            badge.style.border = '1px solid #e5e7eb';
        }
    });
}

function enableRowClick() {
    const rows = document.querySelectorAll('tbody tr');

    rows.forEach(row => {
        const actionLink = row.querySelector('a.btn-primary');
        if (actionLink) {
            row.style.cursor = 'pointer';
            row.addEventListener('click', (e) => {
                if (e.target !== actionLink && !actionLink.contains(e.target)) {
                    actionLink.click();
                }
            });
        }
    });
}
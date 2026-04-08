document.addEventListener('DOMContentLoaded', () => {
    const refreshBtn = document.querySelector('.btn-modern-refresh');

    if (refreshBtn) {
        refreshBtn.addEventListener('click', () => {
            const originalHtml = refreshBtn.innerHTML;
            refreshBtn.innerHTML = '<i class="bi bi-arrow-clockwise fa-spin"></i> Refreshing...';
            refreshBtn.style.pointerEvents = 'none';

            setTimeout(() => {
                window.location.reload();
            }, 500);
        });
    }

    const rows = document.querySelectorAll('.table-row-hover');

    rows.forEach(row => {
        row.style.cursor = 'pointer';
        row.addEventListener('click', (e) => {
            if (!e.target.closest('a') && !e.target.closest('button')) {
                const actionBtn = row.querySelector('.btn-modern-action');
                if (actionBtn) {
                    actionBtn.click();
                }
            }
        });
    });
});
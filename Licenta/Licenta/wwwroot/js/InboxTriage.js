document.addEventListener("DOMContentLoaded", function () {
    setupRowClickNavigation();
    formatFileNames();
});

function setupRowClickNavigation() {
    const rows = document.querySelectorAll('.table tbody tr');

    rows.forEach(row => {
        const assignBtn = row.querySelector('.btn-primary');

        if (assignBtn) {
            const targetUrl = assignBtn.getAttribute('href');

            row.addEventListener('click', function (e) {

                if (e.target.tagName === 'A' || e.target.tagName === 'BUTTON' || window.getSelection().toString().length > 0) {
                    return;
                }

                window.location.href = targetUrl;
            });
        }
    });
}

function formatFileNames() {
    const fileCells = document.querySelectorAll('.file-name');
    fileCells.forEach(cell => {
        const text = cell.textContent.trim();
        cell.title = text;

        
    });
}
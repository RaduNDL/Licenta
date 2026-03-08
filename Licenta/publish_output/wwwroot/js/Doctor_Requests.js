document.addEventListener('DOMContentLoaded', function () {
    const tooltipTriggerList = [].slice.call(document.querySelectorAll('[title]'));
    tooltipTriggerList.map(function (tooltipTriggerEl) {
        if (typeof bootstrap !== 'undefined') {
            return new bootstrap.Tooltip(tooltipTriggerEl);
        }
    });

    const rows = document.querySelectorAll('.custom-table tbody tr');
    rows.forEach(row => {
        row.addEventListener('mouseenter', function () {
            this.style.backgroundColor = '#f8f9fc';
            this.style.transition = 'background-color 0.2s ease';
        });
        row.addEventListener('mouseleave', function () {
            this.style.backgroundColor = '';
        });
    });
});
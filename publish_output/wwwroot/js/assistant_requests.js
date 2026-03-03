document.addEventListener('DOMContentLoaded', function () {
    var tooltipTriggerList = [].slice.call(document.querySelectorAll('[title]'));
    var tooltipList = tooltipTriggerList.map(function (tooltipTriggerEl) {
        if (typeof bootstrap !== 'undefined') {
            return new bootstrap.Tooltip(tooltipTriggerEl);
        }
    });


    const urlParams = new URLSearchParams(window.location.search);
    const statusParam = urlParams.get('status');

    if (statusParam === null) {
        const allLink = document.querySelector('a[href$="status="]');
        if (allLink) allLink.classList.add('active');
    }
});
document.addEventListener('DOMContentLoaded', function () {
    var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    tooltipTriggerList.map(function (tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl);
    });

    const currentPath = window.location.pathname;
    const navLinks = document.querySelectorAll('.nav-link');

    navLinks.forEach(link => {
        const href = link.getAttribute('href');
        if (href && href !== '#' && currentPath.toLowerCase().startsWith(href.toLowerCase())) {
            link.classList.add('active');

            const dropdownParent = link.closest('.dropdown');
            if (dropdownParent) {
                const parentToggle = dropdownParent.querySelector('.dropdown-toggle');
                if (parentToggle) parentToggle.classList.add('active');
            }
        }
    });

    const notifDropdown = document.querySelector('.notif-dropdown');
    const notifBell = document.getElementById('notifBell');

    if (notifDropdown && notifBell && window.bootstrap && bootstrap.Dropdown) {
        const dd = bootstrap.Dropdown.getOrCreateInstance(notifBell, { autoClose: 'outside' });

        let hideTimer = null;

        const showMenu = () => {
            if (hideTimer) {
                clearTimeout(hideTimer);
                hideTimer = null;
            }
            dd.show();
        };

        const hideMenuSoon = () => {
            if (hideTimer) clearTimeout(hideTimer);
            hideTimer = setTimeout(() => dd.hide(), 180);
        };

        notifDropdown.addEventListener('mouseenter', showMenu);
        notifDropdown.addEventListener('mouseleave', hideMenuSoon);

        notifBell.addEventListener('click', function (e) {
            e.preventDefault();
            const target = notifBell.href;
            if (target) window.location.href = target;
        });

        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') dd.hide();
        });
    }
});

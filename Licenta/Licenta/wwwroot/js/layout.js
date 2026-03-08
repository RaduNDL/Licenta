document.addEventListener('DOMContentLoaded', function () {

    const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    tooltipTriggerList.map(function (tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl);
    });


    const currentPath = window.location.pathname.toLowerCase();
    const navLinks = document.querySelectorAll('.main-menu .nav-link');

    navLinks.forEach(link => {
        const href = link.getAttribute('href');
        if (!href || href === '#') return;

        const linkPath = href.toLowerCase();

        if (currentPath === linkPath || (linkPath !== '/' && currentPath.startsWith(linkPath))) {
            link.classList.add('active');

            const dropdownParent = link.closest('.dropdown');
            if (dropdownParent) {
                const parentToggle = dropdownParent.querySelector('.dropdown-toggle');
                if (parentToggle) parentToggle.classList.add('active');

                link.classList.add('active');
            }
        }
    });

    const navbar = document.querySelector('.navbar-modern');
    if (navbar) {
        window.addEventListener('scroll', () => {
            if (window.scrollY > 20) {
                navbar.classList.add('scrolled');
            } else {
                navbar.classList.remove('scrolled');
            }
        });
 
        if (window.scrollY > 20) navbar.classList.add('scrolled');
    }

    if (window.innerWidth >= 1200) {
        document.querySelectorAll('.dropdown-hover').forEach(dropdown => {
            dropdown.addEventListener('mouseenter', () => {
                const toggle = dropdown.querySelector('[data-bs-toggle="dropdown"]');
                if (toggle && window.bootstrap) {
                    const inst = bootstrap.Dropdown.getOrCreateInstance(toggle);
                    inst.show();
                }
            });
            dropdown.addEventListener('mouseleave', () => {
                const toggle = dropdown.querySelector('[data-bs-toggle="dropdown"]');
                if (toggle && window.bootstrap) {
                    const inst = bootstrap.Dropdown.getOrCreateInstance(toggle);
                    inst.hide();
                }
            });
        });
    }

    const notifDropdown = document.querySelector('.notif-dropdown');
    const notifBell = document.getElementById('notifBell');

    if (notifDropdown && notifBell && window.bootstrap) {
        const ddInstance = bootstrap.Dropdown.getOrCreateInstance(notifBell, { autoClose: 'outside' });

        notifBell.addEventListener('click', function (e) {
            if (window.innerWidth < 1200) {
                return;
            }
            e.preventDefault();
            const target = notifBell.getAttribute('href');
            if (target && target !== '#') window.location.href = target;
        });
    }
});
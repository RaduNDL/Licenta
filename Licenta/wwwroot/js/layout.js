document.addEventListener("DOMContentLoaded", function () {
    const navbar = document.querySelector('.navbar-modern');

    function handleScroll() {
        if (window.scrollY > 10) {
            navbar.classList.add('scrolled');
        } else {
            navbar.classList.remove('scrolled');
        }
    }

    window.addEventListener('scroll', handleScroll);
    handleScroll();

    const currentPath = window.location.pathname.toLowerCase();
    const navLinks = document.querySelectorAll('.nav-link');
    const dropdownItems = document.querySelectorAll('.dropdown-item');

    function setActiveStatus(elements) {
        elements.forEach(link => {
            const href = link.getAttribute('href');
            if (!href || href === '#') return;

            const hrefPath = href.split('?')[0].toLowerCase();

            if (currentPath === hrefPath || (currentPath.startsWith(hrefPath) && hrefPath !== '/' && hrefPath.length > 3)) {
                link.classList.add('active');

                const parentDropdown = link.closest('.dropdown');
                if (parentDropdown) {
                    const toggle = parentDropdown.querySelector('.dropdown-toggle');
                    if (toggle) toggle.classList.add('active');
                }
            }
        });
    }

    setActiveStatus(navLinks);
    setActiveStatus(dropdownItems);
});
(function () {
    "use strict";

    function onReady(fn) {
        if (document.readyState !== "loading") fn();
        else document.addEventListener("DOMContentLoaded", fn);
    }

    onReady(function () {
        var navbar = document.querySelector(".app-navbar");
        var collapseEl = document.getElementById("navContent");

        if (navbar) {
            var applyScrollState = function () {
                if (window.scrollY > 8) navbar.classList.add("scrolled");
                else navbar.classList.remove("scrolled");
            };
            window.addEventListener("scroll", applyScrollState, { passive: true });
            applyScrollState();
        }

        var normalize = function (p) {
            if (!p) return "";
            try {
                var url = new URL(p, window.location.origin);
                var path = url.pathname.toLowerCase();
                if (path.length > 1 && path.endsWith("/")) path = path.slice(0, -1);
                return path;
            } catch (e) {
                return p.split("?")[0].toLowerCase().replace(/\/+$/, "");
            }
        };

        var currentPath = normalize(window.location.pathname);

        var bestMatchLength = 0;
        var bestMatchEl = null;

        var allLinks = document.querySelectorAll(".main-menu .nav-link, .main-menu .dropdown-item");
        allLinks.forEach(function (link) {
            var href = link.getAttribute("href");
            if (!href || href === "#") return;
            var hrefPath = normalize(href);
            if (!hrefPath || hrefPath === "/") return;

            var isMatch = currentPath === hrefPath ||
                (currentPath.startsWith(hrefPath + "/") && hrefPath.length > 1);

            if (isMatch && hrefPath.length > bestMatchLength) {
                bestMatchLength = hrefPath.length;
                bestMatchEl = link;
            }
        });

        if (bestMatchEl) {
            bestMatchEl.classList.add("active");

            var parentDropdown = bestMatchEl.closest(".dropdown");
            if (parentDropdown) {
                var toggle = parentDropdown.querySelector(".nav-link.dropdown-toggle");
                if (toggle) toggle.classList.add("active");
            }
        }

        if (collapseEl && window.bootstrap) {
            var leafLinks = collapseEl.querySelectorAll(".nav-link:not(.dropdown-toggle), .dropdown-item");
            leafLinks.forEach(function (link) {
                link.addEventListener("click", function () {
                    if (window.innerWidth < 1200 && collapseEl.classList.contains("show")) {
                        var inst = window.bootstrap.Collapse.getInstance(collapseEl) ||
                            new window.bootstrap.Collapse(collapseEl, { toggle: false });
                        inst.hide();
                    }
                });
            });
        }

        document.addEventListener("keydown", function (e) {
            if (e.key === "Escape") {
                document.querySelectorAll(".dropdown-menu.show").forEach(function (menu) {
                    var toggle = menu.previousElementSibling;
                    if (toggle && window.bootstrap) {
                        var dd = window.bootstrap.Dropdown.getInstance(toggle);
                        if (dd) dd.hide();
                    }
                });
            }
        });
    });
})();
(function () {
    "use strict";

    function initEscalate() {
        var toggleBtn = document.getElementById("escalateToggleBtn");
        var dropdownMenu = document.getElementById("escalateMenu");
        var container = toggleBtn ? toggleBtn.closest(".escalate-dropdown-container") : null;

        if (!toggleBtn || !dropdownMenu) return;

        if (toggleBtn.dataset.init === "1") return;
        toggleBtn.dataset.init = "1";

        function closeMenu() {
            dropdownMenu.classList.remove("show");
            if (container) container.classList.remove("open");
        }

        function toggleMenu() {
            if (toggleBtn.disabled || toggleBtn.hasAttribute("disabled")) return;
            dropdownMenu.classList.toggle("show");
            if (container) container.classList.toggle("open");
        }

        toggleBtn.addEventListener("click", function (e) {
            e.preventDefault();
            e.stopPropagation();
            toggleMenu();
        });

        document.addEventListener("click", function (e) {
            if (!dropdownMenu.contains(e.target) && !toggleBtn.contains(e.target)) {
                closeMenu();
            }
        });

        document.addEventListener("keydown", function (e) {
            if (e.key === "Escape") {
                closeMenu();
            }
        });
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initEscalate);
    } else {
        initEscalate();
    }
})();
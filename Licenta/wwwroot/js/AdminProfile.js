/* ═══════════════════════════════════════════════
   AdminProfile.js
   E:\Facultate\Licenta\Licenta\Licenta\wwwroot\js\AdminProfile.js
   ═══════════════════════════════════════════════ */

(function () {
    'use strict';

    // ── Preview poza selectata ─────────────────────────────────────
    function previewPhoto(input) {
        var file = input.files[0];
        if (!file) return;

        var preview = document.getElementById('photoPreview');
        var uploadBtn = document.getElementById('uploadBtn');

        var reader = new FileReader();
        reader.onload = function (e) {
            preview.src = e.target.result;
            preview.style.display = 'block';
            uploadBtn.style.display = 'block';
        };
        reader.readAsDataURL(file);
    }

    // ── Drag & drop ────────────────────────────────────────────────
    function initDragDrop() {
        var zone = document.getElementById('uploadZone');
        if (!zone) return;

        zone.addEventListener('dragover', function (e) {
            e.preventDefault();
            zone.classList.add('drag-over');
        });

        zone.addEventListener('dragleave', function () {
            zone.classList.remove('drag-over');
        });

        zone.addEventListener('drop', function (e) {
            e.preventDefault();
            zone.classList.remove('drag-over');

            var files = e.dataTransfer.files;
            if (files.length === 0) return;

            var input = document.getElementById('photoFileInput');
            // Inlocuieste fisierele din input cu cele dropped
            var dt = new DataTransfer();
            dt.items.add(files[0]);
            input.files = dt.files;

            previewPhoto(input);
        });
    }

    // ── Click pe zona deschide file picker ─────────────────────────
    function initUploadZoneClick() {
        var zone = document.getElementById('uploadZone');
        var input = document.getElementById('photoFileInput');
        if (!zone || !input) return;

        zone.addEventListener('click', function (e) {
            // Evita dublu-click daca userul a dat click exact pe input
            if (e.target === input) return;
            input.click();
        });

        input.addEventListener('change', function () {
            previewPhoto(this);
        });
    }

    function initAutoDismissAlerts() {
        var alerts = document.querySelectorAll('.profile-alert');
        alerts.forEach(function (el) {
            setTimeout(function () {
                el.style.transition = 'opacity 0.4s ease';
                el.style.opacity = '0';
                setTimeout(function () {
                    el.remove();
                }, 400);
            }, 4000);
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        initDragDrop();
        initUploadZoneClick();
        initAutoDismissAlerts();
    });

    window.previewPhoto = previewPhoto;
})();
(function () {
    'use strict';

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
            var dt = new DataTransfer();
            dt.items.add(files[0]);
            input.files = dt.files;

            previewPhoto(input);
        });
    }

    function initUploadZoneClick() {
        var zone = document.getElementById('uploadZone');
        var input = document.getElementById('photoFileInput');
        if (!zone || !input) return;

        zone.addEventListener('click', function (e) {
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
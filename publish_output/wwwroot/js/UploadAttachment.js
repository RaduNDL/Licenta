document.addEventListener('DOMContentLoaded', function () {
    const dropZone = document.querySelector('.file-drop-zone');
    const fileInput = document.querySelector('.file-input');
    const fileNameDisplay = document.querySelector('.selected-file-name');

    if (!dropZone || !fileInput) return;

    ['dragenter', 'dragover', 'dragleave', 'drop'].forEach(eventName => {
        dropZone.addEventListener(eventName, (e) => {
            e.preventDefault();
            e.stopPropagation();
        }, false);
    });

    ['dragenter', 'dragover'].forEach(evt => {
        dropZone.addEventListener(evt, () => dropZone.classList.add('dragover'), false);
    });

    ['dragleave', 'drop'].forEach(evt => {
        dropZone.addEventListener(evt, () => dropZone.classList.remove('dragover'), false);
    });

    dropZone.addEventListener('drop', (e) => {
        const dt = e.dataTransfer;
        fileInput.files = dt.files;
        handleFiles();
    }, false);

    fileInput.addEventListener('change', handleFiles, false);

    function handleFiles() {
        if (fileInput.files.length > 0) {
            const file = fileInput.files[0];
            fileNameDisplay.innerHTML = `<i class="bi bi-file-check"></i> ${file.name} <span class="text-muted">(${(file.size / 1024 / 1024).toFixed(2)} MB)</span>`;
            fileNameDisplay.style.color = '#059669';
        }
    }
});
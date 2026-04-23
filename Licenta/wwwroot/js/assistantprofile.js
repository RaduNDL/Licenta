document.addEventListener('DOMContentLoaded', function () {
    const fileInput = document.getElementById('fileInput');
    const fileNameDisplay = document.getElementById('fileName');
    const uploadBtn = document.getElementById('uploadBtn');

    if (fileInput) {
        fileInput.addEventListener('change', function () {
            if (this.files && this.files.length > 0) {
                fileNameDisplay.textContent = this.files[0].name;
                if (uploadBtn) {
                    uploadBtn.style.display = 'block';
                }
            } else {
                fileNameDisplay.textContent = '';
                if (uploadBtn) {
                    uploadBtn.style.display = 'none';
                }
            }
        });
    }
});
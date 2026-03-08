document.addEventListener('DOMContentLoaded', function () {

    const tabKey = 'medflow_patient_active_tab';
    const tabs = document.querySelectorAll('button[data-bs-toggle="pill"]');

    const activeTabId = localStorage.getItem(tabKey);
    if (activeTabId) {
        const tabTrigger = document.querySelector(`button[data-bs-target="${activeTabId}"]`);
        if (tabTrigger) {
            const tab = new bootstrap.Tab(tabTrigger);
            tab.show();
        }
    }

    tabs.forEach(tab => {
        tab.addEventListener('shown.bs.tab', function (event) {
            localStorage.setItem(tabKey, event.target.getAttribute('data-bs-target'));
        });
    });

    const fileInput = document.getElementById('fileInput');
    const fileNameDisplay = document.getElementById('fileName');
    const uploadBtn = document.getElementById('uploadBtn');
    const avatarImg = document.querySelector('.profile-avatar img');
    const avatarFallback = document.querySelector('.profile-avatar .avatar-fallback');

    if (fileInput) {
        fileInput.addEventListener('change', function (e) {
            const file = e.target.files[0];
            if (file) {
                fileNameDisplay.textContent = file.name;
                uploadBtn.style.display = 'block';

                const reader = new FileReader();
                reader.onload = function (e) {
                    if (avatarImg) {
                        avatarImg.src = e.target.result;
                    } else if (avatarFallback) {
                        avatarFallback.style.display = 'none';
                        let tempImg = document.createElement('img');
                        tempImg.src = e.target.result;
                        document.querySelector('.profile-avatar').appendChild(tempImg);
                    }
                }
                reader.readAsDataURL(file);
            }
        });
    }

    const alerts = document.querySelectorAll('.alert');
    if (alerts.length > 0) {
        setTimeout(() => {
            alerts.forEach(alert => {
                alert.style.transition = "opacity 0.5s ease";
                alert.style.opacity = "0";
                setTimeout(() => alert.remove(), 500);
            });
        }, 5000);
    }
});
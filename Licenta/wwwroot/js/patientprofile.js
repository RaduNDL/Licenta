document.addEventListener('DOMContentLoaded', () => {
 
    const tabKey = 'medflow_patient_active_tab';
    const tabButtons = document.querySelectorAll('button[data-bs-toggle="pill"]');

    try {
        const activeTabId = localStorage.getItem(tabKey);
        if (activeTabId) {
            const tabTrigger = document.querySelector(`button[data-bs-target="${activeTabId}"]`);
            if (tabTrigger && window.bootstrap?.Tab) {
                new bootstrap.Tab(tabTrigger).show();
            }
        }
    } catch {
      
    }

    tabButtons.forEach(btn => {
        btn.addEventListener('shown.bs.tab', (event) => {
            try {
                const target = event.target?.getAttribute('data-bs-target');
                if (target) localStorage.setItem(tabKey, target);
            } catch {
              
            }
        });
    });

    const fileInput = document.getElementById('fileInput');
    const fileNameDisplay = document.getElementById('fileName');
    const uploadBtn = document.getElementById('uploadBtn');

    const avatarContainer = document.querySelector('.profile-avatar');
    if (!avatarContainer) return;

    const getOrCreateAvatarImg = () => {
        let img = avatarContainer.querySelector('img');
        const fallback = avatarContainer.querySelector('.avatar-fallback');

        if (!img) {
            img = document.createElement('img');
            
            img.style.width = '100%';
            img.style.height = '100%';
            img.style.objectFit = 'cover';
            img.style.display = 'block';

            avatarContainer.appendChild(img);
        }

        if (fallback) fallback.style.display = 'none';
        return img;
    };

    const allowedMimeTypes = new Set([
        'image/png',
        'image/jpeg',
        'image/webp'
    ]);

    if (fileInput) {
        fileInput.addEventListener('change', (e) => {
            const file = e.target.files && e.target.files[0];
            if (!file) return;

            if (fileNameDisplay) fileNameDisplay.textContent = file.name;
            if (uploadBtn) uploadBtn.style.display = 'block';

            if (!allowedMimeTypes.has(file.type)) {
              
                if (fileNameDisplay) fileNameDisplay.textContent = '';
                if (uploadBtn) uploadBtn.style.display = 'none';
                fileInput.value = '';

                alert('Format invalid. Folosește PNG / JPG / WEBP.');
                return;
            }

            const reader = new FileReader();
            reader.onload = (ev) => {
                const img = getOrCreateAvatarImg();
                img.src = ev.target.result;
            };
            reader.readAsDataURL(file);
        });
    }

    
    const alerts = document.querySelectorAll('.alert');
    if (alerts.length > 0) {
        setTimeout(() => {
            alerts.forEach((alertEl) => {
                alertEl.style.transition = 'opacity 0.5s ease';
                alertEl.style.opacity = '0';
                setTimeout(() => alertEl.remove(), 500);
            });
        }, 5000);
    }
});
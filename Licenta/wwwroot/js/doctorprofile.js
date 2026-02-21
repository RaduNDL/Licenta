document.addEventListener('DOMContentLoaded', () => {
    const photoInput = document.querySelector('input[type="file"]');
    const avatarContainer = document.querySelector('.profile-avatar');

    if (photoInput && avatarContainer) {
        photoInput.addEventListener('change', (event) => {
            const file = event.target.files[0];
            if (file) {
                const reader = new FileReader();
                reader.onload = (e) => {
                    const existingImg = avatarContainer.querySelector('img');
                    if (existingImg) {
                        existingImg.src = e.target.result;
                    } else {
                        const fallback = avatarContainer.querySelector('.avatar-fallback');
                        if (fallback) fallback.remove();

                        const newImg = document.createElement('img');
                        newImg.src = e.target.result;
                        newImg.alt = "Profile preview";
                        avatarContainer.appendChild(newImg);
                    }
                };
                reader.readAsDataURL(file);
            }
        });
    }

    const specialtyInput = document.getElementById('Input_Specialty');
    const languagesInput = document.getElementById('Input_Languages');

    const previewCards = document.querySelectorAll('.card');
    const previewCard = previewCards[previewCards.length - 1];

    if (previewCard) {
        const specialtyBadge = previewCard.querySelector('.bi-award')?.parentElement;
        const languagesBadge = previewCard.querySelector('.bi-translate')?.parentElement;

        if (specialtyInput && specialtyBadge) {
            specialtyInput.addEventListener('input', (e) => {
                const val = e.target.value.trim();
                const iconHtml = '<i class="bi bi-award me-1"></i>';
                specialtyBadge.innerHTML = iconHtml + (val || 'Specialty not set');
            });
        }

        if (languagesInput && languagesBadge) {
            languagesInput.addEventListener('input', (e) => {
                const val = e.target.value.trim();
                const iconHtml = '<i class="bi bi-translate me-1"></i>';
                languagesBadge.innerHTML = iconHtml + (val || 'Languages not set');
            });
        }
    }

    const alerts = document.querySelectorAll('.alert');
    alerts.forEach(alert => {
        setTimeout(() => {
            alert.style.transition = "opacity 0.5s ease";
            alert.style.opacity = "0";
            setTimeout(() => alert.remove(), 500);
        }, 4000);
    });
});
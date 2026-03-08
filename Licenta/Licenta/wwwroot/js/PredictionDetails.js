document.addEventListener('DOMContentLoaded', () => {
    const resultVal = document.querySelector('.stat-value[data-type="result"]');
    if (resultVal) {
        const txt = resultVal.textContent.toLowerCase();
        if (txt.includes('melanoma') || txt.includes('malignant')) {
            resultVal.style.color = '#dc2626';
        } else if (txt.includes('nevus') || txt.includes('benign')) {
            resultVal.style.color = '#16a34a';
        }
    }
});
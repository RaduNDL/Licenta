document.addEventListener('DOMContentLoaded', () => {
    const meters = document.querySelectorAll('.meter-fill');
    meters.forEach(m => {
        const width = m.getAttribute('data-width');
        setTimeout(() => { m.style.width = width; }, 100);
    });

    const resultCells = document.querySelectorAll('td[data-type="result"]');
    resultCells.forEach(cell => {
        const text = cell.textContent.toLowerCase();
        if (text.includes('melanoma') || text.includes('malignant')) {
            cell.style.color = '#dc2626';
            cell.style.fontWeight = 'bold';
        } else if (text.includes('nevus') || text.includes('benign')) {
            cell.style.color = '#16a34a';
            cell.style.fontWeight = 'bold';
        }
    });
});
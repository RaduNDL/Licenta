document.addEventListener('DOMContentLoaded', function () {


    const searchInput = document.getElementById('appointmentSearch');
    const cards = document.querySelectorAll('.appt-card');

    if (searchInput) {
        searchInput.addEventListener('keyup', (e) => {
            const term = e.target.value.toLowerCase();
            cards.forEach(card => {
                const doctor = card.dataset.doctor.toLowerCase();
                const status = card.dataset.status.toLowerCase();

                if (doctor.includes(term) || status.includes(term)) {
                    card.style.display = 'flex';
                } else {
                    card.style.display = 'none';
                }
            });
        });
    }


    const noteElements = document.querySelectorAll('.raw-notes');

    noteElements.forEach(el => {
        const rawText = el.textContent.trim();

        if (rawText.includes('|')) {
            const parts = rawText.split('|');
            let html = '';

            parts.forEach(part => {
                if (part.toLowerCase().includes('suggested:')) {
                    const datePart = part.replace('Suggested:', '').trim();
                    html += `<div class="note-suggestion">
                                <i class="fa-regular fa-calendar-check"></i>
                                <span>Suggested Date: <strong>${datePart}</strong></span>
                             </div>`;
                } else {
                    
                    const cleanText = part.replace(/_/g, ' ');
                    html += `<div class="note-text">${cleanText}</div>`;
                }
            });

            el.innerHTML = html;
        } else if (rawText === "-") {
            el.innerHTML = '<span class="text-muted">No feedback provided yet.</span>';
        }
    });
});
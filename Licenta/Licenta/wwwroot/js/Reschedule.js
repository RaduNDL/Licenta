document.addEventListener("DOMContentLoaded", function () {
    const datesContainer = document.getElementById("datesContainer");
    const timesContainer = document.getElementById("timesContainer");
    const step3 = document.getElementById("step3");
    const hiddenSlotKey = document.getElementById("hiddenSlotKey");
    const finalSelectionText = document.getElementById("finalSelectionText");

    let selectedDateKey = null;

    if (!doctorData || !doctorData.DaysAndSlots || Object.keys(doctorData.DaysAndSlots).length === 0) {
        datesContainer.innerHTML = '<div class="text-muted small">No availability found for this doctor.</div>';
        return;
    }

    renderDates();

    function renderDates() {
        datesContainer.innerHTML = "";
        const dates = Object.keys(doctorData.DaysAndSlots);

        dates.forEach(dateStr => {
            const dateObj = new Date(dateStr);
            const dayName = dateObj.toLocaleDateString('en-US', { weekday: 'short' });
            const dayNum = dateObj.getDate();
            const monthName = dateObj.toLocaleDateString('en-US', { month: 'short' });

            const card = document.createElement("div");
            card.className = "date-card";
            card.dataset.date = dateStr;
            card.innerHTML = `
                <span class="day-name">${dayName}</span>
                <span class="day-number">${dayNum}</span>
                <span class="month-name">${monthName}</span>
            `;

            card.addEventListener("click", function () {
                document.querySelectorAll(".date-card").forEach(c => c.classList.remove("active"));
                this.classList.add("active");
                selectedDateKey = dateStr;
                renderTimes();
            });

            datesContainer.appendChild(card);
        });
    }

    function renderTimes() {
        timesContainer.innerHTML = "";
        if (!selectedDateKey) return;

        const slots = doctorData.DaysAndSlots[selectedDateKey] || [];

        if (slots.length === 0) {
            timesContainer.innerHTML = '<span class="text-muted small">No times available for this day.</span>';
            return;
        }

        slots.forEach(slot => {
            const pill = document.createElement("div");
            pill.className = "time-slot";
            pill.textContent = slot.TimeDisplay;
            pill.dataset.key = slot.Key;
            pill.dataset.iso = slot.LocalIso;

            pill.addEventListener("click", function () {
                document.querySelectorAll(".time-slot").forEach(p => p.classList.remove("active"));
                this.classList.add("active");

                hiddenSlotKey.value = this.dataset.key;

                const dt = new Date(this.dataset.iso);
                const displayStr = dt.toLocaleDateString('en-US', { weekday: 'short', day: 'numeric', month: 'short' }) + ' at ' + dt.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit', hour12: false });

                finalSelectionText.textContent = displayStr;
                step3.classList.remove("disabled-step");
            });

            timesContainer.appendChild(pill);
        });
    }
});
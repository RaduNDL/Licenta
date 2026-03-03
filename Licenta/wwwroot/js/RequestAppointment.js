document.addEventListener("DOMContentLoaded", function () {
    if (typeof doctorsData !== 'undefined') {
        initWizard();
    }
    initFormHandlers();
    autoResizeTextarea();
});

let selectedDate = null;

function initWizard() {
    const doctorsContainer = document.getElementById('doctorsContainer');

    if (doctorsData.length === 0) {
        doctorsContainer.innerHTML = '<div class="alert alert-warning border-0">No doctors available.</div>';
        return;
    }

    doctorsData.forEach(doc => {
        const hasSlots = Object.keys(doc.DaysAndSlots).length > 0;
        const statusText = hasSlots
            ? '<span class="text-success fw-bold" style="font-size:0.75rem;">Available slots</span>'
            : '<span class="text-danger fw-bold" style="font-size:0.75rem;">No availability</span>';

        const card = document.createElement('div');
        card.className = 'doctor-card';
        card.innerHTML = `
<img src="${doc.ProfileImagePath}" class="doc-img" onerror="this.src='/images/default.jpg'" alt="Dr. ${doc.Name}">            <div class="doc-info w-100">
                <h6>Dr. ${doc.Name}</h6>
                <p>${doc.Specialty}</p>
                ${statusText}
            </div>
        `;
        card.addEventListener('click', () => selectDoctor(doc, card));
        doctorsContainer.appendChild(card);
    });
}
function selectDoctor(doc, cardElement) {
    selectedDate = null;
    document.getElementById('hiddenSlotKey').value = '';

    document.querySelectorAll('.doctor-card').forEach(c => c.classList.remove('selected'));
    cardElement.classList.add('selected');

    document.getElementById('step2').classList.remove('disabled-step');
    document.getElementById('step3').classList.add('disabled-step');
    document.getElementById('selectedDoctorName').innerHTML = `<i class="bi bi-person-badge text-primary me-2"></i> Dr. ${doc.Name} - ${doc.Specialty}`;

    renderDates(doc);
}

function renderDates(doc) {
    const datesContainer = document.getElementById('datesContainer');
    const timesContainer = document.getElementById('timesContainer');
    datesContainer.innerHTML = '';
    timesContainer.innerHTML = '';

    const availableDates = Object.keys(doc.DaysAndSlots);

    if (availableDates.length === 0) {
        datesContainer.innerHTML = '<p class="text-danger fw-bold small">This doctor has no available schedule or is fully booked for the next 14 days.</p>';
        return;
    }

    availableDates.forEach(dateStr => {
        const fDate = formatDateText(dateStr);
        const pill = document.createElement('div');
        pill.className = 'date-pill';
        pill.innerHTML = `
            <span class="day-name">${fDate.dayName}</span>
            <span class="day-num">${fDate.dayNum}</span>
            <span class="day-name">${fDate.month}</span>
        `;

        pill.addEventListener('click', () => selectDate(dateStr, doc, pill));
        datesContainer.appendChild(pill);
    });
}

function selectDate(dateStr, doc, pillElement) {
    selectedDate = dateStr;
    document.getElementById('hiddenSlotKey').value = '';

    document.querySelectorAll('.date-pill').forEach(p => p.classList.remove('selected'));
    pillElement.classList.add('selected');

    document.getElementById('step3').classList.add('disabled-step');
    document.getElementById('finalSelectionText').innerText = "No time selected";

    renderTimes(doc.DaysAndSlots[dateStr]);
}

function renderTimes(slotsArray) {
    const timesContainer = document.getElementById('timesContainer');
    timesContainer.innerHTML = '';

    slotsArray.forEach(slot => {
        const pill = document.createElement('div');
        pill.className = 'time-pill';
        pill.innerText = slot.TimeDisplay;

        pill.addEventListener('click', () => selectTime(slot, pill));
        timesContainer.appendChild(pill);
    });
}

function selectTime(slot, pillElement) {
    document.querySelectorAll('.time-pill').forEach(p => p.classList.remove('selected'));
    pillElement.classList.add('selected');

    document.getElementById('hiddenSlotKey').value = slot.Key;
    document.getElementById('step3').classList.remove('disabled-step');

    const fDate = formatDateText(selectedDate);
    document.getElementById('finalSelectionText').innerHTML =
        `${fDate.dayName}, ${fDate.dayNum} ${fDate.month} at <b class="text-primary">${slot.TimeDisplay}</b>`;
}

function formatDateText(dateStr) {
    const date = new Date(dateStr);
    const days = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
    const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
    return { dayName: days[date.getDay()], dayNum: date.getDate(), month: months[date.getMonth()] };
}

function initFormHandlers() {
    const form = document.querySelector('form');
    const submitBtn = document.querySelector('button[type="submit"]');
    const hiddenInput = document.getElementById('hiddenSlotKey');

    if (!form || !submitBtn) return;

    form.addEventListener('submit', function (e) {
        if (!hiddenInput.value) {
            e.preventDefault();
            alert("Please select a doctor, a date, and an available time.");
            return;
        }

        if (!form.checkValidity()) return;

        submitBtn.classList.add('btn-loading');
        submitBtn.disabled = true;
        submitBtn.innerHTML = '<span class="spinner"></span> Processing...';
    });
}

function autoResizeTextarea() {
    const textarea = document.querySelector('textarea');
    if (!textarea) return;

    textarea.addEventListener('input', function () {
        this.style.height = 'auto';
        this.style.height = (this.scrollHeight + 2) + 'px';
    });
}
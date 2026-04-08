document.addEventListener("DOMContentLoaded", function () {
    if (typeof doctorsData !== "undefined") {
        initWizard();
    }

    initFormHandlers();
    initExitGuard();
    autoResizeTextarea();
});

let selectedDate = null;
let isDirty = false;
let isSubmitting = false;
let pendingNavigationUrl = null;

function initWizard() {
    const doctorsContainer = document.getElementById("doctorsContainer");

    if (!doctorsContainer) return;

    if (!Array.isArray(doctorsData) || doctorsData.length === 0) {
        doctorsContainer.innerHTML = '<div class="alert alert-warning border-0">No doctors available.</div>';
        return;
    }

    doctorsData.forEach(doc => {
        const hasSlots = doc.DaysAndSlots && Object.keys(doc.DaysAndSlots).length > 0;
        const statusText = hasSlots
            ? '<span class="text-success fw-bold" style="font-size:0.75rem;">Available slots</span>'
            : '<span class="text-danger fw-bold" style="font-size:0.75rem;">No availability</span>';

        const card = document.createElement("div");
        card.className = "doctor-card";
        card.innerHTML = `
            <img src="${doc.ProfileImagePath}" class="doc-img" onerror="this.src='/images/default.jpg'" alt="Dr. ${escapeHtml(doc.Name)}">
            <div class="doc-info w-100">
                <h6>Dr. ${escapeHtml(doc.Name)}</h6>
                <p>${escapeHtml(doc.Specialty)}</p>
                ${statusText}
            </div>
        `;

        card.addEventListener("click", () => selectDoctor(doc, card));
        doctorsContainer.appendChild(card);
    });
}

function selectDoctor(doc, cardElement) {
    selectedDate = null;
    setDirty(true);

    const hiddenSlotKey = document.getElementById("hiddenSlotKey");
    const step2 = document.getElementById("step2");
    const step3 = document.getElementById("step3");
    const selectedDoctorName = document.getElementById("selectedDoctorName");
    const finalSelectionText = document.getElementById("finalSelectionText");

    if (hiddenSlotKey) hiddenSlotKey.value = "";

    document.querySelectorAll(".doctor-card").forEach(c => c.classList.remove("selected"));
    cardElement.classList.add("selected");

    if (step2) step2.classList.remove("disabled-step");
    if (step3) step3.classList.add("disabled-step");

    if (selectedDoctorName) {
        selectedDoctorName.innerHTML = `<i class="bi bi-person-badge text-primary me-2"></i> Dr. ${escapeHtml(doc.Name)} - ${escapeHtml(doc.Specialty)}`;
    }

    if (finalSelectionText) {
        finalSelectionText.innerText = "No time selected";
    }

    renderDates(doc);
}

function renderDates(doc) {
    const datesContainer = document.getElementById("datesContainer");
    const timesContainer = document.getElementById("timesContainer");

    if (!datesContainer || !timesContainer) return;

    datesContainer.innerHTML = "";
    timesContainer.innerHTML = "";

    const availableDates = doc.DaysAndSlots ? Object.keys(doc.DaysAndSlots) : [];

    if (availableDates.length === 0) {
        datesContainer.innerHTML = '<p class="text-danger fw-bold small">This doctor has no available schedule or is fully booked for the next 14 days.</p>';
        return;
    }

    availableDates.forEach(dateStr => {
        const fDate = formatDateText(dateStr);
        const pill = document.createElement("div");
        pill.className = "date-pill";
        pill.innerHTML = `
            <span class="day-name">${fDate.dayName}</span>
            <span class="day-num">${fDate.dayNum}</span>
            <span class="day-name">${fDate.month}</span>
        `;

        pill.addEventListener("click", () => selectDate(dateStr, doc, pill));
        datesContainer.appendChild(pill);
    });
}

function selectDate(dateStr, doc, pillElement) {
    selectedDate = dateStr;
    setDirty(true);

    const hiddenSlotKey = document.getElementById("hiddenSlotKey");
    const step3 = document.getElementById("step3");
    const finalSelectionText = document.getElementById("finalSelectionText");

    if (hiddenSlotKey) hiddenSlotKey.value = "";

    document.querySelectorAll(".date-pill").forEach(p => p.classList.remove("selected"));
    pillElement.classList.add("selected");

    if (step3) step3.classList.add("disabled-step");
    if (finalSelectionText) finalSelectionText.innerText = "No time selected";

    renderTimes(doc.DaysAndSlots[dateStr] || []);
}

function renderTimes(slotsArray) {
    const timesContainer = document.getElementById("timesContainer");
    if (!timesContainer) return;

    timesContainer.innerHTML = "";

    slotsArray.forEach(slot => {
        const pill = document.createElement("div");
        pill.className = "time-pill";
        pill.innerText = slot.TimeDisplay;

        pill.addEventListener("click", () => selectTime(slot, pill));
        timesContainer.appendChild(pill);
    });
}

function selectTime(slot, pillElement) {
    setDirty(true);

    const hiddenSlotKey = document.getElementById("hiddenSlotKey");
    const step3 = document.getElementById("step3");
    const finalSelectionText = document.getElementById("finalSelectionText");

    document.querySelectorAll(".time-pill").forEach(p => p.classList.remove("selected"));
    pillElement.classList.add("selected");

    if (hiddenSlotKey) hiddenSlotKey.value = slot.Key;
    if (step3) step3.classList.remove("disabled-step");

    const fDate = formatDateText(selectedDate);
    if (finalSelectionText) {
        finalSelectionText.innerHTML = `${fDate.dayName}, ${fDate.dayNum} ${fDate.month} at <b class="text-primary">${escapeHtml(slot.TimeDisplay)}</b>`;
    }
}

function formatDateText(dateStr) {
    const date = new Date(dateStr + "T00:00:00");
    const days = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];
    const months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    return {
        dayName: days[date.getDay()],
        dayNum: date.getDate(),
        month: months[date.getMonth()]
    };
}

function initFormHandlers() {
    const form = document.getElementById("bookingForm");
    const submitBtn = document.getElementById("submitAppointmentBtn");
    const hiddenInput = document.getElementById("hiddenSlotKey");
    const textarea = document.querySelector('textarea[name="Input.Reason"]');

    if (!form || !submitBtn || !hiddenInput) return;

    if (textarea) {
        textarea.addEventListener("input", function () {
            setDirty(this.value.trim().length > 0 || !!hiddenInput.value);
        });
    }

    form.addEventListener("submit", function (e) {
        if (!hiddenInput.value) {
            e.preventDefault();
            alert("Please select a doctor, a date, and an available time.");
            return;
        }

        if (!form.checkValidity()) {
            return;
        }

        isSubmitting = true;
        isDirty = false;

        submitBtn.classList.add("btn-loading");
        submitBtn.disabled = true;

        const label = submitBtn.querySelector(".btn-label");
        if (label) {
            label.textContent = "Processing...";
        }
    });
}

function autoResizeTextarea() {
    const textarea = document.querySelector('textarea[name="Input.Reason"]');
    if (!textarea) return;

    const resize = function () {
        this.style.height = "auto";
        this.style.height = (this.scrollHeight + 2) + "px";
    };

    textarea.addEventListener("input", resize);
    resize.call(textarea);
}

function initExitGuard() {
    const modal = document.getElementById("exitConfirmModal");
    const stayBtn = document.getElementById("stayOnPageBtn");
    const leaveBtn = document.getElementById("leavePageBtn");

    document.querySelectorAll(".exit-guard-link").forEach(link => {
        link.addEventListener("click", function (e) {
            if (!shouldWarnBeforeExit()) return;

            e.preventDefault();
            pendingNavigationUrl = this.href;
            openExitModal();
        });
    });

    document.addEventListener("click", function (e) {
        const anchor = e.target.closest("a");
        if (!anchor) return;

        if (anchor.classList.contains("exit-guard-link")) return;
        if (anchor.getAttribute("href") === "#" || anchor.hasAttribute("download")) return;
        if (anchor.target === "_blank") return;
        if (anchor.hasAttribute("data-bs-toggle")) return;

        const href = anchor.getAttribute("href");
        if (!href) return;
        if (href.startsWith("javascript:")) return;
        if (href.startsWith("#")) return;

        if (!shouldWarnBeforeExit()) return;

        e.preventDefault();
        pendingNavigationUrl = anchor.href;
        openExitModal();
    });

    if (stayBtn) {
        stayBtn.addEventListener("click", function () {
            pendingNavigationUrl = null;
            closeExitModal();
        });
    }

    if (leaveBtn) {
        leaveBtn.addEventListener("click", function () {
            const targetUrl = pendingNavigationUrl;
            isDirty = false;
            isSubmitting = false;
            closeExitModal();

            if (targetUrl) {
                window.location.href = targetUrl;
            }
        });
    }

    if (modal) {
        modal.addEventListener("click", function (e) {
            if (e.target === modal) {
                pendingNavigationUrl = null;
                closeExitModal();
            }
        });
    }

    document.addEventListener("keydown", function (e) {
        if (e.key === "Escape") {
            closeExitModal();
        }
    });

    window.addEventListener("beforeunload", function (e) {
        if (!shouldWarnBeforeExit()) return;

        e.preventDefault();
        e.returnValue = "";
    });
}

function shouldWarnBeforeExit() {
    return !isSubmitting && isDirty;
}

function openExitModal() {
    const modal = document.getElementById("exitConfirmModal");
    if (!modal) return;

    modal.classList.add("show");
    modal.setAttribute("aria-hidden", "false");
    document.body.classList.add("modal-open-request");
}

function closeExitModal() {
    const modal = document.getElementById("exitConfirmModal");
    if (!modal) return;

    modal.classList.remove("show");
    modal.setAttribute("aria-hidden", "true");
    document.body.classList.remove("modal-open-request");
}

function setDirty(value) {
    if (isSubmitting) return;
    isDirty = value;
}

function escapeHtml(value) {
    if (value === null || value === undefined) return "";
    return String(value)
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#039;");
}
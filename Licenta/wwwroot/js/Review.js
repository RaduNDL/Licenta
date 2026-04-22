document.addEventListener("DOMContentLoaded", function () {
    const reviewModalEl = document.getElementById("reviewModal");
    const deleteReviewModalEl = document.getElementById("deleteReviewModal");

    const reviewModal = reviewModalEl ? new bootstrap.Modal(reviewModalEl, { backdrop: true, keyboard: true, focus: true }) : null;
    const deleteReviewModal = deleteReviewModalEl ? new bootstrap.Modal(deleteReviewModalEl) : null;

    const form = document.getElementById("reviewForm");
    const titleSpan = document.getElementById("reviewModalTitle")?.querySelector('[data-slot="title"]');
    const submitSpan = document.getElementById("reviewSubmitBtn")?.querySelector('[data-slot="submit"]');

    const inputId = document.getElementById("Input_Id");
    const targetApp = document.getElementById("target-app");
    const targetDoctor = document.getElementById("target-doctor");
    const doctorSelectWrap = document.getElementById("doctorSelectWrap");
    const doctorSelect = document.getElementById("Input_DoctorId");
    const inputTitle = document.getElementById("Input_Title");
    const inputComment = document.getElementById("Input_Comment");
    const charCount = document.getElementById("charCount");

    function setFormHandler(handler) {
        if (!form) return;
        form.setAttribute("action", `?handler=${handler}`);
    }

    function updateChoiceCards() {
        document.querySelectorAll(".choice-card").forEach(card => {
            const input = card.querySelector('input[type="radio"]');
            card.classList.toggle("is-selected", !!input?.checked);
        });
    }

    function toggleDoctorSelect() {
        if (!doctorSelectWrap || !doctorSelect) return;

        const isDoctor = !!targetDoctor?.checked;
        doctorSelectWrap.hidden = !isDoctor;
        doctorSelect.disabled = !isDoctor;

        if (isDoctor) doctorSelect.setAttribute("required", "required");
        else {
            doctorSelect.removeAttribute("required");
            doctorSelect.value = "";
        }

        updateChoiceCards();
    }

    function setRating(value) {
        const star = document.getElementById(`rating-${value}`);
        if (star) star.checked = true;
    }

    targetApp?.addEventListener("change", toggleDoctorSelect);
    targetDoctor?.addEventListener("change", toggleDoctorSelect);

    inputComment?.addEventListener("input", () => {
        if (charCount) charCount.textContent = String(inputComment.value.length);
    });

    document.querySelectorAll('[data-mode="create"]').forEach(btn => {
        btn.addEventListener("click", function () {
            form?.reset();
            setFormHandler("Create");

            if (inputId) inputId.value = "";
            if (titleSpan) titleSpan.textContent = "Write a Review";
            if (submitSpan) submitSpan.textContent = "Submit Feedback";

            const target = this.getAttribute("data-target");
            if (target === "Doctor") {
                if (targetDoctor) targetDoctor.checked = true;
                if (targetApp) targetApp.checked = false;
            } else {
                if (targetApp) targetApp.checked = true;
                if (targetDoctor) targetDoctor.checked = false;
            }

            toggleDoctorSelect();
            setRating(5);
            if (charCount) charCount.textContent = "0";
        });
    });

    document.querySelectorAll(".js-edit-review").forEach(btn => {
        btn.addEventListener("click", function () {
            form?.reset();
            setFormHandler("Edit");

            if (inputId) inputId.value = this.getAttribute("data-id") || "";
            if (titleSpan) titleSpan.textContent = "Update Feedback";
            if (submitSpan) submitSpan.textContent = "Save Changes";

            const target = this.getAttribute("data-target");
            if (target === "Doctor") {
                if (targetDoctor) targetDoctor.checked = true;
                if (targetApp) targetApp.checked = false;
                if (doctorSelect) doctorSelect.value = this.getAttribute("data-doctor-id") || "";
            } else {
                if (targetApp) targetApp.checked = true;
                if (targetDoctor) targetDoctor.checked = false;
            }

            toggleDoctorSelect();

            const rating = parseInt(this.getAttribute("data-rating") || "5", 10);
            setRating(rating);

            if (inputTitle) inputTitle.value = this.getAttribute("data-title") || "";
            if (inputComment) {
                inputComment.value = this.getAttribute("data-comment") || "";
                if (charCount) charCount.textContent = String(inputComment.value.length);
            }

            reviewModal?.show();
        });
    });

    document.querySelectorAll(".js-delete-review").forEach(btn => {
        btn.addEventListener("click", function () {
            const deleteInput = document.getElementById("deleteReviewId");
            if (deleteInput) deleteInput.value = this.getAttribute("data-id") || "";
            deleteReviewModal?.show();
        });
    });

    const searchInput = document.getElementById("reviewSearch");
    const ratingFilter = document.getElementById("reviewRatingFilter");

    function applyFilters() {
        if (!searchInput || !ratingFilter) return;

        const query = searchInput.value.toLowerCase();
        const rVal = ratingFilter.value;

        document.querySelectorAll(".review-card").forEach(item => {
            const text = (item.getAttribute("data-search") || "").toLowerCase();
            const rating = item.getAttribute("data-rating");
            const show = text.includes(query) && (rVal === "0" || rating === rVal);
            item.style.display = show ? "flex" : "none";
        });
    }

    searchInput?.addEventListener("input", applyFilters);
    ratingFilter?.addEventListener("change", applyFilters);

    reviewModalEl?.addEventListener("shown.bs.modal", function () {
        toggleDoctorSelect();
        setTimeout(() => inputComment?.focus(), 50);
    });

    toggleDoctorSelect();
});
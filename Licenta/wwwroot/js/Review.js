document.addEventListener("DOMContentLoaded", function () {
    const reviewModalEl = document.getElementById("reviewModal");
    let reviewModal;
    if (reviewModalEl) {
        reviewModal = new bootstrap.Modal(reviewModalEl);
    }

    const deleteReviewModalEl = document.getElementById("deleteReviewModal");
    let deleteReviewModal;
    if (deleteReviewModalEl) {
        deleteReviewModal = new bootstrap.Modal(deleteReviewModalEl);
    }

    const form = document.getElementById("reviewForm");
    const titleSpan = document.getElementById("reviewModalTitle") ? document.getElementById("reviewModalTitle").querySelector('[data-slot="title"]') : null;
    const submitSpan = document.getElementById("reviewSubmitBtn") ? document.getElementById("reviewSubmitBtn").querySelector('[data-slot="submit"]') : null;

    const inputId = document.getElementById("Input_Id");
    const targetApp = document.getElementById("target-app");
    const targetDoctor = document.getElementById("target-doctor");
    const doctorSelectWrap = document.getElementById("doctorSelectWrap");
    const doctorSelect = document.getElementById("Input_DoctorId");
    const inputTitle = document.getElementById("Input_Title");
    const inputComment = document.getElementById("Input_Comment");
    const charCount = document.getElementById("charCount");

    function updateChoiceCards() {
        document.querySelectorAll(".choice-card").forEach(card => {
            const input = card.querySelector('input[type="radio"]');
            if (input && input.checked) {
                card.classList.add("is-selected");
            } else {
                card.classList.remove("is-selected");
            }
        });
    }

    function toggleDoctorSelect() {
        if (!doctorSelectWrap || !doctorSelect) return;

        if (targetDoctor && targetDoctor.checked) {
            doctorSelectWrap.hidden = false;
            doctorSelect.disabled = false;
            doctorSelect.setAttribute("required", "required");
        } else {
            doctorSelectWrap.hidden = true;
            doctorSelect.disabled = true;
            doctorSelect.removeAttribute("required");
            doctorSelect.value = "";
        }

        updateChoiceCards();
    }

    if (targetApp) targetApp.addEventListener("change", toggleDoctorSelect);
    if (targetDoctor) targetDoctor.addEventListener("change", toggleDoctorSelect);

    function setRating(value) {
        const star = document.getElementById("rating-" + value);
        if (star) {
            star.checked = true;
            star.dispatchEvent(new Event("change", { bubbles: true }));
        }
    }

    if (inputComment && charCount) {
        inputComment.addEventListener("input", function () {
            charCount.textContent = this.value.length;
        });
    }

    document.querySelectorAll('[data-mode="create"]').forEach(btn => {
        btn.addEventListener("click", function () {
            if (form) {
                form.reset();
                form.action = "?handler=Create";
            }
            if (inputId) inputId.value = "";
            if (titleSpan) titleSpan.textContent = "Write a Review";
            if (submitSpan) submitSpan.textContent = "Submit Feedback";

            const target = this.getAttribute("data-target");
            if (target === "Doctor" && targetDoctor) {
                targetDoctor.checked = true;
                if (targetApp) targetApp.checked = false;
            } else if (targetApp) {
                targetApp.checked = true;
                if (targetDoctor) targetDoctor.checked = false;
            }

            toggleDoctorSelect();
            if (charCount) charCount.textContent = "0";

            setRating(5);
        });
    });

    document.querySelectorAll(".js-edit-review").forEach(btn => {
        btn.addEventListener("click", function () {
            if (form) {
                form.reset();
                form.action = "?handler=Edit";
            }
            if (inputId) inputId.value = this.getAttribute("data-id");
            if (titleSpan) titleSpan.textContent = "Update Feedback";
            if (submitSpan) submitSpan.textContent = "Save Changes";

            const target = this.getAttribute("data-target");
            if (target === "Doctor") {
                if (targetDoctor) targetDoctor.checked = true;
                if (targetApp) targetApp.checked = false;
                if (doctorSelect) doctorSelect.value = this.getAttribute("data-doctor-id");
            } else {
                if (targetApp) targetApp.checked = true;
                if (targetDoctor) targetDoctor.checked = false;
            }
            toggleDoctorSelect();

            const rating = parseInt(this.getAttribute("data-rating"), 10) || 5;
            setRating(rating);

            if (inputTitle) inputTitle.value = this.getAttribute("data-title") || "";
            if (inputComment) {
                inputComment.value = this.getAttribute("data-comment") || "";
                if (charCount) charCount.textContent = inputComment.value.length;
            }

            if (reviewModal) reviewModal.show();
        });
    });

    document.querySelectorAll(".js-delete-review").forEach(btn => {
        btn.addEventListener("click", function () {
            const deleteInput = document.getElementById("deleteReviewId");
            if (deleteInput) deleteInput.value = this.getAttribute("data-id");
            if (deleteReviewModal) deleteReviewModal.show();
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

            const matchQuery = text.includes(query);
            const matchRating = rVal === "0" || rating === rVal;

            item.style.display = (matchQuery && matchRating) ? "flex" : "none";
        });
    }

    if (searchInput) searchInput.addEventListener("input", applyFilters);
    if (ratingFilter) ratingFilter.addEventListener("change", applyFilters);

    if (reviewModalEl) {
        reviewModalEl.addEventListener("shown.bs.modal", function () {
            updateChoiceCards();
        });
    }

    updateChoiceCards();
});
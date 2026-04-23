document.addEventListener("DOMContentLoaded", function () {
    const form = document.getElementById("reviewForm");
    const formPanel = document.querySelector(".form-panel");
    const formTitle = document.getElementById("formPanelTitle");
    const submitSpan = document.getElementById("reviewSubmitBtn")?.querySelector('[data-slot="submit"]');
    const btnCancelEdit = document.getElementById("btnCancelEdit");

    const inputId = document.getElementById("Input_Id");
    const targetApp = document.getElementById("target-app");
    const targetDoctor = document.getElementById("target-doctor");
    const doctorSelectWrap = document.getElementById("doctorSelectWrap");
    const doctorSelect = document.getElementById("Input_DoctorId");
    const inputTitle = document.getElementById("Input_Title");
    const inputComment = document.getElementById("Input_Comment");
    const charCount = document.getElementById("charCount");

    const deleteForm = document.getElementById("deleteReviewForm");
    const deleteInput = document.getElementById("deleteReviewId");

    function setFormMode(mode) {
        if (!form || !formTitle || !submitSpan || !btnCancelEdit || !formPanel) return;

        if (mode === "create") {
            form.setAttribute("action", "?handler=Create");
            formTitle.textContent = "Write a Review";
            submitSpan.textContent = "Submit Feedback";
            btnCancelEdit.classList.add("d-none");
            formPanel.classList.remove("edit-mode");
            form.reset();
            if (inputId) inputId.value = "";
            setRating(5);
            if (charCount) charCount.textContent = "0";
            if (targetDoctor) targetDoctor.checked = true;
            if (targetApp) targetApp.checked = false;
            toggleDoctorSelect();
        } else {
            form.setAttribute("action", "?handler=Edit");
            formTitle.textContent = "Update Feedback";
            submitSpan.textContent = "Save Changes";
            btnCancelEdit.classList.remove("d-none");
            formPanel.classList.add("edit-mode");
        }
    }

    function toggleDoctorSelect() {
        if (!doctorSelectWrap || !doctorSelect) return;

        const isDoctor = !!targetDoctor?.checked;
        doctorSelectWrap.hidden = !isDoctor;
        doctorSelect.disabled = !isDoctor;

        if (isDoctor) {
            doctorSelect.setAttribute("required", "required");
        } else {
            doctorSelect.removeAttribute("required");
            doctorSelect.value = "";
        }
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

    btnCancelEdit?.addEventListener("click", () => {
        setFormMode("create");
    });

    document.querySelectorAll(".js-edit-review").forEach(btn => {
        btn.addEventListener("click", function () {
            setFormMode("edit");

            if (inputId) inputId.value = this.getAttribute("data-id") || "";

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

            if (formPanel) {
                window.scrollTo({
                    top: formPanel.offsetTop - 120,
                    behavior: "smooth"
                });
            }
        });
    });

    // Simple delete confirm (no bootstrap modal)
    document.querySelectorAll(".js-delete-review").forEach(btn => {
        btn.addEventListener("click", function () {
            const reviewId = this.getAttribute("data-id");
            if (!reviewId || !deleteForm || !deleteInput) return;

            const ok = window.confirm("Sigur vrei să ștergi review-ul? Acțiunea nu poate fi anulată.");
            if (!ok) return;

            deleteInput.value = reviewId;
            deleteForm.submit();
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

    toggleDoctorSelect();
});
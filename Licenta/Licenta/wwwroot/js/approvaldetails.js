document.addEventListener("DOMContentLoaded", () => {
    const modal = document.getElementById("confirmModal");
    const rejectBtn = document.querySelector(".btn-reject");
    const closeBtn = document.querySelector(".modal-close");
    const cancelBtn = document.getElementById("cancelModal");
    const confirmBtn = document.getElementById("confirmReject");
    const form = document.querySelector(".action-form");

    let isSubmitting = false;

    if (rejectBtn) {
        rejectBtn.addEventListener("click", (e) => {
            if (rejectBtn.getAttribute("data-confirm") === "true") {
                e.preventDefault();
                modal?.classList.add("show");
                document.body.style.overflow = "hidden";
            }
        });
    }

    const closeModal = () => {
        modal?.classList.remove("show");
        document.body.style.overflow = "auto";
    };

    cancelBtn?.addEventListener("click", closeModal);
    modal?.addEventListener("click", (e) => {
        if (e.target === modal) closeModal();
    });

    confirmBtn?.addEventListener("click", () => {
        isSubmitting = true;
        closeModal();
        rejectBtn.classList.add("loading");
        rejectBtn.disabled = true;
        form?.submit();
    });

    const approveBtn = document.querySelector(".btn-approve");
    approveBtn?.addEventListener("click", () => {
        if (!isSubmitting) {
            isSubmitting = true;
            approveBtn.classList.add("loading");
            approveBtn.disabled = true;
        }
    });

    document.addEventListener("keydown", (e) => {
        if (e.key === "Escape" && modal?.classList.contains("show")) closeModal();
    });

    form?.addEventListener("submit", (e) => {
        if (form.classList.contains("submitted")) {
            e.preventDefault();
        }
        form.classList.add("submitted");
    });
});
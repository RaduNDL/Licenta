document.addEventListener("DOMContentLoaded", () => {
    const rows = document.querySelectorAll(".table-row-hover");

    rows.forEach((row, index) => {
        row.style.opacity = "0";
        row.style.animation = `fadeIn 0.4s ease-in-out forwards`;
        row.style.animationDelay = `${index * 0.06}s`;
    });

    rows.forEach((row) => {
        const requestNameElement = row.querySelector(".request-name");
        const originalFilename = requestNameElement?.getAttribute("data-original");

        if (originalFilename && originalFilename.length > 0) {
            const dateMatch = originalFilename.match(/\d{8}/);
            if (dateMatch) {
                const dateStr = dateMatch[0];
                const year = dateStr.substring(0, 4);
                const month = dateStr.substring(4, 6);
                const day = dateStr.substring(6, 8);
                const formattedDate = `${day}/${month}/${year}`;
                requestNameElement.textContent = `Appointment Request - ${formattedDate}`;
            } else {
                requestNameElement.textContent = "Appointment Request";
            }
        }
    });

    const patientSpecialties = document.querySelectorAll(".patient-specialty");
    patientSpecialties.forEach((element) => {
        const text = element.textContent.trim();
        if (text.length > 20) {
            const tooltip = document.createElement("div");
            tooltip.className = "tooltip-popup";
            tooltip.textContent = text;
            element.parentElement.appendChild(tooltip);

            element.addEventListener("mouseenter", () => {
                tooltip.style.display = "block";
            });

            element.addEventListener("mouseleave", () => {
                tooltip.style.display = "none";
            });
        }
    });

    rows.forEach((row) => {
        const buttons = row.querySelectorAll(".btn-action");

        row.addEventListener("mouseenter", () => {
            buttons.forEach(btn => {
                btn.style.opacity = "1";
            });
        });

        row.addEventListener("mouseleave", () => {
            buttons.forEach(btn => {
                btn.style.opacity = "1";
            });
        });
    });

    const reviewButtons = document.querySelectorAll(".btn-open");
    reviewButtons.forEach((button) => {
        button.addEventListener("click", (e) => {
            button.disabled = true;
            const originalContent = button.innerHTML;
            button.innerHTML = '<i class="bi bi-hourglass-split"></i> <span class="btn-text">Loading...</span>';

            setTimeout(() => {
                button.disabled = false;
                button.innerHTML = originalContent;
            }, 1000);
        });
    });
});
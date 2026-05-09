document.addEventListener("DOMContentLoaded", function () {
    const rows = document.querySelectorAll(".table-row");

    rows.forEach((row, index) => {
        row.style.animationDelay = `${index * 0.08}s`;
    });

    const alertBox = document.querySelector(".alert");
    if (alertBox) {
        setTimeout(() => {
            alertBox.style.transition = "opacity 0.5s ease, transform 0.5s ease";
            alertBox.style.opacity = "0";
            alertBox.style.transform = "translateY(-10px)";

            setTimeout(() => {
                alertBox.remove();
            }, 500);
        }, 4000);
    }
});
document.addEventListener("DOMContentLoaded", () => {
    const disabledCheckboxes = document.querySelectorAll("input[disabled]");

    disabledCheckboxes.forEach(checkbox => {
        checkbox.addEventListener("click", event => {
            event.preventDefault();
        });
    });
});
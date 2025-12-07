// Auto-hide success alerts after 5 seconds
$(document).ready(function () {
    setTimeout(function () {
        $(".alert-success").fadeOut("slow");
    }, 5000);

    // Bootstrap Tooltips initialization
    var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'))
    var tooltipList = tooltipTriggerList.map(function (tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl)
    });

    // File Upload Preview (Generic)
    $('input[type="file"]').on('change', function () {
        var fileName = $(this).val().split('\\').pop();
        $(this).next('.form-label').addClass("selected").html(fileName);
    });
});
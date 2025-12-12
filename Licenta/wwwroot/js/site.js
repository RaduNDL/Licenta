$(document).ready(function () {
    setTimeout(function () {
        $(".alert-success").fadeOut("slow");
    }, 5000);

    var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'))
    var tooltipList = tooltipTriggerList.map(function (tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl)
    });

    $('input[type="file"]').on('change', function () {
        var fileName = $(this).val().split('\\').pop();
        $(this).next('.form-label').addClass("selected").html(fileName);
    });
});
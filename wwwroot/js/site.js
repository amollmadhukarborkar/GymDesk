$(function () {
    var token = $('input[name="__RequestVerificationToken"]').first().val();
    if (token) {
        $.ajaxSetup({ headers: { "RequestVerificationToken": token } });
    }

    $(document).on("click", "[data-confirm]", function (event) {
        if (!window.confirm($(this).data("confirm"))) {
            event.preventDefault();
        }
    });

    $(document).on("input", "[data-table-filter]", function () {
        var query = $(this).val().toLowerCase();
        var table = $($(this).data("table-filter"));
        table.find("tbody tr").each(function () {
            $(this).toggle($(this).text().toLowerCase().indexOf(query) >= 0);
        });
    });

    $(document).on("change", "[data-close-toggle]", function () {
        $(this).closest("tr").find("input[type=time]").prop("disabled", $(this).is(":checked"));
    });

    $("[data-close-toggle]").each(function () {
        $(this).closest("tr").find("input[type=time]").prop("disabled", $(this).is(":checked"));
    });
});

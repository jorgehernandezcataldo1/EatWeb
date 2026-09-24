// Setup AJAX anti-forgery token
$.ajaxSetup({
    headers: {
        'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').first().val()
    }
});

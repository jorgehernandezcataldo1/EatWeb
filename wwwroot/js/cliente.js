// Cliente-side JavaScript para polling de MiMesa
document.addEventListener('DOMContentLoaded', function () {
    var lastErrorTicket = 0;
    var isRequestInProgress = false;

    function actualizarMesa() {
        if (isRequestInProgress || document.hidden) {
            return;
        }

        isRequestInProgress = true;

        $.get({
            url: $('[data-url-actualizar-mesa]').data('url-actualizar-mesa'),
            success: function (html) {
                // Reemplazar el tbody y los totales
                $('#tbodyMesa').html(html);
            },
            error: function () {
                var now = Date.now();
                if (now - lastErrorTicket > 5000) {
                    console.error('Error actualizando mesa');
                    lastErrorTicket = now;
                }
            },
            complete: function () {
                isRequestInProgress = false;
            }
        });
    }

    // Polling cada 10 segundos
    setInterval(actualizarMesa, 10000);
});

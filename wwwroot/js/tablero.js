// Tablero.js - Actualización en tiempo real del panel de pedidos y mesas
document.addEventListener('DOMContentLoaded', function () {
    var lastErrorTicket = 0;
    var isRequestInProgress = false;
    var estadoActual = $('[data-estado-actual]').data('estado-actual') || '';

    function actualizarPedidos() {
        if (isRequestInProgress || document.hidden) {
            return;
        }

        isRequestInProgress = true;

        var url = $('[data-url-actualizar]').data('url-actualizar');
        if (!url) {
            isRequestInProgress = false;
            return;
        }

        $.get({
            url: url + (estadoActual ? '?estado=' + estadoActual : ''),
            success: function (html) {
                // Reemplazar el tbody de pedidos
                $('#tbodyPedidos').html(html);
                reattachEventHandlers();
            },
            error: function () {
                var now = Date.now();
                if (now - lastErrorTicket > 5000) {
                    console.error('Error actualizando pedidos');
                    lastErrorTicket = now;
                }
            },
            complete: function () {
                isRequestInProgress = false;
            }
        });
    }

    function actualizarMesas() {
        if (isRequestInProgress || document.hidden) {
            return;
        }

        isRequestInProgress = true;

        var url = $('[data-url-actualizar-mesas]').data('url-actualizar-mesas');
        if (!url) {
            isRequestInProgress = false;
            return;
        }

        $.get({
            url: url,
            success: function (html) {
                $('#containerMesas').html(html);
            },
            error: function () {
                var now = Date.now();
                if (now - lastErrorTicket > 5000) {
                    console.error('Error actualizando mesas');
                    lastErrorTicket = now;
                }
            },
            complete: function () {
                isRequestInProgress = false;
            }
        });
    }

    function reattachEventHandlers() {
        // Cambiar estado
        $(document).off('click', '[data-cambiar-estado]').on('click', '[data-cambiar-estado]', function (e) {
            e.preventDefault();
            var pedidoId = $(this).data('pedido-id');
            var nuevoEstado = $(this).data('cambiar-estado');
            var url = $(this).data('url');

            $.post({
                url: url,
                data: { pedidoId: pedidoId, nuevoEstado: nuevoEstado },
                success: function (result) {
                    if (result.ok) {
                        actualizarPedidos();
                    }
                },
                error: function () {
                    alert('Error al cambiar estado');
                }
            });
        });

        // Cancelar pedido (abre modal)
        $(document).off('click', '[data-cancelar-pedido]').on('click', '[data-cancelar-pedido]', function (e) {
            var pedidoId = $(this).data('pedido-id');
            $('#modalCancelarPedidoId').val(pedidoId);
            var modal = new bootstrap.Modal(document.getElementById('modalCancelar'), {});
            modal.show();
        });
    }

    // Polling cada 5 segundos para pedidos, cada 10 para mesas
    setInterval(actualizarPedidos, 5000);
    if ($('[data-url-actualizar-mesas]').data('url-actualizar-mesas')) {
        setInterval(actualizarMesas, 10000);
    }

    // Initial attachment
    reattachEventHandlers();
});

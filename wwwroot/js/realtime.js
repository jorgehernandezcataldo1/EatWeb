(() => {
    if (typeof signalR === 'undefined') {
        document.dispatchEvent(new CustomEvent('comidita:RealtimeNoDisponible'));
        return;
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/operacion')
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    const reenviar = nombre => {
        connection.on(nombre, payload => {
            document.dispatchEvent(new CustomEvent('comidita:' + nombre, {
                detail: payload
            }));
        });
    };

    [
        'OperacionActualizada',
        'GarzonActualizado',
        'EstacionActualizada',
        'MesaActualizada'
    ].forEach(reenviar);

    function emitirEstado(estado) {
        document.dispatchEvent(new CustomEvent('comidita:RealtimeEstado', {
            detail: { estado }
        }));
    }

    connection.onreconnecting(() => emitirEstado('reconectando'));
    connection.onreconnected(() => emitirEstado('conectado'));
    connection.onclose(() => emitirEstado('desconectado'));

    async function iniciar() {
        try {
            await connection.start();
            emitirEstado('conectado');
        } catch {
            emitirEstado('desconectado');
            setTimeout(iniciar, 15000);
        }
    }

    window.comiditaRealtime = {
        connection,
        get estado() {
            return connection.state;
        }
    };

    iniciar();
})();
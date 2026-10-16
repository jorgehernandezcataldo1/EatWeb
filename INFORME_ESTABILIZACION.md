# Informe de estabilización — Comidita / EatWeb

Fecha: 2026-10-15
Rama: `estabilizacion`
Estado: **EN PROCESO — NO APTO AÚN PARA PILOTO SIN QA**.

## Alcance
Estabilizar funcionalidades existentes sin añadir módulos nuevos. Stack actual: ASP.NET Core MVC, Razor, EF Core, LINQ, ViewModels, AJAX y Bootstrap.

## Cambios aplicados
1. `PedidosController`: las acciones `CambiarEstado`, `CambiarEstadoDetalle` y `CambiarEstadoMesa` solo aceptan `Entregado`. La estación conserva el control de `EnPreparacion` y `Listo` en `EstacionTrabajoController`.
2. `Views/Pedidos/Mesa.cshtml`: retirados botones para preparar, marcar listo y cancelar desde la vista de garzón; se conservan botones de entrega y detalle de pedidos.

## Hallazgos por revisar
- `AlertasListos` limita a 20 los ítems listos; podría ocultar pendientes antiguos en servicios intensos.
- `EstacionTrabajoController` identifica estaciones por rol y nombre literal Cocina/Bar.
- Validar visualización móvil de todas las vistas, especialmente tablas, formularios y menús laterales.
- Validar cierre de mesa, pagos parciales, cancelaciones y reintentos concurrentes.
- Validar aislamiento multi-restaurante, permisos y asignaciones.
- Validar mensajes al cliente y alertas al garzón en dispositivos reales.
- Validar migraciones y despliegue, rotación de credenciales y configuración de producción.

## Evidencia de pruebas
- Revisión estática de código mediante GitHub: realizada parcialmente.
- Compilación `dotnet build`: **no ejecutada** (SDK .NET no disponible en el entorno de ejecución).
- Pruebas automatizadas: **no ejecutadas**.
- Pruebas funcionales navegador y responsive: **no ejecutadas**.
- Prueba integral con PostgreSQL: **no ejecutada**.

## QA mínimo antes de piloto
1. Dos comensales hacen pedidos desde una misma mesa.
2. Cocina y bar ven solo sus ítems; garzón no puede preparar ni marcar listo ni mediante POST manual.
3. Garzón recibe ítems listos y los entrega parcialmente.
4. Cliente recibe cambios de estado sin recargar.
5. Cuenta refleja consumos, pagos parciales y saldo correcto; mesa no cierra con deuda.
6. Usuarios de un restaurante no ven datos de otro.
7. Probar a 375px, 768px y 1440px; teclado, scroll, navegación y formularios.
8. Repetir solicitudes POST, abrir dos pestañas y probar condiciones de error.

**No declarar piloto listo hasta completar compilación, ejecución y QA.**

# Comidita — Informe rama `ideas`

## 1. Objetivo

Esta rama nace como una pasada de producto y operación sobre el piloto actual de Comidita.

La pregunta usada para decidir qué cambiar fue:

> ¿Qué tendría que aportar el sistema durante un turno real para que un restaurante perciba que ahorra tiempo, reduce errores y obtiene información por la que vale la pena pagar?

La rama parte de `master` en el commit:

`67af108c32fda5c0bb1f90b0c17ee6ad2b96750b`

No se reemplazó la arquitectura MVC existente ni se creó una segunda capa paralela de negocio. Los cambios reutilizan los servicios, controladores, ViewModels, estados, autorización y vistas actuales.

---

## 2. Metodología de revisión

Se reconstruyeron los flujos del sistema a partir de:

- controladores;
- servicios de dominio;
- ViewModels;
- vistas Razor;
- reglas de autorización;
- relaciones EF Core;
- transiciones de estados;
- endpoints que consumen las vistas;
- comportamiento actual de polling/notificaciones.

### Alcance real de la validación

En este entorno no se dispone de una sesión de navegador autenticada con las credenciales de cada rol del restaurante. Por lo tanto no se afirma que se haya realizado un QA manual end-to-end haciendo clic en el despliegue real.

La validación de esta rama se compone de:

1. recorrido lógico de cada rol siguiendo las rutas reales del código;
2. revisión de aislamiento por restaurante, mesa, estación y usuario;
3. revisión de transiciones de estado y concurrencia existente;
4. compilación mediante GitHub Actions;
5. matriz de QA manual incluida al final de este documento.

El QA manual con usuarios reales debe ejecutarse antes de promover estos cambios a producción.

---

# 3. Simulación de un día normal

## Escenario A — Una persona, consumo rápido

### Cliente

1. Escanea el QR.
2. Ingresa su nombre.
3. Se crea la sesión si la mesa estaba libre.
4. Se crea el comensal.
5. Agrega un plato y una bebida.
6. Personaliza el plato.
7. Envía pedido.
8. Observa cambios de preparación.
9. Solicita la cuenta.
10. Paga y termina la sesión.

### Antes

Los distintos roles detectaban estos cambios mediante ciclos de polling de 5 a 12 segundos.

### Problema percibido

Cinco segundos no parecen demasiado en desarrollo, pero en un restaurante se sienten mucho cuando:

- una mesa llama al garzón;
- cocina termina un plato;
- entra un pedido durante hora punta;
- una cuenta debe cobrarse;
- el cliente está mirando si su pedido ya se prepara.

### Cambio

El cambio crítico ahora se publica por SignalR inmediatamente.

La base de datos sigue siendo la fuente de verdad. SignalR únicamente informa:

> “algo cambió en esta operación”.

La vista vuelve a consultar su endpoint ya autorizado.

---

## Escenario B — Mesa de 10 personas

Se consideró una mesa con:

- 10 comensales;
- pedidos separados;
- productos de Cocina y Bar;
- “sin cebolla”;
- “extra queso”;
- observaciones individuales;
- una segunda ronda de bebidas;
- solicitudes simultáneas;
- productos listos en tiempos distintos;
- solicitud de cuenta;
- pago parcial / dividido.

### Lo que importa operacionalmente

Un pedido puede producir simultáneamente trabajo en:

- Cocina;
- Bar;
- Garzón;
- vista del cliente;
- dashboard administrativo.

No conviene que cada pantalla consulte PostgreSQL cada cinco segundos “por si acaso”.

### Cambio

El pedido se guarda una sola vez mediante el flujo existente y después se publica un evento mínimo.

Ejemplo conceptual:

```text
Pedido creado
     │
     ├── Cocina recibe evento
     ├── Bar recibe evento
     ├── Garzón recibe evento
     ├── Mesa cliente recibe evento
     └── Admin restaurante/cadena recibe evento
```

Cada rol consulta únicamente la información que ya podía consultar antes.

---

# 4. Decisión técnica: SignalR + fallback

## Por qué SignalR

En este producto los cambios operativos son eventos discretos:

- entró una mesa;
- apareció un comensal;
- se creó un pedido;
- cambió el estado de un pedido;
- se creó/canceló/atendió una solicitud;
- se registró un pago;
- se pagó la cuenta;
- se cerró la mesa.

Este patrón encaja mejor con una conexión persistente que con polling agresivo.

## Por qué NO se eliminó completamente el polling

Una red de restaurante no siempre es perfecta.

Puede ocurrir:

- suspensión del teléfono;
- Wi-Fi con microcortes;
- proxy que interrumpe WebSocket;
- reconexión de SignalR;
- despliegue/reinicio del servidor.

Por eso se adoptó:

```text
SignalR
   ↓
actualización inmediata

+

polling lento
   ↓
recuperación / reconciliación
```

Frecuencias de respaldo:

| Vista | Antes | Rama ideas |
|---|---:|---:|
| Garzón / feed de alertas | 5 s | 30 s |
| Garzón / Mesas | 5 s | 30 s |
| Cocina / Bar | 5 s | 30 s |
| Cliente / estados | 6 s | 30 s |
| Cliente / solicitudes | 8 s | 45 s |
| Cliente / mesa completa | 12 s | 45 s |
| Admin | sin realtime | 60 s fallback |

Esto reduce lecturas repetitivas y mantiene recuperación automática.

---

# 5. Arquitectura realtime

## Archivos nuevos

### `Realtime/ComiditaHub.cs`

Único Hub de operación.

No contiene lógica de negocio.

Su responsabilidad es asignar conexiones a grupos seguros calculados por el servidor.

Grupos:

```text
restaurante:{id}
garzon:{usuarioId}
estacion:{restauranteId}:{estacion}
sesion:{sesionId}
```

### `Realtime/OperacionRealtimeService.cs`

Centraliza la publicación.

Los servicios de negocio no usan directamente el Hub.

```text
Servicio de dominio
      │
      ▼
OperacionRealtimeService
      │
      ▼
ComiditaHub
```

Si SignalR falla, el servicio captura el error y lo registra.

Un fallo realtime NO revierte:

- pedido;
- solicitud;
- cambio de estado;
- pago;
- cierre de mesa.

La operación persistida siempre tiene prioridad.

### `wwwroot/js/realtime.js`

Cliente compartido de SignalR.

Funciones:

- conexión a `/hubs/operacion`;
- reconexión automática;
- expone estado de conexión;
- transforma mensajes SignalR en eventos DOM desacoplados.

Eventos DOM:

```text
comidita:OperacionActualizada
comidita:GarzonActualizado
comidita:EstacionActualizada
comidita:MesaActualizada
comidita:RealtimeEstado
```

Las vistas no necesitan conocer detalles internos del Hub.

---

# 6. Seguridad por diseño

## 6.1 El navegador no decide a qué grupo unirse

No se implementó un método como:

```text
JoinRestaurant(5)
JoinTable(20)
```

Eso permitiría intentar enumerar recursos.

Los grupos se calculan exclusivamente en `OnConnectedAsync`.

---

## 6.2 Cliente

El cliente continúa identificado por:

`eatweb_comensal`

La cookie ya era:

- HttpOnly;
- SameSite Lax;
- Secure en HTTPS;
- token GUID único.

Durante la conexión SignalR el servidor valida:

- token existente;
- sesión abierta;
- mesa activa;
- restaurante activo.

Solamente entonces agrega la conexión a:

`sesion:{sesionId}`

JavaScript nunca necesita leer la cookie.

---

## 6.3 Personal autenticado

Para usuarios Identity:

- se toma `NameIdentifier`;
- se verifican membresías reales;
- AdminCadena se resuelve por `CadenaMiembro`;
- resto del personal se resuelve por `RestauranteMiembro`.

Después se agregan los grupos permitidos.

Garzón:

`garzon:{usuarioId}`

Cocina:

`estacion:{restauranteId}:cocina`

Bar:

`estacion:{restauranteId}:bar`

Admins:

grupos de los restaurantes a los que ya tienen acceso.

---

## 6.4 Los eventos no reemplazan autorización

SignalR no envía el detalle completo del pedido.

El mensaje contiene información operacional mínima:

- tipo;
- restaurante;
- sesión cuando aplica;
- fecha.

Luego la vista consulta su endpoint normal.

Esto conserva:

- autorización del controller;
- aislamiento por restaurante;
- reglas de garzón;
- validación de comensal;
- filtros de estación.

---

## 6.5 Rate limiting del cliente

Se agregó la política:

`cliente`

Antes de que exista cookie:

- partición por IP;
- 40 solicitudes/minuto.

Después de entrar a la mesa:

- partición por token de comensal;
- 120 solicitudes/minuto.

Esto es importante en restaurantes: diez personas bajo el mismo Wi-Fi no quedan compartiendo artificialmente el mismo límite una vez identificadas.

---

## 6.6 Rate limiting realtime

Se agregó:

`realtime`

Partición:

1. usuario Identity;
2. comensal;
3. IP como fallback.

Máximo:

60 negociaciones/conexiones por minuto por identidad.

El middleware de rate limiting se ejecuta después de autenticación para que el personal sea particionado por usuario real.

---

## 6.7 Protecciones existentes preservadas

No se retiró:

- antiforgery global en acciones inseguras;
- Identity lockout;
- Data Protection persistido en PostgreSQL;
- cookies HttpOnly;
- comprobaciones de restaurante;
- idempotencia de pagos;
- aislamiento de MesaSesion;
- validación de productos/precios en servidor.

---

# 7. Eventos publicados

| Evento | Cliente | Garzón | Cocina/Bar | Admin restaurante | Admin cadena |
|---|---:|---:|---:|---:|---:|
| Mesa ocupada | sesión | Sí | — | Sí | Sí |
| Comensal ingresó | sesión | Sí | — | Sí | Sí |
| Pedido creado | Sí | Sí | estación afectada | Sí | Sí |
| Pedido actualizado | Sí | Sí | estación afectada | Sí | Sí |
| Solicitud creada | Sí | Sí | — | Sí | Sí |
| Solicitud cancelada | Sí | Sí | — | Sí | Sí |
| Solicitud atendida | Sí | Sí | — | Sí | Sí |
| Pago registrado | Sí | Sí | — | Sí | Sí |
| Cuenta pagada | Sí | Sí | — | Sí | Sí |
| Mesa cerrada | sesión actual | Sí | — | Sí | Sí |

---

# 8. Cambios por archivo

## `Program.cs`

Agregado:

- SignalR;
- `OperacionRealtimeService`;
- endpoint `/hubs/operacion`;
- rate limiter `cliente`;
- rate limiter `realtime`;
- orden de middleware compatible con partición autenticada.

No se agregaron tablas ni migraciones.

---

## `Services/SesionService.cs`

Agregado realtime para:

- apertura de sesión;
- ingreso de comensal;
- cierre.

También se refuerza que la mesa/restaurante estén activos al abrir una nueva sesión.

---

## `Services/PedidoService.cs`

Agregado realtime después de:

- crear pedido;
- cambiar estado;
- cambiar estado por detalle;
- cambios masivos.

Se reutiliza la información del pedido para determinar:

- restaurante;
- sesión;
- garzón;
- estaciones afectadas.

No se movieron las reglas de precios ni personalizaciones al cliente.

---

## `Services/SolicitudMesaService.cs`

Publica después de:

- crear;
- cancelar;
- atender.

Se mantienen las reglas existentes:

- evitar duplicados pendientes;
- exigir consumo para pedir cuenta;
- validar pertenencia del comensal;
- validar garzón/admin al atender.

---

## `Services/CuentaService.cs`

Después del commit exitoso publica:

- `PagoRegistrado`;
- `CuentaPagada`.

La publicación ocurre después de confirmar la transacción.

La idempotencia existente del pago sigue intacta.

---

## `Views/Shared/_Layout.cshtml`

Garzón:

- conserva toast;
- conserva Browser Notification;
- conserva sonido;
- ahora SignalR dispara inmediatamente la consulta del feed;
- polling pasa de 5 s a 30 s.

No se duplicó el formato de notificación.

---

## `Views/Mesas/Index.cshtml`

Se mantiene la vista mapa/lista.

Cambios:

- refresco por SignalR;
- fallback 30 s;
- debounce para ráfagas de eventos;
- panel lateral cambiado de lista pasiva a **Próximas acciones**.

---

# 9. Mejora de valor: cola prioritaria del garzón

Durante hora punta el problema no es solo “tener notificaciones”.

El problema es saber:

> ¿Qué hago ahora?

Se agregó una cola calculada con información existente.

Prioridad aproximada:

1. cuenta pendiente;
2. platos listos esperando retiro;
3. llamada directa al garzón;
4. solicitud personalizada;
5. antigüedad incrementa prioridad.

Ejemplo:

```text
Mesa 8   💳 Preparar / cobrar cuenta
Mesa 4   🍽 Retirar 3 productos listos
Mesa 2   🔔 Mesa llama al garzón · hace 7 min
Mesa 11  • Servilletas · hace 5 min
```

No se creó un estado nuevo.

No se persiste otra cola.

Siempre se reconstruye desde la fuente de verdad.

---

# 10. Cocina y Bar

`Views/EstacionTrabajo/Index.cshtml`

La KDS actual conserva:

- Nuevos;
- En preparación;
- Listos;
- urgentes;
- orden por antigüedad;
- búsqueda;
- sonido;
- acciones táctiles.

Cambio:

- `EstacionActualizada` actualiza inmediatamente;
- fallback 30 s.

Una estación solo se suscribe al grupo que corresponde a su rol.

Un pedido mixto Cocina/Bar notifica a las dos estaciones, pero cada pantalla vuelve a consultar únicamente los detalles que su controller permite.

---

# 11. Cliente

`Views/Cliente/MiMesa.cshtml`

Ya existían:

- estado propio del pedido;
- pedidos de la mesa;
- estado de solicitudes;
- alertas SweetAlert.

Se conservaron.

Ahora `MesaActualizada` dispara inmediatamente:

- solicitudes;
- pedidos de la mesa;
- estado del pedido propio.

Esto mejora especialmente:

- “ya lo están preparando”;
- “está listo”;
- “solicitud atendida”;
- pagos/cierre.

---

# 12. Mejora de valor: salud operativa para Admin Restaurante

El dashboard anterior decía qué estaba ocurriendo.

La rama `ideas` agrega si la operación está funcionando bien.

Nuevos indicadores calculados sin tablas nuevas:

### Preparación promedio

`FechaListo - FechaInicioPreparacion`

Responde:

> ¿Cuánto demora realmente cocina hoy?

### Respuesta a solicitudes

`FechaResolucion - FechaCreacion`

Responde:

> ¿Cuánto demora el equipo en atender a una mesa?

### Pedidos atrasados

Pedidos todavía `Pendiente` después de 10 minutos.

Responde:

> ¿Hay tickets que nadie está tomando?

### Mesas de larga estadía

Sesiones abiertas por más de 90 minutos.

No implica necesariamente un problema, pero permite detectar mesas que requieren observación.

El dashboard se actualiza por `OperacionActualizada` y conserva fallback de 60 s.

---

# 13. Mejora para Admin Cadena

Cada local ahora expone también pedidos atrasados.

El administrador de cadena puede detectar:

```text
Providencia   0 atrasados
Ñuñoa         6 atrasados
Las Condes    1 atrasado
```

Eso es más accionable que comparar solamente ventas.

El dashboard de cadena también recibe eventos de todos los restaurantes de cadenas a las que el usuario pertenece.

---

# 14. Cambios de comportamiento eliminados

No se eliminó ningún estado de negocio.

No se eliminó ningún dato.

No se agregaron migraciones.

Lo que sí se redujo:

- polling cada 5 segundos como mecanismo principal;
- polling frecuente del cliente;
- espera artificial antes de reflejar una acción.

SignalR se convierte en camino principal y el polling queda como reconciliación.

---

# 15. Escenarios de QA por rol

## Cliente — 1 comensal

1. Escanear QR.
2. Ingresar nombre.
3. Confirmar que la mesa aparece ocupada para garzón casi inmediatamente.
4. Agregar producto con personalización.
5. Enviar.
6. Ver pedido en Cocina/Bar sin esperar polling.
7. Cambiar a preparación.
8. Confirmar SweetAlert de cliente.
9. Marcar listo.
10. Confirmar estado cliente y alerta garzón.
11. Solicitar algo.
12. Atender desde garzón.
13. Confirmar que cliente ve atendida.
14. Pedir cuenta.
15. Pagar.
16. Confirmar venta en admin y cierre de sesión.

---

## Cliente — 10 comensales

1. Abrir 10 comensales en la misma MesaSesion.
2. Confirmar contador de comensales.
3. Enviar pedidos desde distintos comensales en pocos segundos.
4. Mezclar productos Cocina/Bar.
5. Usar:
   - sin ingrediente;
   - extra;
   - observación;
   - cantidades múltiples.
6. Confirmar que cada estación recibe solamente lo suyo.
7. Marcar platos listos en orden distinto.
8. Confirmar cola prioritaria del garzón.
9. Crear varias solicitudes.
10. Confirmar que no aparecen duplicados indebidos.
11. Solicitar cuenta.
12. Probar pagos parciales/diferentes métodos.
13. Confirmar saldo.
14. Confirmar cierre solo en saldo cero.

---

## Garzón

Validar:

- solo mesas asignadas;
- mesa nueva inmediata;
- pedido nuevo inmediato;
- plato listo inmediato;
- llamada inmediata;
- cuenta inmediata;
- cola “Próximas acciones” ordenada;
- Browser Notification si fue autorizada;
- reconexión después de apagar/encender Wi-Fi;
- fallback recupera estado aunque SignalR se interrumpa.

---

## Cocina

Validar:

- solo productos Cocina;
- pedido aparece al instante;
- personalizaciones correctas;
- pedido grande agrupado;
- Pendiente → En preparación;
- En preparación → Listo;
- no puede Entregar;
- Bar no ve productos Cocina.

---

## Bar

Misma matriz de Cocina, invertida por estación.

---

## Admin Restaurante

Validar:

- dashboard cambia tras abrir/cerrar mesa;
- ventas cambian después del pago;
- pedidos activos cambian;
- solicitudes pendientes cambian;
- preparación promedio coherente;
- respuesta a solicitudes coherente;
- atraso >10 min;
- mesa >90 min;
- no puede acceder a otro restaurante sin membresía.

---

## Admin Cadena

Validar:

- recibe cambios de todos sus locales;
- no recibe locales de otra cadena;
- ranking/ocupación/ventas;
- atrasos por local;
- selector conserva límites de acceso.

---

# 16. Escenarios de seguridad

## A. Intentar conexión sin usuario y sin cookie

Resultado esperado:

- conexión técnica posible;
- cero grupos internos;
- no recibe eventos de operación.

## B. Cookie de comensal de sesión cerrada

Resultado esperado:

- no entra a grupo de sesión.

## C. Garzón A intentando ver Mesa de Garzón B

Los endpoints actuales deben continuar rechazando/filtrando esa mesa.

SignalR no concede acceso adicional.

## D. Cocina intentando entrar a grupo Bar

No existe método cliente para solicitar ese grupo.

La pertenencia se calcula por rol.

## E. Admin de restaurante intentando otro restaurante

Los grupos provienen de `RestauranteMiembro`.

## F. Admin cadena

Los grupos se calculan desde `CadenaMiembro`.

## G. Abuso de QR/API

Validar respuesta HTTP 429 al exceder límites.

## H. Caída de SignalR

La operación debe persistir normalmente.

Después del intervalo de fallback las pantallas deben reconciliarse.

---

# 17. Qué aporta valor comercial

Las mejoras que se consideran de mayor valor para vender el sistema son:

1. **operación instantánea**, porque evita esperar/refrescar;
2. **cola prioritaria del garzón**, porque convierte eventos en acciones;
3. **KDS instantánea**, porque reduce latencia entre cliente y producción;
4. **estado instantáneo al cliente**, porque reduce preguntas al personal;
5. **métricas de tiempo reales**, porque el encargado puede gestionar el servicio y no solo mirar ventas;
6. **comparación de atrasos entre locales**, porque un dueño de cadena puede detectar dónde intervenir;
7. **fallback resiliente**, porque un restaurante no puede depender de una conexión WebSocket perfecta;
8. **seguridad contextual**, porque el tiempo real no debe romper el aislamiento ya existente.

---

# 18. Limitaciones conocidas

## Browser Push real

Las Browser Notifications actuales necesitan la aplicación/navegador operativo.

No se implementó todavía:

- Service Worker;
- Web Push;
- VAPID;
- almacenamiento de suscripciones.

Para el piloto, SignalR + Browser Notification cubre el flujo con la aplicación abierta.

---

## Escalamiento horizontal

Los grupos de SignalR viven en la instancia del servidor.

Con una sola instancia de aplicación funciona correctamente.

Si Comidita se despliega luego con múltiples instancias simultáneas, se necesitará un backplane, por ejemplo:

- Redis;
- Azure SignalR Service;
- otro mecanismo compatible.

Esto debe resolverse antes de escalar horizontalmente.

---

## Cliente JavaScript de SignalR

La rama carga una versión fijada del cliente JavaScript desde CDN.

Antes de una etapa de producción más estricta conviene vendorear/autohospedar el archivo dentro de `wwwroot/lib` mediante un proceso de dependencias reproducible, evitando depender de un tercero en runtime.

---

# 19. Archivos nuevos

```text
Realtime/ComiditaHub.cs
Realtime/OperacionRealtimeService.cs
wwwroot/js/realtime.js
IDEAS_IMPLEMENTACION.md
```

# 20. Archivos modificados principales

```text
Program.cs
Controllers/ClienteController.cs
Controllers/MesasController.cs
Services/SesionService.cs
Services/PedidoService.cs
Services/SolicitudMesaService.cs
Services/CuentaService.cs
Services/AdminDashboardService.cs
ViewModels/MesaViewModel.cs
ViewModels/AdminDashboardViewModels.cs
Views/Shared/_Layout.cshtml
Views/Shared/_LayoutCliente.cshtml
Views/Shared/_LayoutEstacion.cshtml
Views/Shared/_LayoutAdmin.cshtml
Views/Mesas/Index.cshtml
Views/EstacionTrabajo/Index.cshtml
Views/Cliente/MiMesa.cshtml
Views/Admin/Restaurante.cshtml
Views/Admin/IndexCadena.cshtml
wwwroot/css/site.css
wwwroot/css/admin.css
```

# 21. Base de datos

Esta rama no necesita una nueva migración.

Todos los indicadores y eventos se apoyan en campos existentes.

---

# 22. Criterio para merge

Antes de mergear:

- GitHub Actions debe compilar en Release;
- ejecutar matriz manual mínima Cliente/Garzón/Cocina/Bar;
- probar una mesa con varios comensales;
- probar desconexión de SignalR;
- probar un pago;
- comprobar permisos cruzados;
- comprobar 429 sin afectar uso normal.

La rama está diseñada para que si SignalR falla, el sistema continúe funcionando con la base de datos y los refrescos de respaldo.

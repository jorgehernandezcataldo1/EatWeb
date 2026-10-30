# Planes y funcionalidades comerciales — EatWeb

## Arquitectura
Las membresías `CadenasMiembros` y `RestaurantesMiembros` son de **empleados**:
gestionan permisos de administrador, garzón, cocina/bar. No representan pagos o
planes y no se duplican en las nuevas tablas.

Las nuevas tablas son:
- `PlanesComerciales`: catálogo de planes, límite opcional de sucursales.
- `Funcionalidades`: catálogo único de claves de módulo.
- `PlanesFuncionalidades`: funciones incluidas en cada plan (clave compuesta).
- `SuscripcionesComerciales`: UNA suscripción por cadena o por restaurante
  (exactamente uno de los dos, con restricción CHECK e índices únicos).
- `RestaurantesFuncionalidadesOverride`: excepción por funcionalidad y local
  (clave compuesta): `true` concede, `false` bloquea, ausencia hereda.

La cadena y el restaurante conservan sus datos en las tablas existentes.
No hay copias de usuarios, roles, precios ni locales en el modelo comercial.

## Precedencia de una función en restaurante R
1. Si el local o su cadena está suspendido -> no disponible.
2. Si existe excepción explícita para R -> conceder o bloquear.
3. Si R tiene suscripción propia -> evaluar solo esta suscripción, incluso si
   está suspendida o vencida (no se recupera por herencia).
4. Si no hay suscripción propia y R pertenece a cadena -> heredar el plan de cadena.
5. En otro caso -> no disponible.

Un plan marcado como no disponible en el catálogo impide nuevas asignaciones,
pero no revoca las suscripciones ya contratadas. La vigencia se controla con
`SuscripcionesComerciales.Activa` y `VenceUtc`.

**Compatibilidad del MVP:** las claves comerciales aún no creadas permiten seguir
usando los módulos actuales. Una vez creada una clave protegida, se requiere
un plan o excepción que la conceda.

Claves que ya están conectadas al control de autorización del servidor:
- `catalogo.gestion` — controlador de Productos.
- `estaciones.gestion` — controlador de Estaciones.

Otras claves añadidas al catálogo no bloquean automáticamente controladores
hasta integrar explícitamente `[RequiereFuncionalidad("clave")]`. No activar
una función imaginando que protege acciones que todavía no han sido integradas.
La autorización técnica de roles y membresías se aplica independientemente.

## Prueba manual sugerida — ambiente local independiente
1. Respaldar la base destinada a la prueba y configurar
   `ConnectionStrings__DefaultConnection` hacia una base **no productiva**.
2. Obtener la rama `audit/demo-readiness-20261027`, restaurar paquetes
   y ejecutar `dotnet test`.
3. Ejecutar `dotnet ef database update` (también hay migración al iniciar).
   Confirmar tablas nuevas y `__EFMigrationsHistory`.
4. Con una cuenta existente y activa, habilitar una sola vez el bootstrap:
   `SeedSuperAdmin__Enabled=true` y `SeedSuperAdmin__Email=... `.
   Apagar inmediatamente `SeedSuperAdmin__Enabled` después de asignar el rol.
5. Iniciar sesión y entrar a `/SuperAdmin`. Crear `Cadena A`,
   `Local 1` y `Local 2` dentro de la cadena; crear `Local Independiente`.
6. Crear `Plus` y `Cadena Plus`. Crear claves
   `catalogo.gestion`, `estaciones.gestion` y otras capacidades.
   Asignar funciones a cada plan desde el panel.
7. Asignar `Cadena Plus` a `Cadena A`. Verificar acceso heredado en
   `Local 1` y `Local 2`.
8. Dar una **excepción de bloqueo** a `Local 2` en
   `catalogo.gestion`. Comprobar que `Local 1` sigue autorizado.
9. Asignar `Plus` directamente a `Local 2`: prevalece sobre Cadena Plus.
   Quitar plan propio desde su detalle: vuelve a heredar Cadena Plus.
10. Asignar `Plus` a `Local Independiente` y verificar que las
    otras tiendas no se ven afectadas.
11. Suspender suscripción local y comprobar que no hereda por accidente.
    Suspender cadena y comprobar bloqueo de los locales de dicha cadena.
12. Probar administradores y garzones ordinarios: no deben acceder a
    `/SuperAdmin` y los permisos operativos siguen sujetos a su membresía.

## Pendientes antes de vender / expandir
- Pruebas de integración PostgreSQL y E2E para operaciones concurrentes,
  vencimientos y combinaciones de planes.
- Conectar nuevas claves a los módulos adicionales que se comercializarán.
- Política de facturación, renovaciones, gracia, registro de cambios administrativos
  y monitoreo/alertas del vencimiento.
- La sesión y SignalR usan el patrón de revocación local ya existente;
  múltiples instancias necesitan coordinación distribuida.

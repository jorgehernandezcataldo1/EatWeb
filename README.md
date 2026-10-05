# Comidita

Comidita es una plataforma de autogestión para restaurantes y bares construida con ASP.NET Core MVC, EF Core, PostgreSQL e Identity.

## Flujo MVP

El recorrido cubierto actualmente es:

1. El cliente escanea el QR de una mesa.
2. Ingresa su nombre y abre/se une a la sesión de mesa.
3. Revisa la carta, personaliza productos y envía pedidos.
4. Cocina/Bar reciben el trabajo separado por estación.
5. El garzón gestiona preparación, entrega y solicitudes de clientes.
6. El cliente puede llamar al garzón, pedir la cuenta o solicitar algo adicional.
7. La cuenta puede pagarse completa, por persona, en partes o por ítems.
8. Al completar el pago la sesión se cierra y la mesa queda disponible.
9. El administrador puede revisar sesiones, pagos y operación histórica.

## Configuración segura de desarrollo

El repositorio no debe contener connection strings ni contraseñas.

El proyecto tiene `UserSecretsId`, por lo que en desarrollo configura al menos:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<CONNECTION_STRING_POSTGRES>"
```

Para una base nueva puedes crear el primer administrador y restaurante con:

```powershell
dotnet user-secrets set "SeedAdmin:Email" "<EMAIL_ADMIN>"
dotnet user-secrets set "SeedAdmin:Password" "<PASSWORD_SEGURA>"
dotnet user-secrets set "SeedAdmin:Nombre" "<NOMBRE>"
dotnet user-secrets set "SeedAdmin:RestauranteNombre" "<NOMBRE_RESTAURANTE>"
```

Después:

```powershell
dotnet build
dotnet ef database update
dotnet run
```

Una vez creado el administrador inicial puedes quitar la contraseña bootstrap:

```powershell
dotnet user-secrets remove "SeedAdmin:Password"
```

Los usuarios y datos demo están deshabilitados por defecto. `SeedDemo:Enabled` solo debe activarse en entornos de prueba y exige contraseñas configuradas externamente.

## Producción / piloto

Usa variables de entorno o el almacén de secretos del proveedor:

- `ConnectionStrings__DefaultConnection`
- `App__BaseUrl` si necesitas forzar una URL pública para los QR
- `SeedAdmin__Email`, `SeedAdmin__Password`, `SeedAdmin__Nombre`, `SeedAdmin__RestauranteNombre` solo para el bootstrap inicial
- `SeedDemo__Enabled=false`

El endpoint `/health` responde OK solo cuando la aplicación puede conectarse a PostgreSQL.

### Importante: rotación de credenciales

Una connection string con credenciales estuvo versionada anteriormente en el repositorio. Aunque ya no esté en `appsettings.json`, permanece en el historial Git. Antes de un piloto real se debe **rotar la contraseña/credencial de esa base de datos** y configurar únicamente la nueva credencial mediante secretos.

## Checklist de piloto

Antes de sentar clientes reales:

- Dashboard del restaurante muestra **Listo para piloto**.
- Cocina y Bar están activas.
- Existe al menos una categoría activa.
- Existe al menos un producto activo y disponible.
- Existe al menos una mesa activa con QR.
- Existe al menos un garzón activo.
- Existe al menos una mesa asignada a un garzón.
- El QR se abre correctamente desde un teléfono que no sea el equipo de desarrollo.
- Un pedido mixto Cocina + Bar se separa correctamente.
- Una solicitud Cliente → Garzón aparece y puede ser atendida.
- Los ítems cancelados no se cobran.
- Pago parcial + pago final dejan saldo cero y cierran la mesa.
- La sesión cerrada aparece en Historial con sus pagos.
- Un segundo restaurante no puede ver datos del primero.
- Un garzón no puede operar mesas ajenas por URL directa.

## Operación

El dashboard administrativo incluye métricas del día:

- ventas confirmadas;
- propinas;
- mesas ocupadas;
- pedidos activos;
- solicitudes pendientes;
- disponibilidad de catálogo;
- checklist de preparación para piloto.

## Almacenamiento de imágenes

Comidita usa una abstracción propia `IStorageService`. La implementación actual habla el protocolo S3, por lo que el resto de la aplicación no depende de Supabase directamente.

Las imágenes de productos se guardan en PostgreSQL como una clave neutral, por ejemplo:

```text
restaurantes/12/productos/45/0f4c8d....webp
```

La URL pública se construye desde configuración. Esto permite migrar a otro proveedor S3/CDN sin reescribir controladores ni vistas.

### Supabase Storage

1. Crea un bucket público llamado `comidita-media`.
2. En Supabase, habilita/configura el acceso S3 y obtén región, Access Key ID y Secret Access Key.
3. No guardes esas credenciales en Git.

Configuración local:

```powershell
dotnet user-secrets set "Storage:ServiceUrl" "https://<project-ref>.storage.supabase.co/storage/v1/s3"
dotnet user-secrets set "Storage:Region" "<REGION>"
dotnet user-secrets set "Storage:AccessKey" "<ACCESS_KEY>"
dotnet user-secrets set "Storage:SecretKey" "<SECRET_KEY>"
dotnet user-secrets set "Storage:Bucket" "comidita-media"
dotnet user-secrets set "Storage:PublicBaseUrl" "https://<project-ref>.supabase.co/storage/v1/object/public/comidita-media"
```

En Render usa las mismas claves como variables de entorno:

- `Storage__ServiceUrl`
- `Storage__Region`
- `Storage__AccessKey`
- `Storage__SecretKey`
- `Storage__Bucket=comidita-media`
- `Storage__PublicBaseUrl`

El formulario de productos acepta JPG, PNG y WebP, con un máximo predeterminado de 5 MB. El límite puede ajustarse con `Storage__MaxImageBytes`.

Las credenciales S3 son exclusivamente de servidor. No deben exponerse en JavaScript, HTML, URLs públicas ni commits.

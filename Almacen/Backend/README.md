# Almacén inteligente

Sistema de almacén con clientes, proveedores, productos con aprobación, almacenes con existencias,
recepciones, ventas, traspasos, kardex, movimientos financieros, reportería y correos.

## Arquitectura (hexagonal)

| Proyecto | Contenido |
|---|---|
| `AlmacenInteligente.Domain` | Entidades, value objects (RFC, Dirección, Dinero, IVA) y reglas: existencias nunca negativas, costo promedio ponderado, kardex, alertas de bajo stock |
| `AlmacenInteligente.Application` | Casos de uso detrás de un único `IMediador` que autoriza por permiso y valida; puertos de salida; matriz de roles |
| `AlmacenInteligente.Infrastructure` | Adaptadores de salida (EF Core + PostgreSQL, Identity, correo, PDF, archivos) |
| `AlmacenInteligente.Api` | REST (Minimal APIs) sobre el mismo mediador |
| `AlmacenInteligente.Web` | Blazor Server sobre el mismo mediador |

## Ejecutar la API y Blazor juntos

Doble clic en `ejecutar.cmd` (o ejecútalo desde una consola en la raíz del repo). Compila y abre dos ventanas:

- Blazor: http://localhost:5285
- API con Swagger: http://localhost:5228/swagger

En Visual Studio o Rider también puedes marcar `AlmacenInteligente.Web` y `AlmacenInteligente.Api` como proyectos de inicio múltiples.
El superadmin y el gerente tienen en Blazor el apartado **Administración → API web**: muestra si la API está en línea, genera un token
con su usuario y abre Swagger ya autorizado. La web y la API deben compartir la misma `Jwt:Clave`.

## Desarrollo sin Docker

Requisitos: SDK de .NET 10 y PostgreSQL local. Los `appsettings*.json` no se suben al repo: copia
`appsettings.example.json` como `appsettings.Development.json` en `src/AlmacenInteligente.Web` y en
`src/AlmacenInteligente.Api`, y llena la conexión, la `Jwt:Clave` (la misma en los dos) y el superadmin.

```bash
dotnet build
dotnet test
dotnet run --project src/AlmacenInteligente.Web
```

Al arrancar en Development, Blazor aplica las migraciones, crea los 4 roles con sus permisos y,
si no hay superadmin, lo crea con los datos de la sección `Superadmin` del mismo archivo.
Entra con ese correo y contraseña y da de alta a los demás usuarios en **Usuarios**.

Migraciones nuevas:

```bash
dotnet tool restore
dotnet ef migrations add NombreDelCambio -p src/AlmacenInteligente.Infrastructure -s src/AlmacenInteligente.Infrastructure -o Persistencia/Migraciones
```

## Correos e imágenes de productos

**Correos.** Los casos de uso no envían correos directamente: los agregados emiten eventos de dominio que se
guardan en la tabla `outbox` en la misma transacción que el cambio. Un proceso en segundo plano (corre en la API
y en la web; cada mensaje lo toma una sola con `FOR UPDATE SKIP LOCKED`) los convierte en correos cada 10 s
(`Outbox:IntervaloSegundos`). Si el envío falla se reintenta con espera creciente hasta 8 veces; el error queda en
`outbox.ultimo_error`.

| Evento | Correo |
|---|---|
| Producto pendiente de aprobación | Al gerente elegido, con botón a la solicitud |
| Producto aprobado / rechazado | Al encargado que lo dio de alta (con el motivo si se rechazó) |
| Existencia en o bajo el mínimo | A los usuarios activos con el permiso `alertas.bajostock` |
| Venta confirmada | Al cliente, con el comprobante PDF (`Notificaciones:ComprobanteVentaAlCliente`) |

El servicio se elige por configuración:

| Configuración | Servicio |
|---|---|
| `Correo:Azure:CadenaConexion` + `Correo:Remitente` | Azure Communication Services Email (producción) |
| `Correo:Smtp:Host` (+ `Puerto`, `Usuario`, `Contrasena`, `Ssl`) | SMTP, p. ej. Mailpit en `localhost:1025` |
| ninguno | Solo se escribe en el log (lo que pasa al correr sin Docker) |

`Notificaciones:UrlWeb` es la URL del Blazor para los botones de los correos.

Para ver los correos sin Docker, descarga [Mailpit](https://mailpit.axllent.org/docs/install/), córrelo (`mailpit`)
y arranca la web y la API con `Correo__Smtp__Host=localhost` y `Correo__Smtp__Puerto=1025`; la bandeja queda en
http://localhost:8025.

**Imágenes.** Se guardan siempre en Azure Blob Storage (`Archivos:Blob:CadenaConexion`), en el contenedor
`Archivos:Blob:Contenedor` (por defecto `productos`, privado; se crea solo). No hay respaldo en disco: sin esa
cadena la API y la web no arrancan. Se sirven por `/archivos/...` (web) y `/api/archivos/...` (API) solo con sesión.
En desarrollo `appsettings.Development.json` ya apunta a Azurite (`UseDevelopmentStorage=true`); sin Docker,
córrelo con `npx azurite-blob --skipApiVersionCheck` antes de subir imágenes.

## Desarrollo con Docker

```bash
docker compose up --build
```

Blazor en http://localhost:8080, API en http://localhost:8081, Mailpit en http://localhost:8026. Las imágenes van a Azurite.

## API para la app móvil

```bash
dotnet run --project src/AlmacenInteligente.Api
```

Swagger: http://localhost:5228/swagger (en desarrollo). Obtén un token con `POST /api/cuenta/token`, pulsa **Authorize** y pega el token.
La API expone los mismos casos de uso que Blazor (clientes, proveedores, productos, almacenes, existencias, kardex,
recepciones, ventas, traspasos, finanzas, reportes, movimientos, usuarios y PDF) con los mismos permisos. Los enums viajan como texto.

1. `POST /api/cuenta/token` con `{ "correo": "...", "contrasena": "..." }` devuelve un JWT, el nombre, los roles y los permisos del usuario.
2. Envía el token en `Authorization: Bearer <token>`.

| Endpoint | Devuelve |
|---|---|
| `GET /api/pdf/ventas/{id}` | Comprobante de venta en PDF |
| `GET /api/pdf/recepciones/{id}` | Comprobante de recepción en PDF |
| `GET /api/pdf/traspasos/{id}` | Comprobante de traspaso en PDF |
| `GET /api/finanzas/mensual?anio=2026` | Reporte mensual (JSON): Debe y Haber por mes de 3 años y saldo actual |
| `GET /api/pdf/finanzas/mensual?anio=2026` | Reporte mensual en PDF con gráficas |
| `GET /api/movimientos?desde=&hasta=` · `/api/movimientos/csv` · `/api/pdf/movimientos` | Movimientos de inventario (JSON, CSV, PDF) |
| `GET /api/pdf/reportes/{tipo}?desde=yyyy-MM-dd&hasta=yyyy-MM-dd` | Reporte en PDF con gráficas. `tipo`: General, Ventas, Compras, Inventario, Financiero o Margen |

Los errores vienen como ProblemDetails: 401 sin sesión, 403 sin permiso, 404 no existe, 409 regla de negocio, 422 datos inválidos.
La clave de firma va en `Jwt:Clave` (en producción por variable de entorno `Jwt__Clave`). Los datos de la empresa que salen en los PDF van en la sección `Empresa` de `appsettings.json`.

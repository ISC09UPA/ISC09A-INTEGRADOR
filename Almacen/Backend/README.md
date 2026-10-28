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

Requisitos: SDK de .NET 10 y PostgreSQL local. La conexión de desarrollo está en
`src/AlmacenInteligente.Web/appsettings.Development.json` (`Username=root`, base `almacen`).

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

## Desarrollo con Docker

```bash
docker compose up --build
```

Blazor en http://localhost:8080, API en http://localhost:8081, Mailpit en http://localhost:8026.

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

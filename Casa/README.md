# Casa Domótica

Proyecto integrador ISC09A. Esta carpeta contiene la parte de **Casa Domótica** dentro del monorepo.

## Estructura

| Carpeta | Contenido |
|---|---|
| `Backend/` | Servicios .NET: Identity, Home, Security, Assistant y Home.Functions. |
| `Frontend/mobile/` | Aplicación móvil Expo. |
| `Contracts/` | Contratos v1/v2, OpenAPI, fixtures y validadores. |
| `Scripts/` | Plantillas de inicio, pruebas, diagnóstico y despliegue. |
| `Documentacion/` | Arquitectura, contrato del dispositivo y estado académico. |
| `RaspberryPi/` | Agente `casa_pi` y prototipo físico. |
| `Entregables/` | Documentos y alcance de cada entregable. |

## Entregable 1 — Backend núcleo

Esta rama (`casa/nov13/backend-nucleo`) aporta:

- `Backend/Identity`: login, refresh, sesiones, roles, claves de servicio y JWKS.
- `Backend/Home`: casa, recursos, órdenes, telemetría, capturas y video privado.
- `Backend/Home.Functions`: ingesta autenticada desde IoT Hub.

## Compilar y probar

```bash
cd Casa/Backend/Identity
dotnet test CasaDomotica.Identity.sln --verbosity quiet

cd ../Home
dotnet test CasaDomotica.Home.sln --verbosity quiet
```

`Home.Functions` se compila con:

```bash
cd Casa/Backend/Home.Functions
dotnet build CasaDomotica.Home.Functions.csproj
```

## Reglas del repositorio

- No subir `secrets/`, `.env.local`, `local.settings.json`, `bin/`, `obj/`, `node_modules/` ni archivos generados.
- Las credenciales y cadenas de conexión se configuran fuera del repositorio.
- Los datos de prueba son fixtures aislados; no representan lecturas reales.
- No se declara funcionamiento físico de la Raspberry hasta la etapa de aceptación.

## Otros entregables

- Entregable 2: `Backend/Security` y `Backend/Assistant`.
- Entregable 3: `Frontend/mobile`.
- Entregable 4: `Contracts`, `Scripts`, `Documentacion` e integración.

Cada uno se integra en su propia rama y se fusiona en `casa/nov13/base`.

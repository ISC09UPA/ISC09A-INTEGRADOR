# Almacén inteligente · app móvil

App Expo (React Native, SDK 58) que consume la API REST de `api-web/` y replica la web de Blazor:
mismo inicio de sesión, menú según el rol y los mismos módulos y PDF.

| Sección | Pantallas |
| --- | --- |
| Operación | Ventas, Recepciones, Traspasos (lista con filtros, detalle, captura, cancelación y PDF) |
| Inventario | Existencias (stock mínimo e inicial), Movimientos (PDF y CSV), Kardex |
| Catálogos | Productos (alta, edición, imagen, activar/desactivar), Aprobaciones, Almacenes, Clientes, Proveedores |
| Análisis | Reportes (ventas, compras, inventario, flujo, margen; PDF por pestaña y general), Movimientos financieros y reporte mensual (PDF) |
| Administración | Usuarios (alta, rol, activo, restablecer contraseña) |

El menú y los botones se muestran con los permisos que trae el token (`/api/cuenta/token`);
la API vuelve a validarlos en cada llamada, igual que en la web.

## Requisitos

- Node 20 o superior y pnpm (`npm i -g pnpm`).
- La API corriendo (`api-web/ejecutar.cmd` o `dotnet run --project api-web/src/AlmacenInteligente.Api`).
- Para el teléfono: la app **Expo Go** (Android / iOS) en la misma red Wi-Fi que la PC.

## Ejecutar

```bash
cd my-app
pnpm install
pnpm start          # QR para Expo Go; "w" abre la versión web
```

### ¿A qué API se conecta?

Por defecto (se muestra debajo del botón "Entrar"):

- navegador y simulador de iOS → `http://localhost:5228`
- emulador de Android → `http://10.0.2.2:5228`
- teléfono con Expo Go → la IP de la PC que sirve Metro, puerto 5228

En el teléfono, la API debe escuchar en la red, no solo en localhost:

```bash
dotnet run --project api-web/src/AlmacenInteligente.Api --launch-profile red-local
```

(y permitir el puerto 5228 en el firewall de Windows). Para fijar otra dirección, copia `.env.example`
a `.env` y cambia `EXPO_PUBLIC_API_URL`.

## PDF

- Web: se abren en otra pestaña.
- Teléfono: se descargan a la caché y se abre el visor del sistema, desde donde se pueden guardar o compartir.

## Verificar

```bash
npx tsc --noEmit
npx eslint src
```

## Arquitectura

Las pantallas nunca llaman a la API directamente; pasan por capas:

```
pantalla ──► service (clase) ──► HttpClient ──► API
                 ▲                    │
                 │ model              │ DTO
                 └──── mapper ◄───────┘
```

- **HttpClient** (`api/http-client.ts`): clase única que arma la URL, agrega el token, traduce los
  errores (ProblemDetails) y devuelve el **DTO** tal como lo manda la API.
- **DTOs** (`dtos/*.dto.ts`, uno por archivo): el contrato de la API, con sus `null` y nombres.
- **Mappers** (`mappers/*.mapper.ts`): convierten DTO → model (y model → DTO para lo que se envía):
  `null` → opcional, `expira` → `Date`, referencias a documentos agrupadas, textos para selectores, IVA por pagar, etc.
- **Models** (`models/*.model.ts`, uno por archivo): lo que usan las pantallas.
- **Services** (`services/*.service.ts`, clases): un método por caso de uso; reciben y devuelven models.
  `services/index.ts` expone una instancia de cada uno sobre el mismo `HttpClient`.

```
src/
  app/          pantallas (Expo Router; cada archivo es una ruta)
  api/          HttpClient
  dtos/         contratos de la API (uno por archivo)
  models/       modelos de la app (uno por archivo)
  mappers/      DTO ↔ model
  services/     clases de servicio (cuenta, clientes, proveedores, productos, almacenes,
                ventas, recepciones, traspasos, movimientos, finanzas, reportes, usuarios, archivos)
  types/        enums de la API (texto)
  components/   piezas compartidas (UI, listas, documentos, selectores, gráficas)
  hooks/        carga de datos y colores
  lib/          sesión (contexto React), permisos y formato (dinero, fechas, folios)
  constants/    tema (misma paleta que Blazor)
```

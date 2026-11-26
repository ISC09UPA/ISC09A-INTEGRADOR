# App móvil — Casa Domótica

Aplicación Android/web con React Native 0.88, Expo SDK 58, Expo Router y TypeScript 6.

Consulta todos los servicios reales (`Identity`, `Home`, `Security`, `Notifications` y `Assistant`) y usa SignalR para invalidar estado. No inventa estados, lecturas ni transmisiones.

---

## 1. Ejecutar

Requisitos: Node compatible con Expo SDK 58.

```bash
cd Casa/Frontend/mobile
npm ci
npx expo start --web --port 8089
```

Android:

```bash
npm run android
```

Web:

```bash
npx expo start --web --port 8089
```

Usar el puerto **8089** porque los CORS de los servicios están configurados para:

```text
http://localhost:8089
http://127.0.0.1:8089
```

Si se usa otro puerto, añadir `Cors:Origins` en el backend.

---

## 2. Configuración

`.env.local` contiene solo URLs públicas:

```env
EXPO_PUBLIC_IDENTITY_URL=...
EXPO_PUBLIC_HOME_URL=...
EXPO_PUBLIC_SECURITY_URL=...
EXPO_PUBLIC_ASSISTANT_URL=...
EXPO_PUBLIC_NOTIFICATIONS_URL=...
```

- Nunca colocar claves, cadenas de conexión ni secretos en variables `EXPO_PUBLIC_*`.
- `.env.local` está fuera de Git.
- En emulador Android se puede usar `10.0.2.2`.
- En teléfono físico usar HTTPS o `adb reverse`.
- En web no se persiste el refresh token; al recargar hay que iniciar sesión.
- En Android/iOS el refresh token se guarda en Expo SecureStore.

---

## 3. Estructura

```text
app/
├── _layout.tsx
├── index.tsx
└── [screen].tsx
src/
├── core/
│   ├── api.ts          Cliente HTTP, errores tipados, refresh
│   ├── commands.ts     Seguimiento de órdenes e idempotencia
│   ├── events.ts       Eventos internos de la app
│   ├── homeState.ts    Frescura y fusión de estado
│   ├── models.ts       Tipos de dominio
│   ├── realtime.ts     Cliente SignalR JSON
│   ├── secureSession.ts SecureStore
│   └── state.tsx       Estado global
├── features/app/
│   ├── AppScreen.tsx   Rutas y navegación
│   ├── screens.tsx     Pantallas
│   ├── CameraPlayer.tsx
│   ├── ui.tsx
│   └── useResource.ts
├── providers/
└── shared/theme/
__tests__/
```

---

## 4. Pantallas

- Login.
- Aceptar invitación.
- Recuperar contraseña.
- Inicio.
- Luces.
- Seguridad.
- Cámara.
- Actividad.
- Accesos y puerta.
- Capturas.
- Detalle de captura/incidente.
- Personas autorizadas.
- Detalle de persona.
- Registro biométrico.
- Miembros.
- Ubicación.
- Preferencias.
- Asistente.
- Configuración.
- Más.

---

## 5. Integración

| Servicio | Funciones |
|---|---|
| Identity | Login, refresh, logout, `/me`, miembros, invitaciones, aceptación, contraseña |
| Home | Estado, luces, recursos, órdenes, comandos por clave, telemetría, seguridad, ubicación, capturas, video |
| Security | Modo, actividad, accesos, incidentes, capturas, preferencias, personas, biometría |
| Notifications | Preferencias, verificación de destinatario, correo de prueba, estado de entrega |
| Assistant | Preguntas de solo lectura y diagnóstico de modelo |
| SignalR | `/hubs/home/negotiate`, `stateChanged`, `permissionRevoked` |

### Comportamiento

- El refresh se renueva de forma atómica.
- Un `403` refresca permisos.
- Cambiar cuenta/casa/rol limpia cachés y órdenes.
- Un estado con más de 60 s bloquea controles.
- Tras reconectar SignalR se reconcilia por REST.
- Las órdenes ambiguas no se reenvían.
- La cámara solo muestra reproductor en estado `live`.
- Las capturas usan cabecera Bearer y no cachean.

---

## 6. Pruebas

```bash
npm test -- --runInBand --no-watchman
npm run typecheck
npm run lint
npx expo export --platform web --output-dir /private/tmp/casa-web
```

Resultado actual:

- 52/52 Jest.
- TypeScript sin errores.
- ESLint sin errores.
- Export web correcto.

---

## 7. Pendientes

- Generar APK instalable.
- Probar en emulador/teléfono: teclado, Atrás, texto escalado, HLS y desconexión.
- Verificar SignalR real en navegador/teléfono.
- Probar cámara/video con hardware real.
- Confirmar correo real y biometría cuando estén disponibles.
- Validar contra APIM cuando se importe.

# Casa Domótica — aplicación Android

React Native, Expo Router y TypeScript. La aplicación exige una sesión para entrar a las pantallas y muestra estados vacíos hasta recibir información de servicios reales. Identidad, Casa, Seguridad y el Asistente de lectura tienen APIs publicadas en Azure. La cámara usa sesiones privadas con playlist y segmentos HLS autenticados; sin cámara física no hay transmisión. Notificaciones, miembros y biometría siguen pendientes.

## Ejecutar

Usa las versiones de Node compatibles con `package.json` y Expo SDK 58. Ejecuta `npm ci`, `npm start` y abre Android con Expo; `npm run web` permite revisar la interfaz en navegador. Configura las variables de `.env.example` en un archivo `.env` local y reinicia Metro al cambiarlas. En navegador usa `localhost` y configura CORS del backend. En emulador Android usa `10.0.2.2`; en teléfono físico, la dirección accesible de tu servidor o `adb reverse` con localhost.

La copia de trabajo ya tiene `.env.local` (excluido de Git) con las URL HTTPS reales de Identidad, Casa y Asistente. Para desarrollar contra servicios locales, ajusta ese archivo; `.env.local` tiene prioridad sobre `.env`. Reinicia Metro para aplicar cambios. No añadas claves, tokens o contraseñas a variables `EXPO_PUBLIC_*`.

## Preparación para backend

- `src/core/api.ts`: HTTP, sesión, renovación, errores y rutas existentes de Identity/Home. No reintenta automáticamente órdenes físicas.
- `src/core/models.ts`: modelos y puertos de servicios pendientes. Registra el adaptador real con `configureExtensions` antes de montar la aplicación. `requestService` permite reutilizar HTTP autenticado.
- `src/core/events.ts`: invalidación de consultas al recibir notificaciones autorizadas. El adaptador `subscribe` gestiona la negociación y reconexión; después de reconectar debe invalidar para volver a consultar por REST.
- `src/features/app`: pantallas, formularios y controles. No contienen credenciales ni acceso directo a IoT Hub.

En Android, el token de renovación se guarda con Expo SecureStore; acceso y datos permanecen en memoria. En web, la sesión es solo en memoria. No se guardan contraseñas. El rol proviene del perfil autenticado; el servidor debe autorizar cada operación.

Lee [la guía de conexión y los pendientes](docs/INTEGRACION.md). No confundir interfaces preparadas con integración ya probada.

## Verificar

```sh
npm run typecheck
npm run lint
npm test -- --watchman=false --runInBand
```

Las respuestas controladas existen exclusivamente en pruebas unitarias; no hay adaptador simulado ni datos ficticios en la aplicación.

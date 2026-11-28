# Casa Domótica

Sistema de casa domótica con app móvil, backend .NET, Azure y agente para Raspberry Pi.

**Carpeta canónica:** `Casa`
**Última actualización:** 27 de noviembre de 2026

---

## Estado actual

| Área | Estado |
|---|---|
| Backend .NET | Implementado y desplegado en Azure |
| App móvil | Implementada, conectada a APIs reales y ejecutable en web/Android |
| Contratos | OpenAPI/JSON Schema validados |
| Agente de Raspberry `Device/agent` | En evaluación; 41/41 pruebas locales sin hardware |
| Agente histórico | Conservado como referencia; 78/78 pruebas |
| Hardware físico | Pendiente: Raspberry Pi 4B, cámara CSI, sensores y actuadores |
| APIM | Recurso creado, sin APIs importadas |
| APK Android | No generado |
| Correo real | ACS configurado; Identity aún no envía correos reales |
| Face | Bloqueado por aprobación de Limited Access |
| Ubicación real | Sin configurar (`404`) |

**No se presentan lecturas, imágenes, transmisiones ni ejecuciones físicas inventadas.**

---

## Arquitectura en una vista

```text
App Expo
  ├─ HTTPS + Bearer → Identity
  ├─ HTTPS + Bearer → Home
  ├─ HTTPS + Bearer → Security
  ├─ HTTPS + Bearer → Notifications
  ├─ HTTPS + Bearer → Assistant
  └─ SignalR → Home /hubs/home

Home → método directo IoT Hub → Raspberry Pi 4B
Raspberry Pi 4B → IoT Hub → Home.Functions → Home /internal/device-events
Home → Service Bus house-events-v1 → Security / Notifications
Home/Security → Blob privado → capturas, video y cuarentena
Security → Vision / Face
Notifications → ACS Email
Assistant → Azure OpenAI
```

Detalle completo en [Arquitectura](Documentacion/ARQUITECTURA.md).

---

## Componentes

| Componente | Tecnología | Ruta |
|---|---|---|
| Identity API | .NET 10, EF Core SQL Server, JWT ES256 | `Backend/Identity` |
| Home API | .NET 10, IoT Hub, SignalR, Blob, Service Bus | `Backend/Home` |
| Security API | .NET 10, Service Bus, Blob, Vision, Face | `Backend/Security` |
| Assistant API | .NET 10, Azure OpenAI | `Backend/Assistant` |
| Notifications Functions | Azure Functions .NET 10, ACS Email | `Backend/Notifications` |
| Home Functions | Azure Functions .NET 10, Event Hub | `Backend/Home.Functions` |
| App móvil | React Native 0.88, Expo SDK 58, TypeScript 6 | `Frontend/mobile` |
| Contratos | OpenAPI 3.1, JSON Schema 2020-12 | `contracts/v1`, `contracts/v2` |
| Agente de Raspberry | Python, enlace serie, SQLite, IoT Hub | `Device/agent` |
| Agente histórico | Python, Pi/QEMU, SQLite | `RaspberryPi` |
| Infra APIM | Bicep + manifiesto | `Infra/apim` |

---

## Lo que ya funciona

- Login, refresh rotatorio, logout, roles `admin`/`resident`, miembros e invitaciones.
- Control de luces, seguridad, puerta, capturas y video privado desde el backend.
- Actividad, incidentes, accesos, personas y biometría.
- Preferencias de notificaciones y verificación de destinatario.
- Asistente de solo lectura con historial acotado.
- App con todas las pantallas principales conectadas a APIs reales.
- SignalR con validación de casa/permiso y reconciliación REST.
- Ingesta de IoT Hub y cuarentena privada.
- Agente de Raspberry con `DeviceStateReported` v2, comandos y captura local.
- Servicios Azure con `/health` y `/ready` en 200.

Evidencia y pruebas en [Estado y pruebas](Documentacion/ESTADO_Y_PRUEBAS.md).

---

## Lo que falta

1. Importar APIM (`Scripts/deploy-apim.py --deploy`).
2. Generar y probar APK Android.
3. Verificar SignalR real en navegador/teléfono.
4. Implementar correo real en Identity y probarlo con autorización explícita.
5. Aprobar Azure Face Limited Access.
6. Configurar coordenadas reales de la casa.
7. Probar OpenAI con reportes reales.
8. Integrar el agente de Raspberry con Azure IoT.
9. Probar hardware: Raspberry Pi 4B, cámara CSI, sensores y actuadores.
10. Implementar o declarar pendientes: `StartVideo`, device twin y correlación C2D.
11. Agregar CI/CD.

Detalle y bloqueos en [Estado y pruebas](Documentacion/ESTADO_Y_PRUEBAS.md).

---

## Levantar la app

```bash
cd Casa/Frontend/mobile
npm ci
npx expo start --web --port 8089
```

Para que no muera por timeout:

```bash
tmux new-session -d -s casa-expo \
  'cd Casa/Frontend/mobile && BROWSER=none npx expo start --web --port 8089'
```

Abrir:

```text
http://localhost:8089
```

En web no se persiste el refresh token; para persistencia real usar Android/Expo Go.

Más detalles en [App móvil](Frontend/mobile/README.md).

---

## Backend local

Requiere Azure SQL, Key Vault, Azure SignalR, IoT Hub, Service Bus y Blob. No hay modo completamente offline ni Docker Compose.

Pasos, variables y orden de arranque en [Operación](Documentacion/OPERACION.md).

---

## Pruebas

```bash
dotnet test Casa/Backend/Home/CasaDomotica.Home.sln --verbosity quiet
dotnet test Casa/Backend/Identity/CasaDomotica.Identity.sln --verbosity quiet
dotnet test Casa/Backend/Security/tests/CasaDomotica.Security.Domain.Tests/CasaDomotica.Security.Domain.Tests.csproj --verbosity quiet
dotnet test Casa/Backend/Security/tests/CasaDomotica.Security.Infrastructure.Tests/CasaDomotica.Security.Infrastructure.Tests.csproj --verbosity quiet
dotnet test Casa/Backend/Assistant.Tests/CasaDomotica.Assistant.Tests.csproj --verbosity quiet
dotnet test Casa/Backend/Notifications/tests/CasaDomotica.Notifications.Tests/CasaDomotica.Notifications.Tests.csproj --verbosity quiet

cd Casa/contracts/v1
../.venv/bin/python tests/validate_contracts.py
../.venv/bin/python tests/validate_fixtures.py
../.venv/bin/python tests/matrix_check.py
../.venv/bin/python tests/adversarial_audit.py

cd ../../Frontend/mobile
npm test -- --runInBand --no-watchman
npm run typecheck
npm run lint

cd ../../..
Casa/.venv-device/bin/python Casa/Device/tests/test_agent_local.py
```

---

## Documentación

- [Índice de documentación](Documentacion/README.md)
- [Arquitectura](Documentacion/ARQUITECTURA.md)
- [Estado y pruebas](Documentacion/ESTADO_Y_PRUEBAS.md)
- [Operación](Documentacion/OPERACION.md)
- [Contratos](Documentacion/CONTRATOS.md)
- [Dispositivo](Documentacion/DISPOSITIVO.md)
- [App móvil](Frontend/mobile/README.md)
- [Raspberry virtual](RaspberryPi/virtual/README.md)

---

## Seguridad y operación

- No subir `secrets/`, `.env.local`, claves ni cadenas de conexión.
- No enviar correos reales sin autorización explícita.
- No insertar datos ficticios en Azure.
- No ejecutar migraciones ni `bootstrap-admin` contra Azure SQL sin revisión.
- No afirmar que el hardware funciona sin las pruebas físicas correspondientes.

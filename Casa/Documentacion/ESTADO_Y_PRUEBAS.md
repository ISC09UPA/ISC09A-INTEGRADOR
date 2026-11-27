# Estado y pruebas

**Última actualización:** 27 de noviembre de 2026
**Carpeta de referencia:** `Casa`

---

## 1. Resumen

El sistema tiene implementada la plataforma de software de las etapas 1–9; la etapa 10 está en curso. Los servicios están desplegados en Azure y la app está conectada a las APIs reales.

Lo pendiente se concentra en:

- Importación de APIM.
- APK Android y pruebas en teléfono/emulador.
- Correo real, Face, ubicación y OpenAI con datos reales.
- SignalR verificado en un cliente real.
- Integración Azure IoT del agente de Raspberry.
- Hardware físico y aceptación de Etapa 11.
- CI/CD.

---

## 2. Evidencia ejecutada

| Verificación | Resultado |
|---|---|
| Backend .NET | **130/130** |
| Home | 60/60 (2 dominio + 58 aplicación) |
| Identity | 35/35 (2 dominio + 33 aplicación) |
| Security | 23/23 (7 dominio + 16 infraestructura) |
| Assistant | 6/6 |
| Notifications | 6/6 |
| Home.Functions | Compila sin warnings ni errores |
| Contratos | **60/60** esquemas/OpenAPI, **121/121** fixtures, **75/75** vínculos, **6/6** adversariales |
| Móvil | **52/52** Jest, TypeScript y ESLint sin errores |
| Agente de Raspberry | **41/41** locales |
| Agente histórico | **78/78** |
| App web | `http://localhost:8089` → 200 |
| Health/ready Azure | Identity, Home, Security, Assistant, Notifications → 200 |

### Comprobaciones reales

- Login real: `200`.
- `/identity/me`: `200`, una casa.
- `GET /houses/{id}/status`: `404 noReport`.
- `GET /resources`: `200`, cero recursos.
- `GET /location`: `404 notFound`.
- Face: `200`, `available: false`, `faceNotConfigured`.
- Notifications email provider: `200 ready`.
- Assistant model: `200 ready`, `casa-asistente-nano`.
- APIM: existe `apim-casa-example`, pero `api list` está vacío.

Los estados vacíos y `404` son correctos sin dispositivo físico. No se inventaron lecturas.

---

## 3. Estado por etapa

| Etapa | Estado | Nota |
|---|---|---|
| 0 | Completada | Respaldo, diagnóstico y consolidación documental |
| 1 | Completada | Login, sesiones, roles y autorización |
| 2 | Completada sin hardware | Casa, sensores, órdenes, idempotencia e ingesta |
| 3 | Completada sin hardware | Seguridad, actividad, incidentes y Service Bus |
| 4 | Completada sin cámara | Capturas, Blob, Vision y retención |
| 5 | Completada sin cámara | Video HLS privado y reproducción autenticada |
| 6 | Software completo | Notificaciones; correo real pendiente |
| 7 | Software completo | Miembros, invitaciones y contraseña; correo real pendiente |
| 8 | Software completo | Biometría; Face bloqueado por aprobación |
| 9 | Software completo | Asistente y ubicación; datos reales pendientes |
| 10 | Parcial | SignalR, APIM y Android: APIM y APK pendientes |
| 11 | Pendiente | Raspberry y hardware |

---

## 4. Lo que falta

### 4.1 Software y externos

| Pendiente | Estado | Acción |
|---|---|---|
| APIM | Sin APIs importadas | `Scripts/deploy-apim.py --deploy` y verificar rutas |
| APK | No generado | Android SDK/Gradle o `eas build` |
| Android físico | No probado | Emulador/teléfono: teclado, Atrás, texto, HLS, desconexión |
| SignalR real | Solo Jest | Verificar negociación/reconexión/revocación |
| Correo Identity | `NullAccountEmailSender` | Implementar proveedor y probar con autorización |
| Face | Limited Access pendiente | Aprobación de Microsoft |
| Ubicación | 404 | Cargar coordenadas reales |
| OpenAI real | Sin reportes reales | Probar cuando existan datos de la casa |
| CI/CD | No existe | Workflow de build, tests, contratos, Jest, lint y agente |

### 4.2 Dispositivo

| Pendiente | Estado |
|---|---|
| Raspberry Pi 4B | No disponible |
| Cámara CSI | Pendiente |
| Cámara CSI, PIR, LDR, DHT11, servo y R307 | Verificados históricamente, no disponibles ahora |
| Ruta Azure IoT del agente de Raspberry | No verificada |
| `CaptureImage` contra Azure | Solo local |
| `StartVideo` | Backend listo; agente de Raspberry no |
| Device twin | Agente de Raspberry no; histórico sí |
| Correlación C2D | Sin decisión |
| `roomId` PIR/huella | Acoplado a `LightId` |
| Aceptación E2E | Pendiente |

### 4.3 Documentación y operación

- APIM sin APIs importadas.
- Rango de firewall de Notifications temporal.
- El APK no se ha generado.
- No hay CI/CD.
- Verificar que `secrets/` y `.env.local` sigan fuera de Git.

---

## 5. Riesgos

| Riesgo | Impacto | Mitigación |
|---|---|---|
| App Service Free / SQL serverless | Cold starts y 503 | Reintentar con timeout 120–180 s |
| Firewall SQL | `40615` | Regla exacta o rango temporal; revisar |
| Dos agentes coexistiendo | Confusión | Mantener el de Raspberry en evaluación; no reemplazar aún |
| `StartVideo` ausente | Video físico no funciona | Implementar o declarar bloqueo |
| Device twin ausente en agente de Raspberry | Configuración deseada no aplica | Usar variables o implementar twin |
| Correo real no probado | Invitaciones/reset no llegan | Implementar proveedor y probar |
| Face no aprobado | Biometría facial no disponible | Esperar aprobación |
| Hardware ausente | Etapa 11 bloqueada | No simular; dejar evidencia |
| Sin CI | Regresiones | Agregar workflow |

---

## 6. Verificación rápida

```bash
# Backend
dotnet test Casa/Backend/Home/CasaDomotica.Home.sln --verbosity quiet
dotnet test Casa/Backend/Identity/CasaDomotica.Identity.sln --verbosity quiet
dotnet test Casa/Backend/Security/tests/CasaDomotica.Security.Domain.Tests/CasaDomotica.Security.Domain.Tests.csproj --verbosity quiet
dotnet test Casa/Backend/Security/tests/CasaDomotica.Security.Infrastructure.Tests/CasaDomotica.Security.Infrastructure.Tests.csproj --verbosity quiet
dotnet test Casa/Backend/Assistant.Tests/CasaDomotica.Assistant.Tests.csproj --verbosity quiet
dotnet test Casa/Backend/Notifications/tests/CasaDomotica.Notifications.Tests/CasaDomotica.Notifications.Tests.csproj --verbosity quiet

# Contratos
cd Casa/contracts/v1
../.venv/bin/python tests/validate_contracts.py
../.venv/bin/python tests/validate_fixtures.py
../.venv/bin/python tests/matrix_check.py
../.venv/bin/python tests/adversarial_audit.py

# App
cd ../../Frontend/mobile
npm test -- --runInBand --no-watchman
npm run typecheck
npm run lint

# Agente de Raspberry
cd ../../..
Casa/.venv-device/bin/python Casa/Device/tests/test_agent_local.py
```

---

## 7. Criterio de cierre

No se declara terminado lo que depende de hardware, permisos externos o datos reales sin evidencia. Las pruebas locales no sustituyen la aceptación física ni el APK instalado.

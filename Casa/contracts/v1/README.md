# Contratos v1 — Primer flujo de luz (revisión C07)

> **Estado:** propuestos implementados para revisión. No se declaran "aprobados" hasta que el revisor los acepte.

Esta carpeta contiene los contratos versionados para el primer recorrido:

```text
App → Identity (login)
App → Home API → IoT Hub → Raspberry Pi → CommandResult → Home API → App
```

## Estructura

| Carpeta | Contenido |
|---|---|
| `openapi/` | Especificaciones OpenAPI 3.1.0 en JSON para Identity y Home APIs. |
| `schemas/` | JSON Schemas 2020-12 de mensajes y payloads con `$id` explícito. |
| `fixtures/` | Ejemplos válidos, negativos de negocio y mutantes de shape. |
| `tests/` | Validadores formales + matriz endpoint/security/schema/fixture cargada desde OpenAPI + auditoría adversarial. |
| `.venv/` | Entorno de desarrollo local aislado (excluido de commits). |
| `requirements-dev.txt` | Dependencias de validación reproducibles. |

## Decisiones reflejadas

- **HTTP 202 = pending**, nunca efecto aplicado.
- **CommandResult v1** incluye `eventId` estable, `correlationId`, `commandId`, `status`, `type`, `reasonCode`, `reason`, snapshot `{lightOn, lightingMode, source, appliedAtUtc}` y `stateRevision`. `source=device` = driver reportó éxito, **no** prueba óptica física.
- **EXECUTED obliga snapshot completo** y `stateRevision` entero no nulo; `reasonCode` null. Estados terminales de error (`rejected`, `expired`, `unknown`, `failed`) requieren `reasonCode` no nulo.
- **Pending** es estado de orden Home, no resultado ejecutado por Pi; `reasonCode` null, sin snapshot.
- **Twin v1** solo umbrales (`lightOnBelow`/`lightOffAbove`), `reportSeconds` y versión. No confirma comandos, no cambia modo de seguridad. `lightingMode` es campo legado: ignorar con advertencia, sin sobrescribir `lighting_mode` local.
- **Autorización:** único check usuario en `POST /identity/internal/authz/check` con `serviceBearerAuth`; body `userId`/`sessionId`/`houseId`/`action`. 200 solo `allowed`; 403 denegado/revocado; 503 indisponible. Online por request y antes del dispatch; sin cache permisiva.
- **JWT:**
  - Usuario: emisor `Identity`, audiencia explícita `["Identity.Public", "Home.Public"]`, claims `sub`/`sid`/`jti`/`scope`, access `expiresIn=300`, refresh `refreshExpiresIn=604800`.
  - Servicio Home → Identity: assertion ES256 firmada con clave privada Home (UserSecrets), pública Home pinned en Identity dev; assertion TTL ≤ 60 s. Token servicio emisor `Identity`, audiencia `Identity.Internal`, `sub=service:home`, `client_id=home`, `scope=identity:authorize`, `expiresIn=300`.
  - Rotación de firma (kid/overlap) separada de revocación de familias refresh.
- **Logout:** público (`security: []`), refresh token opaco en body, 204 idempotente.
- **Idempotencia:** clave requerida; replay devuelve mismo `commandId`/resultado; 409 si contenido difiere.
- **deviceId** requerido en orden a Pi como comprobación adicional de destino; identidad real viene del canal autenticado.
- **commandSequence** generado por Home, persistente por luz; Pi serializa.
- **Home command resource:** terminal requiere `result` objeto no nulo cuyo `status` coincida con el recurso. C10 invariante: `result.commandId` y `result.type` deben coincidir con el recurso (checker semántico).
- **IDOR:** Home resuelve house/light real, consulta Identity, devuelve 404 si no es miembro.

## Configuración del entorno de validación

```bash
cd contracts
python3 -m venv .venv
.venv/bin/python -m pip install -r requirements-dev.txt
```

No instala dependencias globales ni modifica el shell del usuario.

## Validación formal

```bash
cd contracts/v1
../.venv/bin/python tests/validate_contracts.py
../.venv/bin/python tests/validate_fixtures.py
../.venv/bin/python tests/matrix_check.py
../.venv/bin/python tests/adversarial_audit.py
```

La validación usa un registry offline por `$id`; **no hay fallback a red**. La matriz se carga desde los OpenAPI; `tests/matrix_links.json` contiene los enlaces explícitos endpoint/método/dirección/status/schema/fixture.

## Conteo actual

| Tipo | Cantidad |
|---|---|
| Schemas JSON Schema 2020-12 | 21 |
| OpenAPI 3.1.0 | 2 |
| Fixtures | 69 |
| Scripts de validación / auditoría | 4 |

## Matriz endpoint/security/schema/fixture

| Endpoint | Security | Request schema | Response schema / status |
|---|---|---|---|
| `POST /identity/auth/login` | public | `identity-login-request.json` | `identity-token-response.json` / 200 |
| `POST /identity/auth/refresh` | public | `identity-refresh-request.json` | `identity-token-response.json` / 200 |
| `POST /identity/auth/logout` | public | `identity-logout-request.json` | HTTP 204 |
| `GET /identity/me` | userBearerAuth | — | `identity-me-response.json` / 200 |
| `GET /identity/.well-known/jwks.json` | public | — | `identity-jwks.json` / 200 |
| `POST /identity/internal/token` | public | `identity-service-token-request.json` | `identity-service-token-response.json` / 200 |
| `POST /identity/internal/authz/check` | serviceBearerAuth | `authz-check-request.json` | `authz-check-response.json` / 200, `problem-details.json` / 401/403/503 |
| `GET /houses/{houseId}/status` | userBearerAuth | — | `home-status.json` / 200 |
| `POST /houses/{houseId}/lights/{lightId}/commands` | userBearerAuth | `light-command-request.json` | `home-command-response.json` / 202, `home-command-resource.json` / 200, `problem-details.json` / 409 |
| `GET /commands/{commandId}` | userBearerAuth | — | `home-command-resource.json` / 200 |

## Puertas pendientes

- Validación formal no reemplaza pruebas contra servicios reales.
- Consumer group dedicado para Event Hubs no existe (solo `$Default`).
- Azure Functions Core Tools, Azurite y Android SDK no están instalados.
- SQL real y IoT Hub data-plane requieren autorización de consumo.

## Cambios respecto a entregas anteriores de C07

- Matriz `matrix_check.py` reescrita para cargar operaciones OpenAPI y validar security/status/schema/contenido; ya no es una tabla hardcoded.
- Añadido `tests/matrix_links.json` (manifest de enlaces explícito) y `tests/adversarial_audit.py`.
- JWT claims separados de HTTP body: `identity/service-token-request.json` es body sintáctico; claims se prueban en `jwt-contract.json`.
- Añadidos fixtures `identity/logout-request.json`, `identity/service-token-request.json`, `authz/check-response-unauthorized-401.json`.
- `CommandResult`: añadido estado `failed`; `reasonCode` requerido para estados terminales de error y null para pending/executed.
- `home-command-resource.json`: terminal requiere `result` objeto no nulo con `status` coincidente; invariante C10 documentado.
- Añadidos mutantes para resource terminal-null, status-mismatch, commandId-mismatch, type-mismatch.
- Añadidos mutantes para command-result sin `reasonCode` y con `reasonCode` inconsistente.
- `validate_contracts.py` ahora resuelve punteros de fragmento internos (`#/$defs/...`).
- OpenAPI referencia `problem-details.json` directamente en lugar de `common.json#/$defs/problemDetails`.

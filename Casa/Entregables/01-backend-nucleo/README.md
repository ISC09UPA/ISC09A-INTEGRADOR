# Entregable 01 — Backend núcleo

## Objetivo

Aportar la base de servicios de Casa Domótica: identidad, control de casa, telemetría, órdenes, capturas y video privado, junto con el consumidor IoT de Azure Functions.

## Componentes

### Identity

- Login con correo y contraseña contra SQL.
- Hash de contraseñas con PBKDF2.
- Sesiones con refresh rotatorio, logout y revocación.
- JWT ES256 con audiencias explícitas.
- Roles `admin` y `resident`; autorización por casa y membresía.
- Claves de servicio separadas y JWKS.
- Límites de solicitudes para login y aserciones internas.

### Home

- Asociaciones casa/dispositivo/recurso.
- Estado aplicado, frescura y telemetría.
- Órdenes persistentes con idempotencia, secuencia y vencimiento.
- Despacho por IoT Hub sin repetir acciones físicas ambiguas.
- Inbox/outbox y cuarentena de eventos inválidos.
- Capturas privadas con clave temporal, validación y ETag.
- Video privado con sesiones breves, productor, segmentos MPEG-TS y HLS.

### Home.Functions

- Disparador de IoT Hub.
- Validación del contrato del dispositivo.
- Envío autenticado a Home.
- Cuarentena privada de eventos inválidos.

## Pruebas

- Home: 55 pruebas.
- Identity: 29 pruebas.
- Contratos compartidos: 37 esquemas, 99 fixtures y 46 vínculos de matriz.

## Límites

- No hay Raspberry ni sensores físicos conectados.
- No se declara video real ni captura de la vivienda.
- Las pruebas de persistencia usan SQLite aislado; la verificación real se hizo contra Azure SQL.
- Las credenciales y cadenas de conexión no están en el repositorio.

## Autor

Completar con el nombre del integrante responsable y la fecha de entrega.

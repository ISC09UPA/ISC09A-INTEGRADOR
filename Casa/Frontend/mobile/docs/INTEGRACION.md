# Conexión del frontend al backend

Actualizado el 12 de noviembre de 2026. Se trabaja en `/Users/karma007/integrador9nocuatri`; estos cambios aún no se han copiado ni subido al repositorio independiente `casa-domotica-app`.

## Ya preparado

Pantallas de inicio, login, luces, seguridad, actividad/filtros/paginación, personas/detalle/registro/revocación, puerta, cámara, capturas/detalle/análisis, ubicación, preferencias/correo de prueba, asistente, configuración y miembros/invitación de residentes. Tema marfil, azul oscuro y turquesa; barra inferior y rutas secundarias de Expo. No se muestran personas, sensores, incidentes ni resultados inventados.

Cliente HTTP para las rutas reales de los proyectos Identity y Home (la tabla del prototipo usa prefijos distintos: no copiarla sin reconciliar con OpenAPI):

| Servicio | Ruta bajo `/api/v1` |
| --- | --- |
| Identity | POST `/identity/auth/login`, `/identity/auth/refresh`, `/identity/auth/logout`; GET `/identity/me` |
| Home | GET `/houses/{houseId}/status`; POST `/houses/{houseId}/lights/{lightId}/commands`; GET `/commands/{commandId}` |

Contratos existentes: `contracts/v1/schemas`. Rol `admin` o `resident`. La casa procede de `/me`, usando inicialmente la primera membresía. El estado se consulta cada 15 segundos y al volver al primer plano. Un reporte con más de 60 segundos se presenta como antiguo y bloquea luces. La respuesta actual no incluye conectividad de Pi: disponibilidad de API no equivale a dispositivo conectado.

Se conserva el estado aplicado mientras una orden espera confirmación. HTTP 202 mantiene `pending`. Los resultados desconocidos o fallos de transporte no provocan reenvío. El registro de órdenes en memoria conserva el bloqueo al navegar. Si falta `commandId` tras una solicitud ambigua, se conserva el bloqueo: hace falta consulta por clave de idempotencia para resolverlo. Las órdenes pendientes del servidor deben incluirse en el estado de Home.

## Contratos pendientes de acordar mañana

`ServiceExtensions` define las entradas y salidas que necesita la UI. No son contratos de red ya aprobados ni endpoints inventados. Crear el adaptador remoto con los servicios de Seguridad, Notificaciones y Asistente y registrarlo con `configureExtensions`. Debe validar respuestas y devolver errores explícitos, nunca respuestas locales de éxito.

- Listas y detalles: identificadores, títulos/descripciones, tipo/resultado, paginación y filtros; fechas de ocurrencia y recepción independientes. Capturas mediante URL HTTPS privada temporal, nunca URL pública permanente.
- Sensores: identificador/nombre, valor/unidad, fecha de medición y recepción. Una notificación de SignalR invalida la consulta; el estado autorizado se vuelve a leer del backend.
- Seguridad/puerta: permisos efectivos, estado aplicado fechado y recurso de orden consultable. Reconciliar las órdenes sensibles pendientes antes de habilitar otra solicitud, incluso después de reiniciar o cambiar de sesión. El bloqueo local no sustituye la deduplicación del servidor.
- Registro biométrico: la UI recoge nombre, método y vigencia y presenta revisión. El puerto de sesión incluye consultar progreso, solicitar captura facial en casa y cancelar. El adaptador debe asociar cada captura y progreso del lector a esa sesión; falta definir los contratos de red y verificarlos con hardware. No se usa biometría del teléfono como sustituto.
- Cámara: inicio/cierre y captura independiente preparados. Incluye reproductor Expo Video para URL HTTPS privada HLS o progresiva. Falta conectar la negociación real; WebRTC necesita otro adaptador y transporte, que no se presupone disponible. No se muestra video ficticio ni se afirma que una sesión aceptada tiene video.
- Capturas/análisis/correo: diferenciar aceptación del trabajo y resultado final; actualizar mediante consulta/evento, sin afirmar entrega de correo en la bandeja. Fallar un servicio secundario no borra el incidente.
- Preferencias: GET/guardar con confirmación del servicio y destino verificado. El backend debe aplicar permisos de cada ajuste.
- Ubicación: coordenadas configuradas de casa; abre mapa externo real solo por acción del usuario y sin solicitar geolocalización del teléfono.
- Asistente: consulta de solo lectura con fecha de los datos; restringir herramientas en el backend.

No están implementados aquí los servicios, negociación SignalR, servidor/transporte de video ni adquisición biométrica en el dispositivo. Esas decisiones condicionan las conexiones restantes del frontend. No considerar estas funciones completas de punta a punta por tener pantallas.

## Verificación y límites

TypeScript, ESLint y pruebas unitarias verifican cliente HTTP, estados pendientes, ausencia de reenvío automático y bloqueo de duplicados. Las pruebas usan respuestas aisladas; la app no incorpora datos simulados. Exportación del bundle Android completada. Revisión web sin desbordamiento horizontal en 360 × 800 (15 rutas) y 393 × 852 (19 rutas); la validación de Android físico, botón Atrás, teclado y texto escalado queda pendiente de dispositivo/emulador disponible. SecureStore sigue la documentación oficial de Expo: https://docs.expo.dev/versions/latest/sdk/securestore/.

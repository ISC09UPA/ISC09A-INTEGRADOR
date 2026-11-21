# Autorización entre servicios

Identity admite aserciones ES256 con `client_id` fijo: `home`, `security`, `notifications` o `assistant`. Cada uno firma con su propia clave P-256 y Identity verifica la clave pública correspondiente, emisor, sujeto, audiencia `Identity.TokenEndpoint`, expiración, tiempo máximo de 60 segundos y `jti` único. Dos servicios no pueden tener la misma clave. Las claves privadas se almacenan por separado; cada servicio debe acceder exclusivamente a la suya.

El token emitido tiene audiencia `Identity.Internal`, sujeto `service:{client_id}` y alcance `identity:authorize`. Solo permite solicitar decisiones sobre acciones del propio servicio: Casa (`home:*` definido), Seguridad (`security:read`, `security:manage`), Notificaciones (`notifications:read`, `notifications:manage`) o Asistente (`assistant:read`). Identity niega acciones desconocidas y las de otro servicio.

Las decisiones siempre consultan sesión activa y membresía vigente de la casa. Los residentes pueden leer y controlar luces; el administrador también puede controlar puerta/modo de seguridad y gestionar seguridad, avisos y miembros. Un JWT del usuario incluye audiencias públicas explícitas, pero por sí solo no certifica pertenencia ni autorización para una casa: los servicios deben solicitar una decisión vigente. Una acción de gestión de miembros se reserva a Identity, no se expone como permiso consultable por los otros servicios.

La reproducción de video privado usa `home:read`; iniciar y cerrar una sesión de cámara usan `home:control:capture`, reservado al administrador. El productor de video también exige `home:control:capture` al preparar y confirmar segmentos.

Las APIs futuras no quedan autorizadas por existir una audiencia o una clave: deben validar JWT, consultar autorización y tener permisos de Key Vault específicos cuando se desplieguen. Las acciones no implementadas de los servicios continúan pendientes.

# Documentación del proyecto

Documentación vigente de Casa Domótica. Los planes, auditorías y runbooks históricos fueron consolidados aquí para evitar duplicados.

| Documento | Contenido |
|---|---|
| [Arquitectura](ARQUITECTURA.md) | Componentes, flujos, Azure, bases, autenticación y dispositivos. |
| [Estado y pruebas](ESTADO_Y_PRUEBAS.md) | Lo implementado, evidencia, pendientes, riesgos y etapas. |
| [Operación](OPERACION.md) | Entorno local, variables, despliegue, salud, migraciones y rollback. |
| [Contratos](CONTRATOS.md) | HTTP, eventos, autorización, SignalR, captura, video y dispositivo. |
| [Dispositivo](DISPOSITIVO.md) | Agente de Raspberry, agente histórico, instalación y QEMU. |

## Material de referencia

- `Documentacion/docs/arquitectura.mmd` — diagrama vigente de arquitectura.
- `RaspberryPi/docs/ARQUITECTURA.md` — notas del agente histórico.
- `RaspberryPi/VERIFICACION*.json` — evidencia conservada del agente histórico.

## Reglas

- No incluir secretos, tokens ni cadenas de conexión.
- No presentar datos simulados como producción.
- No afirmar funcionamiento físico sin evidencia.

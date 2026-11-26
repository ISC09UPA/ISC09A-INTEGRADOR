# Despliegue de la API (Azure SQL + Azure Container Apps)

## 1. Base de datos (Azure SQL)
1. Crea un servidor y una base `CiudadInteligente` (el nivel serverless/Basic alcanza).
2. Firewall del servidor: activa "Permitir servicios de Azure" (y tu IP para pruebas).
3. Cadena de conexion (Portal > Base de datos > Cadenas de conexion > ADO.NET) con tu usuario/contraseña.
4. Las tablas se crean solas al arrancar la API (`EnsureCreated`); la base debe existir.

## 2. Llaves: regenera las que se expusieron
Service Bus (root y api-integrador), llave de Communication Services Email, y las llaves
Training/Prediction de Custom Vision. Nunca van al repo: solo como secretos.

## 3. Variables / secretos (Container Apps: `--secrets` + `secretref:`)
| Variable | Para qué |
|---|---|
| `ConnectionStrings__CiudadInteligente` | Azure SQL |
| `Jwt__Clave` | 32+ caracteres al azar (si falta, las sesiones se pierden al reiniciar) |
| `Dispositivos__ApiKey` | llave de la cámara/simulador (`X-Api-Key`) |
| `Usuarios__ContrasenaAdmin` / `Usuarios__ContrasenaOperador` | contraseñas iniciales |
| `ServiceBus__ConnectionString`, `ServiceBus__Habilitado=true` | cola de eventos |
| `Correo__ConnectionString`, `Correo__Remitente`, `Correo__Destinatarios`, `Correo__Habilitado=true` | correo de emergencias |
| `Esp32__Transporte=IoTHub`, `Esp32__IoTHub__ServiceConnectionString`, `Esp32__IoTHub__EventHubConnectionString` | control del ESP32 desde la nube |
| `Vision__PredictionUrl`, `Vision__PredictionKey` (`Vision__Umbral=0.65`) | conteo por foto |
| `Cors__Origenes` | orígenes permitidos de la versión web (coma); vacío = cualquiera |

`Esp32__Transporte=Serial` solo funciona con el ESP32 conectado por USB a la misma máquina: en la nube se usa `IoTHub`.

## 4. Construir y publicar
```
cd Backend
az acr build -r <registro> -t viaclara-api:1 .
az containerapp up ... --ingress external --target-port 8080
```
Sondeo de salud: `GET /health`. Detrás del proxy de Azure se usan `X-Forwarded-*`, así que el límite por IP funciona.

## 5. Probar
- `GET https://<app>/api/publico/cruce` (sin cuenta)
- `POST /api/vision/conteo` (multipart, campo `imagen`, header `X-Api-Key`): 4 franjas verticales = Norte, Sur, Este, Oeste.

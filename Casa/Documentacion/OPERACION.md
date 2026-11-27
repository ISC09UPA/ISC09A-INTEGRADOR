# Operación

**Última actualización:** 27 de noviembre de 2026
**Carpeta canónica:** `Casa`

---

## 1. Requisitos

| Herramienta | Uso |
|---|---|
| .NET SDK 10.0.1xx+ | Backend |
| Node.js 20/22 LTS recomendado | Expo/React Native |
| Python 3.10+ | Contratos y agente |
| Azure CLI 2.9x+ | Azure, Key Vault, SQL, IoT |
| Azure Functions Core Tools v4 | Functions local |
| Android SDK/Gradle o EAS CLI | APK |
| QEMU | Raspberry virtual |
| ffmpeg | Cámara v4l2 del agente |

---

## 2. Estructura local

```text
Casa/
├── Backend/
├── contracts/
├── Device/
├── Documentacion/
├── Frontend/
│   └── mobile/
├── Infra/
├── RaspberryPi/
│   └── virtual/
├── Scripts/
└── .venv-device/     # local, fuera de Git
```

`secrets/` y `.env.local` no se versionan. No imprimir su contenido.

---

## 3. App móvil

```bash
cd Casa/Frontend/mobile
npm ci
npx expo start --web --port 8089
```

Para que sobreviva al entorno:

```bash
tmux new-session -d -s casa-expo \
  'cd Casa/Frontend/mobile && BROWSER=none npx expo start --web --port 8089'
```

- URL: `http://localhost:8089`.
- CORS configurado para `localhost:8089` y `127.0.0.1:8089`.
- En web no se persiste el refresh token.
- En Android usar `npm run android`.
- En teléfono físico usar HTTPS o `adb reverse`.

Detalle en [App móvil](../Frontend/mobile/README.md).

---

## 4. Backend local

El backend depende de Azure SQL, Key Vault, SignalR, IoT Hub, Service Bus y Blob. No hay un modo completamente offline ni Docker Compose.

### 4.1 Azure CLI

```bash
az login
az account show --query '{name:name,id:id,state:state}' -o json
```

### 4.2 Firewall SQL temporal

```bash
IP=$(curl -s https://api.ipify.org)
az sql server firewall-rule create \
  --resource-group rg-casa-example \
  --server sql-casa-example \
  --name dev-local \
  --start-ip-address "$IP" \
  --end-ip-address "$IP"
```

Eliminar la regla al terminar. No dejarla abierta.

### 4.3 Variables

El backend usa variables ASP.NET con doble guion bajo. Las claves reales están en Key Vault y en `secrets/`; no imprimirlas.

| Servicio | Variables mínimas |
|---|---|
| Identity | `ConnectionStrings__IdentityDb`, `Jwt__PrivateKeyPem`, `Jwt__KeyId` |
| Home | `ConnectionStrings__HomeDb`, `ConnectionStrings__SignalR`, `Jwt__PublicKeyPem`, `Jwt__KeyId` |
| Security | `ConnectionStrings__SecurityDb`, `Jwt__PublicKeyPem`, `Jwt__KeyId`, `Identity__BaseUrl`, `Identity__ServicePrivateKeyPem`, `Home__BaseUrl`, `ServiceBus__Namespace` |
| Assistant | `Services__IdentityUrl`, `Services__HomeUrl`, `Services__SecurityUrl`, `OpenAI__Endpoint`, `OpenAI__Deployment` |
| Notifications | `ConnectionStrings__NotificationsDb`, `Jwt__PublicKeyPem`, `Jwt__KeyId`, `Identity__BaseUrl`, `Identity__ServicePrivateKeyPem`, `ServiceBusConnection__fullyQualifiedNamespace`, `AzureWebJobsStorage`, `Email__ConnectionString`, `Email__SenderAddress` |
| Home.Functions | `Home__BaseUrl` (HTTPS), `Ingestion__PrivateKeyPem`, `Storage__AccountUrl`, `IoTHubEventHubName`, `IoTHubEvents`, `IoTConsumerGroup`, `AzureWebJobsStorage` |

Patrón para obtener un secreto sin imprimirlo:

```bash
export ConnectionStrings__IdentityDb="$(az keyvault secret show --vault-name kv-casa-example --name identity-db-connection --query value -o tsv)"
```

### 4.4 Arranque

```bash
dotnet run --project Casa/Backend/Identity/src/CasaDomotica.Identity.Api/CasaDomotica.Identity.Api.csproj --urls http://localhost:5001
dotnet run --project Casa/Backend/Home/src/CasaDomotica.Home.Api/CasaDomotica.Home.Api.csproj --urls http://localhost:5002
dotnet run --project Casa/Backend/Security/src/CasaDomotica.Security.Api/CasaDomotica.Security.Api.csproj --urls http://localhost:5004
dotnet run --project Casa/Backend/Assistant/CasaDomotica.Assistant.Api.csproj --urls http://localhost:5003
```

Notas:

- Home falla si falta `ConnectionStrings__SignalR`.
- Identity falla si falta una clave P-256 válida.
- Assistant exige URLs HTTPS para Identity/Home/Security/OpenAI.
- Home.Functions exige HTTPS para Home; no puede apuntar a `http://localhost:5002`.

---

## 5. Functions local

Requiere `func`:

```bash
brew tap azure/functions
brew install azure-functions-core-tools@4
```

Cada Function App necesita su `local.settings.json` completo. Los ejemplos mínimos no bastan para operar.

Notifications:

```bash
cd Casa/Backend/Notifications/src/CasaDomotica.Notifications.Functions
func start --port 7071
```

Home.Functions:

```bash
cd Casa/Backend/Home.Functions
func start --port 7072
```

El `local.settings.json` de Home.Functions debe incluir `Home__BaseUrl` (HTTPS), `Ingestion__PrivateKeyPem`, `Storage__AccountUrl`, `IoTHubEventHubName`, `IoTHubEvents`, `IoTConsumerGroup` y `AzureWebJobsStorage`.

Si ambas Function Apps corren a la vez, usar 7071 para Notifications y 7072 para Home.Functions.

---

## 6. Migraciones y administrador

Solo contra una base nueva o con autorización explícita:

```bash
dotnet run --project Casa/Backend/Identity/src/CasaDomotica.Identity.Api/CasaDomotica.Identity.Api.csproj -- --migrate
dotnet run --project Casa/Backend/Home/src/CasaDomotica.Home.Api/CasaDomotica.Home.Api.csproj -- --migrate
dotnet run --project Casa/Backend/Security/src/CasaDomotica.Security.Api/CasaDomotica.Security.Api.csproj -- --migrate
```

Administrador:

```bash
export Bootstrap__Email="..."
export Bootstrap__Password="..."
export Bootstrap__HouseName="Casa Domótica"
dotnet run --project Casa/Backend/Identity/src/CasaDomotica.Identity.Api/CasaDomotica.Identity.Api.csproj -- --bootstrap-admin
```

No ejecutar contra la Azure SQL compartida sin revisar.

---

## 7. Despliegue Azure

Orden recomendado:

```bash
python3 Casa/Scripts/deploy-identity.py --deploy
python3 Casa/Scripts/deploy-home.py --deploy
python3 Casa/Scripts/deploy-home-functions.py --deploy
python3 Casa/Scripts/deploy-security.py --deploy
python3 Casa/Scripts/deploy-notifications.py --deploy
python3 Casa/Scripts/deploy-assistant.py --deploy
python3 Casa/Scripts/deploy-apim.py --deploy
```

`deploy-home-functions.py` requiere Home, IoT Hub y Key Vault ya desplegados; crea el consumer group `home-functions-v1`, el contenedor `iot-quarantine` y publica la Function App de ingesta.

Los scripts conservan diagnósticos protegidos en `secrets/`. No imprimirlos sin sanear.

---

## 8. Salud y diagnóstico

```bash
curl -s https://identity-api.example.com/health
curl -s https://home-api.example.com/health
curl -s https://security-api.example.com/health
curl -s https://assistant-api.example.com/health
curl -s https://notify-api.example.com/health
```

`/ready` comprueba dependencias. En Free/Serverless puede tardar 30–120 s.

Diagnósticos autenticados:

- `GET /api/v1/security/houses/{houseId}/diagnostics/face`
- `GET /api/v1/security/houses/{houseId}/diagnostics/bus`
- `GET /api/v1/security/houses/{houseId}/diagnostics/capture-storage`
- `GET /api/v1/notifications/houses/{houseId}/diagnostics/email-provider`
- `GET /api/v1/assistant/houses/{houseId}/diagnostics/model`

---

## 9. SQL y firewall

- Servidor: `sql-casa-example`.
- Bases: `IdentityDb`, `HomeDb`, `SecurityDb`, `NotificationsDb`.
- Usuarios de ejecución: `identity_api`, `home_api`, `security_api`, `notifications_api` (según despliegue).
- `40615` significa IP de cliente no permitida.
- Notifications Flex Consumption rota IPs; se usa un rango temporal de la plataforma.
- Revisar y reducir reglas cuando se estabilice la salida.

---

## 10. Rollback

- Código: desplegar el artefacto anterior con el script correspondiente.
- SQL: las migraciones son aditivas; no borrar tablas ni datos sin revisión.
- APIM: reaplicar el deployment anterior o usar el rollback del script.
- Móvil: reconstruir desde el tag/versión anterior.
- No revertir con `git reset` ni reescribir historial.

---

## 11. Precauciones

- No subir `secrets/`, `.env.local`, claves ni cadenas.
- No enviar correos reales sin autorización.
- No insertar datos ficticios en Azure.
- No dejar el firewall abierto.
- No afirmar que el hardware funciona sin pruebas.
- No ejecutar operaciones Azure en paralelo si saturan el plan gratuito.

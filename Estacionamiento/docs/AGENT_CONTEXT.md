# AGENT CONTEXT: AeroPark · Estacionamiento Inteligente de Aeropuerto
> **Documento de Contexto Rápido para Agentes de IA y Subagentes.**  
> *Versión:* 2.1 · Octubre 2026 · *Marca Oficial:* **AeroPark** (The Delta Apex)  
> *Enfoque:* 100% Cashless, Seguridad Criptográfica & Estilo Minimalista Apple (`apple.com`)  
> *Manual de Marca:* [BRAND_GUIDELINES.md](file:///docs/BRAND_GUIDELINES.md)  
> *Repositorio GitHub Oficial:* https://github.com/AlejandroMechE/-SmartParkingLotAirports.git

---

## 1. Misión y Marca Oficial (AeroPark)
- **Marca:** **AeroPark** (*The Delta Apex* - Emblema alar supersónico blanco puro `#FFFFFF` sobre fondo 100% transparente como overlay).
- **Tagline:** *Seamless Airport Parking · Tap, Park, Fly*
- **Cero Efectivo:** No existen cajeros físicos ni casetas de cobro con dinero en efectivo.
- **Acceso Ágil LPR:** Detección vehicular por lazo inductivo y cámara IP en carril edge (Raspberry Pi), lectura de placa por OCR en $\le 500\text{ ms}$, apertura de pluma en $\le 3\text{ s}$.
- **Cobro en Línea:** 
  - *Usuarios con app / reserva:* Cobro automático al salir contra Apple Pay o tarjeta guardada.
  - *Usuarios ocasionales:* Escaneo de QR dinámico en pantalla LED de carril que abre web express (**C-34**) con checkout Apple Pay en 1 toque.
- **Seguridad y Reportes Policiales:** Consulta a base policial mediante capa anticorrupción (Police Gateway) con dictamen humano obligatorio. La pluma jamás se bloquea sola.

---

## 2. Stack Tecnológico Mandatorio

| Capa | Tecnología | Características Clave |
| :--- | :--- | :--- |
| **Backend** | **.NET 10 (LTS)** | ASP.NET Core Minimal APIs, Clean Architecture, principios SOLID, CQRS con MediatR, FluentValidation |
| **Base de Datos** | **Azure Database for MySQL (Flexible Server)** | Transaccional ACID (InnoDB), Read Replicas para desacoplar lecturas/reportes, Connection Pooling con MySqlConnector, EF Core 10 con Pomelo |
| **Frontend Móvil** | **React Native** (iOS & Android) | Expo SDK 51+ / React Native CLI, integración profunda con hardware (cámara MLKit, biometría Face ID/Touch ID/BiometricPrompt, Apple Pay PassKit, Google Pay, SecureStore) |
| **Frontend Web** | **React Native for Web / React** | Panel admin de escritorio ($\ge 1280\text{ px}$) con retículas Bento Grid estilo macOS / apple.com |
| **Edge Hardware** | **Python 3.11+** (Raspberry Pi 5) | Sensor inductivo, captura YOLO ONNX, OCR local de contingencia, cola local SQLite FIFO offline, comunicación mTLS |
| **Infraestructura** | **Microsoft Azure** | Azure Container Apps (stateless), Azure Cache for Redis, Azure Blob Storage (evidencias), Azure Key Vault (claves criptográficas), IoT Hub |

---

## 3. Reglas Inmutables de Seguridad y Criptografía

1. **Contraseñas: Irreversibilidad y No Descifrabilidad Absoluta:**
   - **Bajo ninguna circunstancia se pueden descifrar las contraseñas.** Queda estrictamente prohibido el almacenamiento en texto plano o con cifrado reversible (simétrico/asimétrico). No existe clave de desencriptación ni función inversa.
   - **Algoritmo:** **Argon2id** (memoria $64\text{ MB}$, $\ge 3$ iteraciones, $4$ hilos).
   - **Salt Criptográfico:** Sal aleatoria e irrepetible de **128 bits** (`RandomNumberGenerator.GetBytes(16)`) generada con CSPRNG por cada usuario.
   - **Validación:** Comparación exclusiva en tiempo constante (`CryptographicOperations.FixedTimeEquals`) para anular ataques de temporización (*Timing Attacks*).
2. **Cifrado Integral de Información Sensible:**
   - **En Tránsito:** **TLS 1.3** obligatorio con encabezados HSTS en todas las comunicaciones. Autenticación mutua (**mTLS**) para nodos edge y Police Gateway.
   - **En Reposo:** Transparent Data Encryption (**TDE**) en Azure Database for MySQL con claves administradas en **Azure Key Vault**.
   - **Cifrado por Columna (Application-Level Encryption):** Toda información PII o sensible (teléfonos, correos en contextos específicos, detalles de alertas de seguridad) se cifra con **AES-256-GCM** en .NET antes de insertarse en MySQL.
   - **En Dispositivo Móvil:** Tokens de sesión y credenciales protegidos mediante hardware en **iOS Keychain** (Secure Enclave) y **Android Keystore** (TEE) vía `expo-secure-store`.
   - **Evidencias Fotográficas:** Azure Blob Storage con SSE AES-256 y acceso exclusivo mediante **SAS Tokens** efímeros de 15 minutos.

---

## 4. Filosofía de Diseño: Sitios Web de Apple (apple.com) & Apple HIG

- **Minimalismo Radical:** Protagonismo absoluto del contenido, eliminación de ruido decorativo y generoso espacio negativo (*whitespace*).
- **Tipografía Monumental:** **SF Pro Display** para encabezados audaces con interletrado negativo (`letter-spacing: -0.025em`), subtítulos elegantes en `#86868B`, *Eyebrows* en azul Apple (`#0071E3`) y **SF Mono** para placas vehiculares y códigos.
- **Retículas Bento Grid Asimétricas:** Módulos de datos con radios continuos *Squircle* ($24\text{ px}$ a $32\text{ px}$) y bordes milimétricos ultrafinos de $0.5\text{ px}$.
- **Navegación Superior Translúcida:** Barra adhesiva de $44\text{ px}$ - $48\text{ px}$ con cristal esmerilado (*Frosted Glass* `backdrop-filter: blur(20px)` y transparencia al $80\%$).
- **Botones en Píldora (*Pill CTAs*):** Botón primario redondeado (`border-radius: 980px`) en azul Apple (`#0071E3`) o negro puro (`#000000`), con microanimación de escala táctil ($1.02\times$). Enlaces secundarios con chevron animado (`›`).
- **Paleta Cromática:**
  - *Light:* Blanco `#FFFFFF`, gris cerámico `#F5F5F7`, texto negro carbón `#1D1D1F`.
  - *Dark:* Negro OLED `#000000`, gris espacial `#161617`, grafito `#1D1D1F`, texto blanco `#F5F5F7`.
  - *Acentos:* Azul Apple (`#0071E3` / `#2997FF`), Verde Éxito (`#34C759`), Rojo Alerta (`#FF3B30`).
- **Físicas:** Curva de aceleración cúbica `cubic-bezier(0.16, 1, 0.3, 1)` y físicas de resorte en móvil.

---

## 5. Arquitectura del Backend (.NET 10 Clean Architecture)

```
AirportParking.sln
├── src/
│   ├── Core/
│   │   ├── AirportParking.Domain/
│   │   │   ├── Entities/               # Customer, Vehicle, Booking, Stay, Tariff, Incident, SecurityAlert
│   │   │   ├── ValueObjects/           # PlateNumber, Money, DateRange
│   │   │   └── Interfaces/             # IRepository<T>, IUnitOfWork
│   │   └── AirportParking.Application/
│   │       ├── Features/               # Casos de uso CQRS (Commands & Queries con MediatR)
│   │       ├── Common/Interfaces/      # ICryptographyService, IPasswordHasher, ICurrentUserService
│   │       └── Behaviors/              # ValidationBehavior (FluentValidation), LoggingBehavior
│   ├── Infrastructure/
│   │   └── AirportParking.Infrastructure/
│   │       ├── Persistence/            # ApplicationDbContext (EF Core 10 Pomelo MySQL)
│   │       ├── Cryptography/           # Aes256GcmService, Argon2idPasswordHasher
│   │       ├── Services/               # AzureKeyVaultService, BlobStorageService, StripePspClient
│   │       └── BackgroundJobs/         # ReconcileShiftsWorker, SyncTelemetryWorker
│   └── Presentation/
│       └── AirportParking.Api/
│           ├── Endpoints/              # Minimal APIs organizadas por dominio (/auth, /access, /bookings, /payments)
│           ├── Hubs/                   # SignalR Hubs para carril en vivo y alertas de seguridad
│           └── Middleware/             # ErrorHandlingMiddleware (RFC 7807 ProblemDetails), SecurityHeaders
└── tests/
    ├── AirportParking.UnitTests/
    └── AirportParking.IntegrationTests/
```

---

## 6. Mapa Documental del Repositorio

- **`docs/PLAN_DE_DESARROLLO.md`:** Plan de desarrollo integral, objetivos MoSCoW, 12 épicas e historias de usuario, ADRs (ADR-01 a ADR-14), especificación técnica de carril Raspberry Pi, matriz de riesgos y SLOs.
- **`docs/DETALLE_PAGINAS_Y_FLUJOS.md`:** Especificación exhaustiva de las **34 pantallas de la App Cliente (C-01 a C-34)** y **40 pantallas del Panel Admin (A-01 a A-40)**, diagramas de secuencia Mermaid, flujos paso a paso y matriz háptica.
- **`docs/PLAN_DE_IMPLEMENTACION.md`:** Plan de scaffolding y ejecución de la estructura del proyecto.
- **`docs/AGENT_CONTEXT.md`:** Este documento (contexto inmediato de alta densidad).

---

## 7. Directrices de Git y Seguridad para Agentes de IA

- **Repositorio Remoto Oficial:** `https://github.com/AlejandroMechE/-SmartParkingLotAirports.git`
- **Rama Principal:** `main`
- **Convención de Commits:** Commits semánticos en minúsculas en español o inglés (`feat:`, `fix:`, `docs:`, `refactor:`, `chore:`, `test:`).
- **Regla Estricta de Cero Fugas de Secretos:**
  - NUNCA agregar al control de versiones archivos `.env`, certificados (`.pem`, `.pfx`, `.crt`, `.key`), cadenas de conexión con contraseñas en texto claro o secretos de Azure Key Vault.
  - El archivo `.gitignore` debe auditarse antes de cualquier `git add .`.
- **Validación Continua:** Todo cambio de código debe compilar (`dotnet build backend/AirportParking.slnx` con 0 errores) antes de crearse un commit.

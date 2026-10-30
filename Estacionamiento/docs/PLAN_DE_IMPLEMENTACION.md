# Plan Experto de Implementación: Estructura y Scaffolding del Proyecto

**Proyecto:** Estacionamiento Inteligente de Aeropuerto (100% Cashless & Operación Digital)  
**Estilo Visual:** Minimalismo refinado inspirado en los sitios web de Apple (apple.com) & Apple HIG  
**Stack Principal:** .NET 10 (LTS) · React Native (Móvil & Web) · Azure Database for MySQL (Flexible Server) · Python 3.11 (Edge)  
**Seguridad:** Cifrado integral (TLS 1.3, TDE, AES-256-GCM) y Contraseñas irreversibles (Argon2id)  

---

## 1. Objetivos del Plan

1. **Centralización de Contexto para Agentes de IA (`/docs`):** Agrupar todos los archivos de especificación en un directorio dedicado y crear un archivo de entrada rápida (`docs/AGENT_CONTEXT.md`) para que cualquier subagente o modelo de lenguaje absorba el 100% del contexto técnico, de negocio y de seguridad en segundos.
2. **Scaffolding de Backend en .NET 10 (Clean Architecture):** Generar la solución (`AirportParking.sln`) con separación en capas puras: Domain, Application (CQRS con MediatR), Infrastructure (EF Core 10 + Pomelo MySQL, servicios criptográficos AES-256-GCM y Argon2id) y Presentation (Minimal APIs + SignalR).
3. **Scaffolding de Frontend en React Native:** Estructurar el espacio para la aplicación móvil cliente (`frontend/customer-mobile`) con soporte nativo de hardware (cámara MLKit, biometría Face ID/Touch ID/BiometricPrompt, Apple Pay/Google Pay y SecureStore) y la consola administrativa (`frontend/admin-web`) con estética Bento Grid estilo apple.com y macOS.
4. **Entorno Edge en Python 3.11 con Entorno Virtual (`.venv`):** Crear el espacio para el agente de carril edge en Raspberry Pi, aprovisionando su entorno virtual aislado (`edge/.venv`), dependencias (`requirements.txt`) para YOLO, SQLite offline y comunicación mTLS.
5. **Persistencia MySQL & DevOps Local:** Inicializar scripts DDL/Seed para Azure Database for MySQL (Flexible Server) y configuración de contenedores Docker (`docker-compose.yml`) para desarrollo y pruebas locales.

---

## 2. Mapa Estructural del Repositorio

```
Integrador/
├── .gitignore                          # Reglas completas para .NET, Node, Python .venv, Expo, Docker
├── README.md                           # Índice maestro del repositorio con links a docs
├── docker-compose.yml                  # Entorno local: MySQL 8.0 + Redis + Adminer
│
├── docs/                               # 📁 Contexto Unificado para Agentes de IA
│   ├── AGENT_CONTEXT.md                # ⚡ Resumen de alta densidad para ingesta rápida de IA
│   ├── PLAN_DE_DESARROLLO.md           # Plan maestro de arquitectura, ADRs y sprints
│   ├── DETALLE_PAGINAS_Y_FLUJOS.md     # Catálogo de 74 pantallas, diagramas y microinteracciones
│   └── PLAN_DE_IMPLEMENTACION.md       # Este documento de planificación y scaffolding
│
├── backend/                            # 📁 Solución .NET 10 LTS (Clean Architecture)
│   ├── AirportParking.sln
│   ├── src/
│   │   ├── Core/
│   │   │   ├── AirportParking.Domain/          # Entidades puras, Value Objects, Excepciones
│   │   │   └── AirportParking.Application/     # CQRS (MediatR), DTOs, Validaciones (FluentValidation)
│   │   ├── Infrastructure/
│   │   │   └── AirportParking.Infrastructure/  # EF Core Pomelo MySQL, Cryptography (AES-256/Argon2id), KeyVault
│   │   └── Presentation/
│   │       └── AirportParking.Api/             # Minimal APIs, SignalR Hubs, Endpoint Filters, Swagger
│   └── tests/
│       ├── AirportParking.UnitTests/           # Pruebas unitarias de dominio y lógica
│       └── AirportParking.IntegrationTests/    # Pruebas de integración con Testcontainers / MySQL
│
├── frontend/                           # 📁 Aplicaciones React Native & Web
│   ├── customer-mobile/                # App Cliente (iOS & Android) - React Native / Expo
│   │   ├── package.json
│   │   ├── app.json
│   │   ├── tsconfig.json
│   │   └── src/
│   │       ├── assets/                 # SF Pro fonts, SVGs, iconos
│   │       ├── components/             # Bento Cards, Pill Buttons, Squircle Sheets
│   │       ├── features/               # Módulos: auth, access, booking, payment, vehicles
│   │       ├── services/               # API Client, Biometrics, SecureStore, Apple Pay
│   │       └── theme/                  # Tokens apple.com: Light Canvas, Dark Canvas, Tipografía
│   └── admin-web/                      # Panel de Supervisión Escritorio (Bento Grid / macOS Style)
│       └── package.json
│
├── edge/                               # 📁 Agente de Carril Raspberry Pi (Python 3.11+)
│   ├── .venv/                          # Entorno virtual aislado de Python
│   ├── requirements.txt                # opencv, onnxruntime, pydantic, sqlite3, azure-iot-device
│   ├── config.example.json             # Certificados mTLS, URLs de backend, umbrales OCR
│   └── src/
│       ├── main.py                     # Ciclo principal de máquina de estados de carril
│       ├── capture/                    # Disparo de ráfaga y sensor inductivo
│       ├── detection/                  # YOLO ONNX recorte y OCR local
│       ├── offline/                    # Cola FIFO local SQLite e idempotencia
│       └── comms/                      # Cliente HTTPS / mTLS y telemetría IoT Hub
│
└── database/                           # 📁 Base de Datos Azure Database for MySQL
    ├── schema/
    │   ├── 01_tables.sql               # DDL con soporte utf8mb4, TDE y particionamiento
    │   └── 02_indexes.sql              # Índices cubrientes de alta concurrencia
    └── seeds/
        └── 01_initial_data.sql         # Tarifas base, terminales y usuarios de prueba
```

---

## 3. Fases Detalladas de Ejecución

### Fase 1: Consolidación Documental y Contexto de IA (`/docs`)
1. Crear el directorio `docs/`.
2. Mover `PLAN_DE_DESARROLLO.md` y `DETALLE_PAGINAS_Y_FLUJOS.md` a `docs/`.
3. Crear `docs/AGENT_CONTEXT.md` estructurado en Markdown limpio y de alta densidad para ingesta rápida de IA:
   - Resumen ejecutivo del negocio y modelo 100% Cashless.
   - Especificación del stack técnico (.NET 10, React Native, MySQL Azure, Python 3.11).
   - Reglas inmutables de seguridad:
     - Irreversibilidad absoluta de contraseñas con **Argon2id** (m=64MB, t=3, p=4) + salt único de 128 bits.
     - Cifrado en tránsito con **TLS 1.3** y mTLS en edge.
     - Cifrado en reposo con **TDE** en MySQL y cifrado por columna **AES-256-GCM** para PII.
   - Filosofía de diseño minimalista estilo sitios web de Apple (apple.com) & Apple HIG (SF Pro, Bento Grids, botones píldora, 44px frosted navbar).
   - Catálogo de endpoints y contratos de datos.

### Fase 2: Configuración Raíz y Control de Versiones
1. Crear `.gitignore` exhaustivo cubriendo:
   - C# / .NET (`bin/`, `obj/`, `*.user`, `appsettings.*.local.json`).
   - Node / JavaScript (`node_modules/`, `.expo/`, `dist/`, `.env`).
   - Python (`.venv/`, `__pycache__/`, `*.pyc`).
   - SO y Editor (`.vscode/`, `.idea/`, `.DS_Store`, `Thumbs.db`).
2. Crear `docker-compose.yml` para levantar localmente MySQL 8.0 y Redis Cache con las configuraciones equivalentes a Azure.
3. Crear `README.md` principal con guía de inicio rápido y enlaces directos a la documentación.

### Fase 3: Scaffolding del Backend (.NET 10 Clean Architecture)
1. Crear directorio `backend/`.
2. Generar solución y proyectos:
   - `dotnet new sln -n AirportParking`
   - `dotnet new classlib -n AirportParking.Domain -f net10.0` en `src/Core/AirportParking.Domain`
   - `dotnet new classlib -n AirportParking.Application -f net10.0` en `src/Core/AirportParking.Application`
   - `dotnet new classlib -n AirportParking.Infrastructure -f net10.0` en `src/Infrastructure/AirportParking.Infrastructure`
   - `dotnet new webapi -n AirportParking.Api -f net10.0` en `src/Presentation/AirportParking.Api`
   - `dotnet new xunit -n AirportParking.UnitTests -f net10.0` en `tests/AirportParking.UnitTests`
3. Configurar referencias entre capas:
   - `Application` referencia `Domain`.
   - `Infrastructure` referencia `Application` y `Domain`.
   - `Api` referencia `Application` e `Infrastructure`.
   - `UnitTests` referencia `Domain` y `Application`.
4. Instalar paquetes NuGet clave:
   - `Pomelo.EntityFrameworkCore.MySql` (Persistencia MySQL en Azure)
   - `MediatR` (CQRS)
   - `FluentValidation.DependencyInjectionExtensions` (Validación declarativa)
   - `Konscious.Security.Cryptography.Argon2` (Hashing irreversible de contraseñas)
5. Crear interfaces de servicios criptográficos (`ICryptographyService`, `IPasswordHasher`) y entidades de dominio base.

### Fase 4: Scaffolding del Edge en Python 3.11 con `.venv`
1. Crear directorio `edge/`.
2. Crear entorno virtual aislado: `python -m venv edge/.venv`.
3. Crear `edge/requirements.txt` con librerías para inferencia y conectividad:
   - `opencv-python-headless`
   - `onnxruntime`
   - `pydantic`
   - `requests`
   - `azure-iot-device`
   - `pytest`
4. Crear estructura modular en `edge/src/` (`capture/`, `detection/`, `offline/`, `comms/`) con punto de entrada `main.py` y configuración de ejemplo.

### Fase 5: Scaffolding del Frontend (React Native & Web)
1. Crear directorio `frontend/`.
2. Estructurar `frontend/customer-mobile/`:
   - `package.json` base con dependencias esenciales de React Native / Expo.
   - Estructura de carpetas por funcionalidades (*feature-based*): `auth`, `access`, `booking`, `payment`, `vehicles`.
   - Archivo de constantes y tokens de diseño estilo apple.com (`src/theme/tokens.ts`): fuentes SF Pro, paleta de colores, sombras suaves y curvas squircle.
3. Estructurar `frontend/admin-web/`:
   - Base para la consola web de escritorio con soporte para Bento Grid y diseño macOS.

### Fase 6: Base de Datos (Scripts MySQL)
1. Crear directorio `database/schema/` y `database/seeds/`.
2. Crear script `database/schema/01_tables.sql` con definición de tablas iniciales (Customers, Vehicles, Stays, Bookings, Tariffs, SecurityAlerts, AuditEntries) optimizadas para MySQL en Azure con soporte utf8mb4.

---

## 4. Criterios de Aceptación y Validación

| Verificación | Comando de Prueba | Resultado Esperado |
| :--- | :--- | :--- |
| **Documentación unificada** | Inspección de `docs/` | Todos los MDs centralizados y `AGENT_CONTEXT.md` disponible |
| **Compilación de Backend** | `dotnet build backend/AirportParking.sln` | `Build succeeded. 0 Error(s)` |
| **Entorno Virtual Edge** | `.\edge\.venv\Scripts\python --version` | `Python 3.11.x` activo |
| **Dependencias Edge** | `.\edge\.venv\Scripts\pip list` | Pip funcional en el entorno virtual |
| **Estructura Frontend** | Validación de carpetas y tokens | `frontend/customer-mobile` estructurado con tokens apple.com |
| **Servicios Criptográficos** | Compilación de interfaces | Tipos de Argon2id y AES-256-GCM definidos |

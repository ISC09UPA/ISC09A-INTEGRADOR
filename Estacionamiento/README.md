# 🛫 AeroPark · Smart Airport Parking
> **Ecosistema Inteligente de Estacionamiento Aeroportuario 100% Digital & Cashless.**  
> Control de acceso LPR por cámara, telemetría IoT de 4 cajones en tiempo real, servomotor PWM de pluma, liquidaciones fiscales SAT CFDI 4.0 y **Consola de Control de Emergencia (Código Rojo)** con enclavamiento físico de barrera, alerta a autoridades (C5/Guardia Nacional) y balizas LED rojas estroboscópicas.  
> 
> *Repositorio Oficial:* [GitHub - AlejandroMechE/-SmartParkingLotAirports](https://github.com/AlejandroMechE/-SmartParkingLotAirports.git)

---

## ⚡ Guía Rápida para Compañeros de Equipo (Ejecución en 3 Pasos)

Si acabas de clonar el repositorio, sigue estos pasos para correr el proyecto completo en tu computadora (Windows, macOS o Linux):

### 📋 Prerrequisitos del Sistema

Antes de iniciar, asegúrate de tener instalado en tu máquina:
1. **Node.js (versión 18 o 20+ LTS):** [Descargar Node.js](https://nodejs.org/)
2. **.NET SDK (versión 8, 9 o 10):** [Descargar .NET SDK](https://dotnet.microsoft.com/download)
3. **Git:** [Descargar Git](https://git-scm.com/)
4. *(Opcional)* **Docker Desktop:** Si deseas levantar el contenedor de base de datos MySQL local (`docker-compose up -d`).

---

### Opción A: Inicio Automático con 1 Clic (Solo Windows)

Si estás en **Windows**, puedes hacer doble clic en el archivo que preparamos en la raíz del proyecto:
- **Doble clic en:** `start-dev.bat`
- O en PowerShell:
  ```powershell
  .\start-dev.ps1
  ```
Esto abrirá dos terminales automáticas levantando el Backend (.NET) y el Frontend (React Native Web).

---

### Opción B: Inicio Manual Paso a Paso (Cualquier Sistema Operativo)

Abre **dos terminales** en la carpeta del proyecto:

#### 📱 Terminal 1: Frontend Universal (React Native / Expo Web)
```bash
# 1. Entrar a la carpeta del frontend móvil
cd Frontend/mobile

# 2. Instalar dependencias
npm install

# 3. Iniciar el servidor web de Expo
npm run web
```
> 🌐 Una vez listo, abre en tu navegador: **[http://localhost:8081](http://localhost:8081)**

*(Nota: Si deseas probarlo en tu celular físico, instala la app **Expo Go** en tu Android/iOS y escanea el código QR que aparece en la terminal).*

#### ⚙️ Terminal 2: Backend API (.NET 10 Minimal APIs)
```bash
# 1. Entrar a la carpeta del backend
cd Backend

# 2. Restaurar paquetes
dotnet restore

# 3. Compilar y correr el servidor API
dotnet run --project EstacionamientoInteligente.API --urls "http://localhost:5000"
```
> 🔌 La API estará activa en: **[http://localhost:5000](http://localhost:5000)**  
> 📑 Documentación interactiva Swagger: **[http://localhost:5000/swagger](http://localhost:5000/swagger)**

---

## 🔐 Credenciales Demo para Iniciar Sesión

El sistema cuenta con detección inteligente de rol para que puedas probar ambos lados de la plataforma desde la pantalla de login:

### 1. Rol Administrador / Supervisor de Operaciones
*Da acceso al Centro de Operaciones, 4 Cajones IoT, Control de Pluma, Liquidaciones Fiscales y **Pantalla de Emergencia**.*
- **Correo Electrónico:** `admin@aeropark.mx`
- **Contraseña:** `admin123` (o cualquier contraseña)
- **PIN de Desarme de Emergencia:** `9911`

### 2. Rol Cliente / Viajero Frecuente
*Da acceso a la App de Pasajeros con Pase Digital Dinámico, Wallet, Registro de Placas y Reserva de Cajones.*
- **Correo Electrónico:** `laura@aeropark.mx`
- **Contraseña:** `cliente123` (o cualquier contraseña)
- *(También puedes pulsar el botón de Face ID / Huella para acceso demo directo).*

---

## 🚀 Módulos y Funcionalidades Principales

### 🚨 1. Pantalla de Emergencia y Protocolo Código Rojo
- **Enclavamiento Total de Pluma (Bloqueo de Subida y Cerrado):** Se deshabilitan los pulsos PWM al servomotor y se acopla el seguro mecánico; ninguna pluma puede levantarse ni bajarse manualmente mientras la emergencia esté activa.
- **Alerta Inmediata a Autoridades:** Despacho automatizado con telemetría perimetral transmitida al **C5 / Fiscalía**, **Guardia Nacional** (Estación Aeropuerto), **AFAC** y **Bomberos CREI**.
- **Balizas y LEDs Rojos Estroboscópicos:** Activación de destellos rojos a 3.2 Hz en carril, semáforo, tira de mástil y los 4 cajones IoT.
- **Sirena Sintetizada con Web Audio API:** Alarma sonora de doble tono audible en el navegador con botón de silencio / mute.
- **Desarme con PIN de Supervisor:** Desbloqueo seguro mediante código PIN (`9911`) que restablece el carril y registra el evento en la bitácora inmutable de auditoría.

### 🚧 2. Centro de Control de Plumas y Carril (LPR & Servomotor)
- Simulación en tiempo real de ángulo de servomotor SG90 (0.0° horizontal a 90.0° vertical).
- Lazo inductivo de masa metálica vehicular con sensor de presencia.
- Cámara LPR 4K con reconocimiento de placas y ciclo automático de paso.
- Botón de interfón / atención a clientes con alerta sonora y visual.

### 🅿️ 3. Monitoreo IoT de 4 Cajones de Estacionamiento
- Telemetría en vivo de distancia ultrasónica (`HC-SR04`) por cajón.
- Indicador visual LED en cada bahía (Verde = Libre, Azul = Carga EV, Rojo = Ocupado / Emergencia).
- Cronómetro de estancia transcurrida por vehículo.

### 💳 4. Liquidaciones y Arqueo Contable Cashless
- Cálculo automatizado de tarifas aeroportuarias ($45.00 MXN / hora).
- Desglose fiscal SAT con CFDI 4.0 timbrado por PAC (Base gravable + IVA 16% trasladado - comisiones bancarias).
- Arqueo de caja 100% conciliado sin discrepancias en efectivo.

---

## 🛠️ Stack Tecnológico

| Capa | Tecnología | Descripción |
| :--- | :--- | :--- |
| **Frontend Universal** | **React Native + Expo SDK 51 + Web** | Interfaz única responsiva para Web de escritorio, iPad/tablets y teléfonos móviles (iOS / Android) bajo las directrices **Apple HIG** y diseño Bento Grid. |
| **Estado Global** | **Zustand** | Manejo reactivo de autenticación, emergencias, estacionamiento y pagos con persistencia local. |
| **Backend API** | **.NET 10 LTS (C#)** | Arquitectura limpia (Clean Architecture), Minimal APIs, CQRS y persistencia en memoria/MySQL. |
| **Edge Hardware** | **Python 3.11+ / Raspberry Pi** | Control de GPIO, servomotor SG90 por PWM, sensores ultrasónicos y lazo inductivo. |
| **Base de Datos** | **MySQL 8.0 & Redis** | Esquema relacional con Docker Compose (`database/seeds/01_initial_data.sql`). |

---

## 🔌 Conexión de Hardware (Raspberry Pi 4 / 5)

Si tu equipo va a montar la maqueta o prototipo físico con la Raspberry Pi:

| Componente | Pin Raspberry Pi | Pin BCM | Tipo / Conexión |
| :--- | :---: | :---: | :--- |
| **Servomotor Pluma (PWM SG90)** | Pin 12 | **GPIO 18** | Señal PWM 50Hz (Alimentación externa 5V) |
| **Relé Enclavamiento Emergencia** | Pin 36 | **GPIO 16** | Control de corte de energía (COM-NC al servo) |
| **Lazo Inductivo / Presencia** | Pin 11 | **GPIO 17** | Entrada digital con resistencia pull-down |
| **Cajón 01 (HC-SR04)** | Pin 16 / 15 | **GPIO 23 (Trig) / GPIO 22 (Echo)** | *ECHO con divisor resistivo 1kΩ + 2kΩ a GND* |
| **Cajón 02 (HC-SR04)** | Pin 22 / 18 | **GPIO 25 (Trig) / GPIO 24 (Echo)** | *ECHO con divisor resistivo 1kΩ + 2kΩ a GND* |
| **Cajón 03 EV (HC-SR04)** | Pin 13 / 37 | **GPIO 27 (Trig) / GPIO 26 (Echo)** | *ECHO con divisor resistivo 1kΩ + 2kΩ a GND* |
| **Cajón 04 (HC-SR04)** | Pin 31 / 29 | **GPIO 6 (Trig) / GPIO 5 (Echo)** | *ECHO con divisor resistivo 1kΩ + 2kΩ a GND* |
| **Semáforo Verde** | Pin 35 | **GPIO 19** | Ánodo con resistencia limitadora 220Ω |
| **Semáforo Rojo** | Pin 33 | **GPIO 13** | Ánodo con resistencia limitadora 220Ω |
| **Balizas / LEDs Rojos Emergencia** | Pin 32 | **GPIO 12** | Control estroboscópico a 3.2 Hz |
| **Buzzer / Sirena de Alarma** | Pin 38 | **GPIO 20** | Zumbador activo de 5V |
| **Botón Físico de Paro de Emergencia** | Pin 40 | **GPIO 21** | Entrada digital con pull-up interno |

> 💡 **Nota de Seguridad:** Para probar el circuito en la Raspberry Pi, ejecuta el script de prueba:
> ```bash
> cd edge
> python src/main.py
> ```

---

## ❓ Preguntas Frecuentes y Solución de Problemas (FAQ)

### 1. ¿Necesito tener el Backend .NET corriendo obligatoriamente para ver el Frontend?
**No.** El Frontend cuenta con un **Modo Autónomo Local** integrado. Si no abres el backend, la aplicación web funcionará normalmente con datos reactivos locales en Zustand. Cuando inicias el backend, el frontend se sincroniza automáticamente.

### 2. ¿Qué hago si me marca error de paquetes en el Frontend?
Ejecuta una instalación limpia:
```bash
cd frontend
npm install --legacy-peer-deps
npm run web
```

### 3. ¿Cómo cambio entre la vista de Administrador y la vista de Cliente?
Cierra sesión con el botón **Salir (⎋)** en la esquina superior derecha del header. En la pantalla de login, escribe `admin@aeropark.mx` para entrar como Administrador o `laura@aeropark.mx` para entrar como Cliente.

---

## 👥 Colaboradores
Proyecto Integrador de Ingeniería Aeroportuaria y Mecatrónica  
Desarrollado para el sistema de estacionamiento inteligente de alta velocidad y cero contacto.

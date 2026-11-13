# 🗄️ AeroPark · Base de Datos MySQL 8.0+

> **Motor Relacional de Alto Rendimiento para el Sistema de Estacionamiento Inteligente de Aeropuerto.**  
> Soporta telemetría IoT de cajones en tiempo real, reconocimiento de placas LPR, liquidaciones fiscales SAT CFDI 4.0, listas de control vehicular y protocolo policial Código Rojo (Zero Trust).

---

## 📁 Estructura de Scripts SQL

| Archivo | Propósito |
| :--- | :--- |
| **`init_database.sql`** | **Script Maestro 1-Clic.** Crea la base de datos `airport_parking`, todas las tablas, vistas, procedimientos almacenados y datos semilla. Ideal para MySQL Workbench o CLI. |
| **`schema/01_tables.sql`** | Definición DDL de las 15 tablas relacionales con tipos `utf8mb4`, llaves foráneas e índices optimizados. |
| **`schema/02_views_procedures.sql`** | Vistas de negocio (`vw_resumen_cajones`, `vw_estancias_en_curso`, `vw_kpis_operativos`) y Procedimientos Almacenados (`sp_registrar_ingreso_lpr`, `sp_registrar_salida_lpr`, `sp_actualizar_telemetria_cajon`). |
| **`schema/03_seed_data.sql`** | Semillas de demostración: usuarios demo (Cliente y Administrador), 4 cajones IoT, tarifas, vehículos, listas de control y alertas. |

---

## 🚀 Métodos de Ejecución

### Opción A: Con Docker Compose (Recomendado y Automático)

Si usas Docker, el archivo `docker-compose.yml` en la raíz del proyecto monta automáticamente la carpeta `database/schema/` en el contenedor:

```bash
# 1. Levantar el contenedor de MySQL 8.0 y Adminer
docker-compose up -d mysql adminer

# 2. Entrar a la interfaz web de gestión (Adminer):
# URL: http://localhost:8080
# Sistema: MySQL
# Servidor: mysql
# Usuario: parking_user
# Contraseña: parking_password
# Base de datos: airport_parking
```

---

### Opción B: En MySQL Workbench / DBeaver / Navicat

1. Abre **MySQL Workbench** y conéctate a tu instancia local de MySQL.
2. Abre el archivo:
   ```
   database/init_database.sql
   ```
3. Ejecuta todo el script con el botón del rayo (**Execute entire script** o `Ctrl + Shift + Enter`).
4. ¡Listo! La base de datos `airport_parking` estará creada con tablas, vistas, procedimientos y datos iniciales.

---

### Opción C: Desde la Terminal (PowerShell o CMD)

```powershell
# Ejecución directa con el cliente de MySQL:
Get-Content database\init_database.sql | mysql -u root -p
```

---

## 🔐 Credenciales por Defecto

* **Host / Servidor:** `localhost` (puerto `3306`)
* **Base de datos:** `airport_parking`
* **Usuario de Aplicación:** `parking_user`
* **Contraseña de Aplicación:** `parking_password`
* **Usuario Administrador:** `root`
* **Contraseña Root:** `rootpassword`

---

## 📊 Tablas Principales del Sistema

1. **`customers`:** Usuarios pasajeros y supervisores con roles (`CUSTOMER`, `ADMIN`) y hashing Argon2id.
2. **`vehicles`:** Autos registrados con placas normalizadas sin guiones (`ABC123D`).
3. **`parking_bays`:** Los 4 cajones IoT sincronizados con la Raspberry Pi (`bay_number`, distancia ultrasónica y estado).
4. **`tariffs`:** Esquema de cobro ($45.00 MXN / hora en fracciones de 15 min, tope de $350.00 MXN/día).
5. **`stays`:** Sesiones de estacionamiento activas e históricas con carril de entrada y salida.
6. **`access_events`:** Telemetría en tiempo real de apertura de pluma, confianza de visión artificial y motivos.
7. **`security_alerts`:** Detecciones de placas boletinadas por C5 / Guardia Nacional (Código Rojo).
8. **`list_entries`:** Lista blanca para ambulancias CREI y patrullas aeroportuarias (acceso exento).
9. **`payments`:** Registros de cobro 100% digital (Apple Pay, Tarjetas).
10. **`system_settings`:** Enclavamiento físico de barrera (`BARRIER_STATUS`, `EMERGENCY_LOCKDOWN`).

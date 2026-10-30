-- ==============================================================================
-- 01_tables.sql: Esquema de Tablas para Estacionamiento Inteligente de Aeropuerto
-- Motor: Azure Database for MySQL (Flexible Server) / MySQL 8.0+
-- Collation: utf8mb4_0900_ai_ci
-- ==============================================================================

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- ------------------------------------------------------------------------------
-- 1. Clientes y Usuarios
-- ------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `customers` (
    `id` VARCHAR(36) NOT NULL,
    `external_id` VARCHAR(128) NULL,
    `nombre` VARCHAR(150) NOT NULL,
    `correo_cifrado` VARCHAR(512) NOT NULL,
    `correo_hash` VARCHAR(64) NOT NULL,
    `telefono_cifrado` VARCHAR(512) NOT NULL,
    `password_hash` VARCHAR(256) NOT NULL COMMENT 'Argon2id unidireccional (imposible descifrar)',
    `password_salt` VARCHAR(64) NOT NULL COMMENT 'Salt CSPRNG único de 128 bits',
    `idioma` VARCHAR(10) NOT NULL DEFAULT 'es-MX',
    `estado` VARCHAR(20) NOT NULL DEFAULT 'ACTIVE',
    `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_customers_correo_hash` (`correo_hash`),
    KEY `idx_customers_external_id` (`external_id`),
    KEY `idx_customers_estado` (`estado`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ------------------------------------------------------------------------------
-- 2. Consentimientos Legales (LFPDPPP)
-- ------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `consents` (
    `id` VARCHAR(36) NOT NULL,
    `customer_id` VARCHAR(36) NOT NULL,
    `tipo` VARCHAR(50) NOT NULL,
    `version_aviso` VARCHAR(20) NOT NULL,
    `fecha_consentimiento` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    `ip_origen` VARCHAR(45) NOT NULL,
    PRIMARY KEY (`id`),
    KEY `idx_consents_customer_id` (`customer_id`),
    CONSTRAINT `fk_consents_customer` FOREIGN KEY (`customer_id`) REFERENCES `customers` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ------------------------------------------------------------------------------
-- 3. Vehículos
-- ------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `vehicles` (
    `id` VARCHAR(36) NOT NULL,
    `customer_id` VARCHAR(36) NOT NULL,
    `placa_normalizada` VARCHAR(15) NOT NULL,
    `alias` VARCHAR(50) NULL,
    `marca` VARCHAR(50) NULL,
    `modelo` VARCHAR(50) NULL,
    `color` VARCHAR(30) NULL,
    `es_predeterminado` TINYINT(1) NOT NULL DEFAULT 0,
    `is_active` TINYINT(1) NOT NULL DEFAULT 1,
    `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_vehicles_customer_placa` (`customer_id`, `placa_normalizada`),
    KEY `idx_vehicles_placa` (`placa_normalizada`),
    CONSTRAINT `fk_vehicles_customer` FOREIGN KEY (`customer_id`) REFERENCES `customers` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ------------------------------------------------------------------------------
-- 4. Terminales y Zonas
-- ------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `terminals` (
    `id` VARCHAR(36) NOT NULL,
    `codigo` VARCHAR(20) NOT NULL,
    `nombre` VARCHAR(100) NOT NULL,
    `is_active` TINYINT(1) NOT NULL DEFAULT 1,
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_terminals_codigo` (`codigo`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS `zones` (
    `id` VARCHAR(36) NOT NULL,
    `terminal_id` VARCHAR(36) NOT NULL,
    `nombre` VARCHAR(100) NOT NULL,
    `nivel` VARCHAR(20) NOT NULL,
    `capacidad_total` INT NOT NULL,
    `tipo_cajon` VARCHAR(30) NOT NULL DEFAULT 'STANDARD',
    `is_active` TINYINT(1) NOT NULL DEFAULT 1,
    PRIMARY KEY (`id`),
    KEY `idx_zones_terminal_id` (`terminal_id`),
    CONSTRAINT `fk_zones_terminal` FOREIGN KEY (`terminal_id`) REFERENCES `terminals` (`id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ------------------------------------------------------------------------------
-- 5. Esquemas Tarifarios
-- ------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `tariffs` (
    `id` VARCHAR(36) NOT NULL,
    `version` INT NOT NULL,
    `fraccion_minutos` INT NOT NULL DEFAULT 15,
    `costo_fraccion` DECIMAL(10,2) NOT NULL,
    `tope_diario` DECIMAL(10,2) NOT NULL,
    `tolerancia_salida_minutos` INT NOT NULL DEFAULT 15,
    `vigencia_desde` DATETIME(3) NOT NULL,
    `vigencia_hasta` DATETIME(3) NULL,
    `is_active` TINYINT(1) NOT NULL DEFAULT 1,
    `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    PRIMARY KEY (`id`),
    KEY `idx_tariffs_vigencia` (`vigencia_desde`, `vigencia_hasta`, `is_active`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ------------------------------------------------------------------------------
-- 6. Reservas
-- ------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `bookings` (
    `id` VARCHAR(36) NOT NULL,
    `customer_id` VARCHAR(36) NOT NULL,
    `vehicle_id` VARCHAR(36) NOT NULL,
    `zone_id` VARCHAR(36) NOT NULL,
    `fecha_inicio` DATETIME(3) NOT NULL,
    `fecha_fin` DATETIME(3) NOT NULL,
    `monto_total` DECIMAL(10,2) NOT NULL,
    `moneda` VARCHAR(5) NOT NULL DEFAULT 'MXN',
    `estado` VARCHAR(30) NOT NULL DEFAULT 'CONFIRMED',
    `codigo_qr` VARCHAR(256) NOT NULL,
    `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (`id`),
    KEY `idx_bookings_customer` (`customer_id`),
    KEY `idx_bookings_fechas_estado` (`fecha_inicio`, `fecha_fin`, `estado`),
    CONSTRAINT `fk_bookings_customer` FOREIGN KEY (`customer_id`) REFERENCES `customers` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `fk_bookings_vehicle` FOREIGN KEY (`vehicle_id`) REFERENCES `vehicles` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `fk_bookings_zone` FOREIGN KEY (`zone_id`) REFERENCES `zones` (`id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ------------------------------------------------------------------------------
-- 7. Estancias (Stays)
-- ------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `stays` (
    `id` VARCHAR(36) NOT NULL,
    `booking_id` VARCHAR(36) NULL,
    `placa_normalizada` VARCHAR(15) NOT NULL,
    `carril_entrada` VARCHAR(50) NOT NULL,
    `fecha_ingreso` DATETIME(3) NOT NULL,
    `carril_salida` VARCHAR(50) NULL,
    `fecha_salida` DATETIME(3) NULL,
    `monto_calculado` DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    `estado_cobro` VARCHAR(30) NOT NULL DEFAULT 'PENDING',
    `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    PRIMARY KEY (`id`),
    KEY `idx_stays_placa_ingreso` (`placa_normalizada`, `fecha_ingreso`),
    KEY `idx_stays_estado_cobro` (`estado_cobro`),
    CONSTRAINT `fk_stays_booking` FOREIGN KEY (`booking_id`) REFERENCES `bookings` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ------------------------------------------------------------------------------
-- 8. Pagos 100% Digitales
-- ------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `payments` (
    `id` VARCHAR(36) NOT NULL,
    `stay_id` VARCHAR(36) NULL,
    `booking_id` VARCHAR(36) NULL,
    `monto` DECIMAL(10,2) NOT NULL,
    `moneda` VARCHAR(5) NOT NULL DEFAULT 'MXN',
    `metodo` VARCHAR(50) NOT NULL COMMENT 'apple_pay, google_pay, card_token',
    `psp_reference` VARCHAR(100) NOT NULL,
    `idempotency_key` VARCHAR(128) NOT NULL,
    `estado` VARCHAR(30) NOT NULL DEFAULT 'SUCCEEDED',
    `recibo_url` VARCHAR(512) NULL,
    `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_payments_idempotency` (`idempotency_key`),
    KEY `idx_payments_stay_id` (`stay_id`),
    KEY `idx_payments_booking_id` (`booking_id`),
    CONSTRAINT `fk_payments_stay` FOREIGN KEY (`stay_id`) REFERENCES `stays` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `fk_payments_booking` FOREIGN KEY (`booking_id`) REFERENCES `bookings` (`id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ------------------------------------------------------------------------------
-- 9. Incidencias Operativas
-- ------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `incidents` (
    `id` VARCHAR(36) NOT NULL,
    `carril_id` VARCHAR(50) NOT NULL,
    `tipo_incidencia` VARCHAR(50) NOT NULL,
    `descripcion` TEXT NOT NULL,
    `estado` VARCHAR(30) NOT NULL DEFAULT 'OPEN',
    `operador_asignado_id` VARCHAR(36) NULL,
    `causa_resolucion` TEXT NULL,
    `resuelto_at` DATETIME(3) NULL,
    `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    PRIMARY KEY (`id`),
    KEY `idx_incidents_estado` (`estado`, `created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ------------------------------------------------------------------------------
-- 10. Alertas de Seguridad Policial (Zero Trust)
-- ------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `security_alerts` (
    `id` VARCHAR(36) NOT NULL,
    `placa_normalizada` VARCHAR(15) NOT NULL,
    `referencia_reporte` VARCHAR(100) NOT NULL,
    `evidencia_uri` VARCHAR(512) NOT NULL,
    `confianza_ocr` DECIMAL(5,4) NOT NULL,
    `dictamen` VARCHAR(30) NOT NULL DEFAULT 'PENDING_REVIEW' COMMENT 'CONFIRMED, DISCARDED, ESCALATED',
    `comentario_cifrado` TEXT NULL COMMENT 'Cifrado AES-256-GCM a nivel de columna',
    `oficial_id` VARCHAR(36) NULL,
    `dictamen_at` DATETIME(3) NULL,
    `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    PRIMARY KEY (`id`),
    KEY `idx_security_alerts_dictamen` (`dictamen`, `created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ------------------------------------------------------------------------------
-- 11. Listas de Control (Blanca / Restringida)
-- ------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `list_entries` (
    `id` VARCHAR(36) NOT NULL,
    `placa_normalizada` VARCHAR(15) NOT NULL,
    `tipo_lista` VARCHAR(20) NOT NULL COMMENT 'WHITE_LIST, RESTRICTED_LIST',
    `justificacion` VARCHAR(255) NOT NULL,
    `vigencia_hasta` DATETIME(3) NULL,
    `is_active` TINYINT(1) NOT NULL DEFAULT 1,
    `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_list_entries_placa_tipo` (`placa_normalizada`, `tipo_lista`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ------------------------------------------------------------------------------
-- 12. Bitácora de Auditoría (Append-Only)
-- ------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `audit_entries` (
    `id` BIGINT NOT NULL AUTO_INCREMENT,
    `actor_id` VARCHAR(50) NOT NULL,
    `accion` VARCHAR(100) NOT NULL,
    `recurso` VARCHAR(100) NOT NULL,
    `recurso_id` VARCHAR(50) NOT NULL,
    `payload_previo` JSON NULL,
    `payload_nuevo` JSON NULL,
    `ip_origen` VARCHAR(45) NOT NULL,
    `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    PRIMARY KEY (`id`),
    KEY `idx_audit_actor_created` (`actor_id`, `created_at`),
    KEY `idx_audit_recurso` (`recurso`, `recurso_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

SET FOREIGN_KEY_CHECKS = 1;

-- ==============================================================================
-- init_database.sql: Script Maestro Unificado de Base de Datos MySQL
-- Sistema: AeroPark · Estacionamiento Inteligente de Aeropuerto
-- Motor: MySQL 8.0+ / Azure Database for MySQL
-- Collation: utf8mb4_0900_ai_ci
-- ==============================================================================

-- 1. Crear Base de Datos si no existe
CREATE DATABASE IF NOT EXISTS `airport_parking`
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_0900_ai_ci;

USE `airport_parking`;

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- ------------------------------------------------------------------------------
-- 2. TABLAS DEL SISTEMA (DDL)
-- ------------------------------------------------------------------------------

-- Clientes y Usuarios (Pasajeros, Administradores, Seguridad)
CREATE TABLE IF NOT EXISTS `customers` (
    `id` VARCHAR(36) NOT NULL,
    `external_id` VARCHAR(128) NULL,
    `nombre` VARCHAR(150) NOT NULL,
    `correo_cifrado` VARCHAR(512) NOT NULL,
    `correo_hash` VARCHAR(64) NOT NULL COMMENT 'Blind index SHA-256 para búsquedas exactas O(1)',
    `telefono_cifrado` VARCHAR(512) NOT NULL,
    `password_hash` VARCHAR(256) NOT NULL COMMENT 'Argon2id unidireccional',
    `password_salt` VARCHAR(64) NOT NULL COMMENT 'Salt CSPRNG único de 128 bits',
    `idioma` VARCHAR(10) NOT NULL DEFAULT 'es-MX',
    `estado` VARCHAR(20) NOT NULL DEFAULT 'ACTIVE' COMMENT 'ACTIVE, SUSPENDED, DELETED',
    `role` VARCHAR(20) NOT NULL DEFAULT 'CUSTOMER' COMMENT 'CUSTOMER, ADMIN, SUPERVISOR',
    `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_customers_correo_hash` (`correo_hash`),
    KEY `idx_customers_external_id` (`external_id`),
    KEY `idx_customers_estado` (`estado`),
    KEY `idx_customers_role` (`role`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Consentimientos Legales (LFPDPPP)
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

-- Terminales y Zonas Aeroportuarias
CREATE TABLE IF NOT EXISTS `terminals` (
    `id` VARCHAR(36) NOT NULL,
    `codigo` VARCHAR(20) NOT NULL,
    `nombre` VARCHAR(100) NOT NULL,
    `is_active` TINYINT(1) NOT NULL DEFAULT 1,
    `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_terminals_codigo` (`codigo`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS `zones` (
    `id` VARCHAR(36) NOT NULL,
    `terminal_id` VARCHAR(36) NOT NULL,
    `nombre` VARCHAR(100) NOT NULL,
    `nivel` VARCHAR(20) NOT NULL,
    `capacidad_total` INT NOT NULL,
    `tipo_cajon` VARCHAR(30) NOT NULL DEFAULT 'STANDARD' COMMENT 'STANDARD, COVERED, EV, VALET',
    `is_active` TINYINT(1) NOT NULL DEFAULT 1,
    `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    PRIMARY KEY (`id`),
    KEY `idx_zones_terminal_id` (`terminal_id`),
    CONSTRAINT `fk_zones_terminal` FOREIGN KEY (`terminal_id`) REFERENCES `terminals` (`id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Cajones de Estacionamiento IoT (Gemelo Digital)
CREATE TABLE IF NOT EXISTS `parking_bays` (
    `id` VARCHAR(36) NOT NULL,
    `bay_number` INT NOT NULL,
    `name` VARCHAR(50) NOT NULL,
    `terminal_id` VARCHAR(36) NOT NULL DEFAULT 't1-agu-main',
    `zone_id` VARCHAR(36) NULL,
    `status` VARCHAR(20) NOT NULL DEFAULT 'FREE' COMMENT 'FREE, OCCUPIED, EV, MAINTENANCE',
    `current_plate` VARCHAR(20) NOT NULL DEFAULT '—',
    `current_vehicle` VARCHAR(100) NOT NULL DEFAULT 'Despejado',
    `sensor_distance` VARCHAR(30) NOT NULL DEFAULT '> 150 cm',
    `occupied_since` DATETIME(3) NULL,
    `rate_per_hour` DECIMAL(10,2) NOT NULL DEFAULT 45.00,
    `gpio_trig` INT NULL,
    `gpio_echo` INT NULL,
    `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_parking_bays_number` (`bay_number`),
    KEY `idx_parking_bays_status` (`status`),
    KEY `idx_parking_bays_plate` (`current_plate`),
    CONSTRAINT `fk_parking_bays_zone` FOREIGN KEY (`zone_id`) REFERENCES `zones` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Vehículos
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
    `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_vehicles_customer_placa` (`customer_id`, `placa_normalizada`),
    KEY `idx_vehicles_placa` (`placa_normalizada`),
    CONSTRAINT `fk_vehicles_customer` FOREIGN KEY (`customer_id`) REFERENCES `customers` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Esquemas Tarifarios
CREATE TABLE IF NOT EXISTS `tariffs` (
    `id` VARCHAR(36) NOT NULL,
    `version` INT NOT NULL,
    `fraccion_minutos` INT NOT NULL DEFAULT 15,
    `costo_fraccion` DECIMAL(10,2) NOT NULL DEFAULT 11.25 COMMENT '$45.00 MXN / hora',
    `tope_diario` DECIMAL(10,2) NOT NULL DEFAULT 350.00,
    `tolerancia_salida_minutos` INT NOT NULL DEFAULT 15,
    `vigencia_desde` DATETIME(3) NOT NULL,
    `vigencia_hasta` DATETIME(3) NULL,
    `is_active` TINYINT(1) NOT NULL DEFAULT 1,
    `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    PRIMARY KEY (`id`),
    KEY `idx_tariffs_vigencia` (`vigencia_desde`, `vigencia_hasta`, `is_active`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Reservas Digitales
CREATE TABLE IF NOT EXISTS `bookings` (
    `id` VARCHAR(36) NOT NULL,
    `customer_id` VARCHAR(36) NOT NULL,
    `vehicle_id` VARCHAR(36) NOT NULL,
    `zone_id` VARCHAR(36) NOT NULL,
    `fecha_inicio` DATETIME(3) NOT NULL,
    `fecha_fin` DATETIME(3) NOT NULL,
    `monto_total` DECIMAL(10,2) NOT NULL,
    `moneda` VARCHAR(5) NOT NULL DEFAULT 'MXN',
    `estado` VARCHAR(30) NOT NULL DEFAULT 'CONFIRMED' COMMENT 'CONFIRMED, COMPLETED, CANCELLED',
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

-- Estancias Vehiculares
CREATE TABLE IF NOT EXISTS `stays` (
    `id` VARCHAR(36) NOT NULL,
    `booking_id` VARCHAR(36) NULL,
    `placa_normalizada` VARCHAR(15) NOT NULL,
    `carril_entrada` VARCHAR(50) NOT NULL,
    `fecha_ingreso` DATETIME(3) NOT NULL,
    `carril_salida` VARCHAR(50) NULL,
    `fecha_salida` DATETIME(3) NULL,
    `monto_calculado` DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    `estado_cobro` VARCHAR(30) NOT NULL DEFAULT 'PENDING' COMMENT 'PENDING, PAID, EXEMPT',
    `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (`id`),
    KEY `idx_stays_placa_ingreso` (`placa_normalizada`, `fecha_ingreso`),
    KEY `idx_stays_estado_cobro` (`estado_cobro`),
    CONSTRAINT `fk_stays_booking` FOREIGN KEY (`booking_id`) REFERENCES `bookings` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Pagos Digitales Cashless
CREATE TABLE IF NOT EXISTS `payments` (
    `id` VARCHAR(36) NOT NULL,
    `stay_id` VARCHAR(36) NULL,
    `booking_id` VARCHAR(36) NULL,
    `monto` DECIMAL(10,2) NOT NULL,
    `moneda` VARCHAR(5) NOT NULL DEFAULT 'MXN',
    `metodo` VARCHAR(50) NOT NULL,
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

-- Eventos de Acceso y Carril (LPR & Barrera)
CREATE TABLE IF NOT EXISTS `access_events` (
    `id` VARCHAR(36) NOT NULL,
    `lane_id` VARCHAR(50) NOT NULL,
    `lane_type` VARCHAR(20) NOT NULL DEFAULT 'ENTRY' COMMENT 'ENTRY, EXIT',
    `placa_normalizada` VARCHAR(15) NOT NULL,
    `confianza_lpr` DOUBLE NOT NULL DEFAULT 0.95,
    `decision` VARCHAR(30) NOT NULL DEFAULT 'ALLOW' COMMENT 'ALLOW, DENIED, REVIEW_REQUIRED',
    `motivo` VARCHAR(255) NOT NULL,
    `evidencia_image_url` VARCHAR(512) NULL,
    `timestamp` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    PRIMARY KEY (`id`),
    KEY `idx_access_events_lane` (`lane_id`, `timestamp`),
    KEY `idx_access_events_placa` (`placa_normalizada`),
    KEY `idx_access_events_decision` (`decision`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Alertas de Seguridad Policial (Zero Trust / Código Rojo)
CREATE TABLE IF NOT EXISTS `security_alerts` (
    `id` VARCHAR(36) NOT NULL,
    `placa_normalizada` VARCHAR(15) NOT NULL,
    `delito_reportado` VARCHAR(200) NOT NULL,
    `fuente_boletin` VARCHAR(100) NOT NULL DEFAULT 'C5 / Fiscalía General',
    `confianza_coincidencia` DOUBLE NOT NULL DEFAULT 0.98,
    `estado_dictamen` VARCHAR(30) NOT NULL DEFAULT 'PENDING_REVIEW' COMMENT 'PENDING_REVIEW, CONFIRMED, DISMISSED',
    `dictaminado_por` VARCHAR(100) NULL,
    `justificacion_dictamen` TEXT NULL,
    `fecha_dictamen` DATETIME(3) NULL,
    `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    PRIMARY KEY (`id`),
    KEY `idx_security_alerts_placa` (`placa_normalizada`),
    KEY `idx_security_alerts_dictamen` (`estado_dictamen`, `created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Listas de Control (Blanca / Restringida)
CREATE TABLE IF NOT EXISTS `list_entries` (
    `id` VARCHAR(36) NOT NULL,
    `placa_normalizada` VARCHAR(15) NOT NULL,
    `tipo_lista` VARCHAR(20) NOT NULL COMMENT 'WHITE_LIST, BLACK_LIST',
    `justificacion` VARCHAR(255) NOT NULL,
    `vigencia_hasta` DATETIME(3) NULL,
    `is_active` TINYINT(1) NOT NULL DEFAULT 1,
    `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_list_entries_placa_tipo` (`placa_normalizada`, `tipo_lista`),
    KEY `idx_list_entries_activa` (`placa_normalizada`, `tipo_lista`, `is_active`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Incidencias Operativas
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

-- Bitácora de Auditoría
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

-- Configuración del Sistema
CREATE TABLE IF NOT EXISTS `system_settings` (
    `clave` VARCHAR(50) NOT NULL,
    `valor` VARCHAR(255) NOT NULL,
    `descripcion` VARCHAR(255) NULL,
    `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (`clave`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ------------------------------------------------------------------------------
-- 3. VISTAS ANALÍTICAS Y OPERATIVAS
-- ------------------------------------------------------------------------------

CREATE OR REPLACE VIEW `vw_resumen_cajones` AS
SELECT 
    pb.id,
    pb.bay_number,
    pb.name,
    pb.status,
    pb.current_plate,
    pb.current_vehicle,
    pb.sensor_distance,
    pb.rate_per_hour,
    pb.occupied_since,
    CASE 
        WHEN pb.occupied_since IS NOT NULL THEN TIMESTAMPDIFF(MINUTE, pb.occupied_since, NOW())
        ELSE 0 
    END AS minutos_ocupado,
    ROUND(
        CASE 
            WHEN pb.occupied_since IS NOT NULL THEN (TIMESTAMPDIFF(MINUTE, pb.occupied_since, NOW()) / 60.0) * pb.rate_per_hour
            ELSE 0.00 
        END, 2
    ) AS acumulado_estimado_mxn
FROM `parking_bays` pb
ORDER BY pb.bay_number ASC;

CREATE OR REPLACE VIEW `vw_estancias_en_curso` AS
SELECT 
    s.id AS stay_id,
    s.placa_normalizada,
    s.carril_entrada,
    s.fecha_ingreso,
    s.estado_cobro,
    TIMESTAMPDIFF(MINUTE, s.fecha_ingreso, NOW()) AS minutos_transcurridos,
    v.marca,
    v.modelo,
    v.color,
    c.nombre AS propietario,
    b.id AS booking_id,
    b.codigo_qr
FROM `stays` s
LEFT JOIN `vehicles` v ON s.placa_normalizada = v.placa_normalizada
LEFT JOIN `customers` c ON v.customer_id = c.id
LEFT JOIN `bookings` b ON s.booking_id = b.id
WHERE s.fecha_salida IS NULL;

CREATE OR REPLACE VIEW `vw_kpis_operativos` AS
SELECT 
    (SELECT COUNT(*) FROM `parking_bays` WHERE `status` = 'OCCUPIED') AS cajones_ocupados,
    (SELECT COUNT(*) FROM `parking_bays` WHERE `status` = 'FREE') AS cajones_libres,
    (SELECT COUNT(*) FROM `parking_bays` WHERE `status` = 'EV') AS cajones_ev,
    (SELECT COUNT(*) FROM `stays` WHERE `fecha_salida` IS NULL) AS estancias_activas,
    (SELECT IFNULL(SUM(`monto`), 0.00) FROM `payments` WHERE DATE(`created_at`) = CURDATE() AND `estado` = 'SUCCEEDED') AS recaudacion_cashless_hoy,
    (SELECT COUNT(*) FROM `security_alerts` WHERE `estado_dictamen` = 'PENDING_REVIEW') AS alertas_seguridad_pendientes,
    (SELECT COUNT(*) FROM `incidents` WHERE `estado` = 'OPEN') AS incidencias_abiertas;

-- ------------------------------------------------------------------------------
-- 4. PROCEDIMIENTOS ALMACENADOS
-- ------------------------------------------------------------------------------

DELIMITER $$

DROP PROCEDURE IF EXISTS `sp_registrar_ingreso_lpr`$$
CREATE PROCEDURE `sp_registrar_ingreso_lpr`(
    IN  p_placa              VARCHAR(15),
    IN  p_carril             VARCHAR(50),
    IN  p_confianza_ocr      DOUBLE,
    IN  p_evidencia_url      VARCHAR(512),
    OUT p_decision           VARCHAR(30),
    OUT p_motivo             VARCHAR(255),
    OUT p_stay_id            VARCHAR(36)
)
BEGIN
    DECLARE v_es_lista_negra INT DEFAULT 0;
    DECLARE v_es_alerta      INT DEFAULT 0;
    DECLARE v_es_lista_blanca INT DEFAULT 0;
    DECLARE v_stay_activa    VARCHAR(36) DEFAULT NULL;
    DECLARE v_uuid_event     VARCHAR(36);
    
    SET p_placa = UPPER(TRIM(REPLACE(p_placa, '-', '')));
    SET v_uuid_event = UUID();
    SET p_stay_id = NULL;

    SELECT COUNT(*) INTO v_es_alerta 
    FROM `security_alerts` 
    WHERE `placa_normalizada` = p_placa 
      AND `estado_dictamen` IN ('PENDING_REVIEW', 'CONFIRMED');

    IF v_es_alerta > 0 THEN
        SET p_decision = 'REVIEW_REQUIRED';
        SET p_motivo = 'ALERTA DE SEGURIDAD: Placa con reporte policial activo en C5 / Fiscalía. Pluma bloqueada.';
    ELSE
        SELECT COUNT(*) INTO v_es_lista_negra 
        FROM `list_entries` 
        WHERE `placa_normalizada` = p_placa 
          AND `tipo_lista` = 'BLACK_LIST' 
          AND `is_active` = 1;

        IF v_es_lista_negra > 0 THEN
            SET p_decision = 'DENIED';
            SET p_motivo = 'ACCESO DENEGADO: Vehículo registrado en lista negra de control vehicular.';
        ELSE
            SELECT COUNT(*) INTO v_es_lista_blanca 
            FROM `list_entries` 
            WHERE `placa_normalizada` = p_placa 
              AND `tipo_lista` = 'WHITE_LIST' 
              AND `is_active` = 1;

            SET p_decision = 'ALLOW';
            IF v_es_lista_blanca > 0 THEN
                SET p_motivo = 'ACCESO CONCEDIDO: Vehículo oficial / servicios de emergencia exento.';
            ELSE
                SET p_motivo = 'ACCESO CONCEDIDO: Apertura de pluma autorizada.';
            END IF;

            SELECT `id` INTO v_stay_activa 
            FROM `stays` 
            WHERE `placa_normalizada` = p_placa AND `fecha_salida` IS NULL 
            LIMIT 1;

            IF v_stay_activa IS NULL THEN
                SET p_stay_id = UUID();
                INSERT INTO `stays` (
                    `id`, `placa_normalizada`, `carril_entrada`, `fecha_ingreso`, `estado_cobro`
                ) VALUES (
                    p_stay_id, p_placa, p_carril, NOW(3), CASE WHEN v_es_lista_blanca > 0 THEN 'EXEMPT' ELSE 'PENDING' END
                );
            ELSE
                SET p_stay_id = v_stay_activa;
            END IF;
        END IF;
    END IF;

    INSERT INTO `access_events` (
        `id`, `lane_id`, `lane_type`, `placa_normalizada`, `confianza_lpr`, `decision`, `motivo`, `evidencia_image_url`, `timestamp`
    ) VALUES (
        v_uuid_event, p_carril, 'ENTRY', p_placa, p_confianza_ocr, p_decision, p_motivo, p_evidencia_url, NOW(3)
    );
END$$

DROP PROCEDURE IF EXISTS `sp_registrar_salida_lpr`$$
CREATE PROCEDURE `sp_registrar_salida_lpr`(
    IN  p_placa                  VARCHAR(15),
    IN  p_carril_salida          VARCHAR(50),
    OUT p_monto_total            DECIMAL(10,2),
    OUT p_minutos_transcurridos  INT,
    OUT p_stay_id                VARCHAR(36),
    OUT p_estado_cobro           VARCHAR(30)
)
BEGIN
    DECLARE v_fecha_ingreso DATETIME(3);
    DECLARE v_fraccion_min  INT DEFAULT 15;
    DECLARE v_costo_fracc   DECIMAL(10,2) DEFAULT 11.25;
    DECLARE v_tope_dia      DECIMAL(10,2) DEFAULT 350.00;
    DECLARE v_tolerancia    INT DEFAULT 15;
    DECLARE v_fracciones    INT DEFAULT 0;

    SET p_placa = UPPER(TRIM(REPLACE(p_placa, '-', '')));
    SET p_monto_total = 0.00;
    SET p_minutos_transcurridos = 0;
    SET p_stay_id = NULL;

    SELECT `id`, `fecha_ingreso`, `estado_cobro` 
    INTO p_stay_id, v_fecha_ingreso, p_estado_cobro
    FROM `stays` 
    WHERE `placa_normalizada` = p_placa AND `fecha_salida` IS NULL 
    ORDER BY `fecha_ingreso` DESC 
    LIMIT 1;

    IF p_stay_id IS NOT NULL THEN
        SET p_minutos_transcurridos = TIMESTAMPDIFF(MINUTE, v_fecha_ingreso, NOW());

        SELECT `fraccion_minutos`, `costo_fraccion`, `tope_diario`, `tolerancia_salida_minutos`
        INTO v_fraccion_min, v_costo_fracc, v_tope_dia, v_tolerancia
        FROM `tariffs`
        WHERE `is_active` = 1
        ORDER BY `version` DESC
        LIMIT 1;

        IF p_minutos_transcurridos <= v_tolerancia OR p_estado_cobro = 'EXEMPT' THEN
            SET p_monto_total = 0.00;
        ELSE
            SET v_fracciones = CEIL(p_minutos_transcurridos / v_fraccion_min);
            SET p_monto_total = v_fracciones * v_costo_fracc;

            IF p_monto_total > v_tope_dia THEN
                SET p_monto_total = v_tope_dia;
            END IF;
        END IF;

        UPDATE `stays`
        SET `carril_salida` = p_carril_salida,
            `fecha_salida` = NOW(3),
            `monto_calculado` = p_monto_total,
            `updated_at` = NOW(3)
        WHERE `id` = p_stay_id;

        INSERT INTO `access_events` (
            `id`, `lane_id`, `lane_type`, `placa_normalizada`, `confianza_lpr`, `decision`, `motivo`, `timestamp`
        ) VALUES (
            UUID(), p_carril_salida, 'EXIT', p_placa, 0.98, 'ALLOW', CONCAT('Salida completada. Importe: $', p_monto_total, ' MXN'), NOW(3)
        );
    END IF;
END$$

DROP PROCEDURE IF EXISTS `sp_actualizar_telemetria_cajon`$$
CREATE PROCEDURE `sp_actualizar_telemetria_cajon`(
    IN p_bay_number      INT,
    IN p_distancia_cm    DOUBLE,
    IN p_placa_detectada VARCHAR(20)
)
BEGIN
    DECLARE v_nuevo_estado VARCHAR(20);
    DECLARE v_distancia_str VARCHAR(30);

    SET v_distancia_str = CONCAT(ROUND(p_distancia_cm, 1), ' cm');

    IF p_distancia_cm < 50.0 THEN
        SET v_nuevo_estado = 'OCCUPIED';
    ELSE
        SET v_nuevo_estado = 'FREE';
        SET p_placa_detectada = '—';
        SET v_distancia_str = '> 150 cm';
    END IF;

    UPDATE `parking_bays`
    SET `status` = v_nuevo_estado,
        `current_plate` = CASE WHEN v_nuevo_estado = 'OCCUPIED' AND p_placa_detectada IS NOT NULL THEN p_placa_detectada ELSE `current_plate` END,
        `sensor_distance` = v_distancia_str,
        `occupied_since` = CASE 
            WHEN v_nuevo_estado = 'OCCUPIED' AND `status` != 'OCCUPIED' THEN NOW(3)
            WHEN v_nuevo_estado = 'FREE' THEN NULL
            ELSE `occupied_since`
        END,
        `updated_at` = NOW(3)
    WHERE `bay_number` = p_bay_number;
END$$

DELIMITER ;

-- ------------------------------------------------------------------------------
-- 5. SEMILLAS DE DATOS INICIALES (SEEDS)
-- ------------------------------------------------------------------------------

INSERT INTO `terminals` (`id`, `codigo`, `nombre`, `is_active`)
VALUES 
('t1-agu-main', 'AGU-T1', 'Terminal 1 Principal - Aeropuerto Internacional Jesús Terán Peredo', 1)
ON DUPLICATE KEY UPDATE `nombre` = VALUES(`nombre`);

INSERT INTO `zones` (`id`, `terminal_id`, `nombre`, `nivel`, `capacidad_total`, `tipo_cajon`, `is_active`)
VALUES 
('z1-covered-lvl1', 't1-agu-main', 'Zona A - Larga Estancia Techada', 'Nivel 1', 120, 'COVERED', 1),
('z2-express-lvl2', 't1-agu-main', 'Zona B - Rotación Rápida / Express', 'Nivel 2', 80, 'STANDARD', 1),
('z3-ev-lvl1',      't1-agu-main', 'Zona C - Electromovilidad (Cargadores EV)', 'Nivel 1', 20, 'EV', 1)
ON DUPLICATE KEY UPDATE `nombre` = VALUES(`nombre`);

INSERT INTO `tariffs` (`id`, `version`, `fraccion_minutos`, `costo_fraccion`, `tope_diario`, `tolerancia_salida_minutos`, `vigencia_desde`, `vigencia_hasta`, `is_active`)
VALUES 
('tar-v1-2026', 1, 15, 11.25, 350.00, 15, '2026-01-01 00:00:00', NULL, 1)
ON DUPLICATE KEY UPDATE `costo_fraccion` = VALUES(`costo_fraccion`);

INSERT INTO `customers` (`id`, `external_id`, `nombre`, `correo_cifrado`, `correo_hash`, `telefono_cifrado`, `password_hash`, `password_salt`, `idioma`, `estado`, `role`)
VALUES 
(
    'cust-01', 
    'EXT-LAURA-01', 
    'Laura Sánchez', 
    'AEROPARK_AES256GCM_ENCRYPTED_laura@aeropark.mx', 
    'd85fe669527ecb8ec3be1f4cf17fe89f7f45b53d456fae85ef05c2a129033481', 
    'AEROPARK_AES256GCM_ENCRYPTED_+524491234567', 
    'n64tQ/E1sJ3a8vKk2hH9hZkM1w2t09v2=dummyargon2idhash', 
    'randomsalt128bitlaura', 
    'es-MX', 
    'ACTIVE', 
    'CUSTOMER'
),
(
    'admin-01', 
    'EXT-CARLOS-ADM', 
    'Carlos Mendoza (Supervisor de Operaciones)', 
    'AEROPARK_AES256GCM_ENCRYPTED_admin@aeropark.mx', 
    '8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918', 
    'AEROPARK_AES256GCM_ENCRYPTED_+524499998888', 
    'z98tQ/E1sJ3a8vKk2hH9hZkM1w2t09v2=dummyargon2idhash', 
    'randomsalt128bitadmin', 
    'es-MX', 
    'ACTIVE', 
    'ADMIN'
)
ON DUPLICATE KEY UPDATE `nombre` = VALUES(`nombre`), `role` = VALUES(`role`);

INSERT INTO `vehicles` (`id`, `customer_id`, `placa_normalizada`, `alias`, `marca`, `modelo`, `color`, `es_predeterminado`, `is_active`)
VALUES 
('veh-01', 'cust-01', 'ABC123D', 'Tesla Blanco', 'Tesla', 'Model 3 Performance', 'Blanco Perla', 1, 1),
('veh-02', 'cust-01', 'XYZ789A', 'Porsche Macan', 'Porsche', 'Macan GTS', 'Gris Volcánico', 0, 1)
ON DUPLICATE KEY UPDATE `marca` = VALUES(`marca`), `modelo` = VALUES(`modelo`);

INSERT INTO `parking_bays` (`id`, `bay_number`, `name`, `terminal_id`, `zone_id`, `status`, `current_plate`, `current_vehicle`, `sensor_distance`, `occupied_since`, `rate_per_hour`, `gpio_trig`, `gpio_echo`)
VALUES 
('bay-01', 1, 'Cajón 01', 't1-agu-main', 'z1-covered-lvl1', 'OCCUPIED', 'ABC-123-D', 'Tesla Model 3 Performance', '18.2 cm', DATE_SUB(NOW(3), INTERVAL 165 MINUTE), 45.00, 23, 22),
('bay-02', 2, 'Cajón 02', 't1-agu-main', 'z1-covered-lvl1', 'FREE',     '—',         'Despejado',                 '> 150 cm', NULL,                                45.00, 25, 24),
('bay-03', 3, 'Cajón 03', 't1-agu-main', 'z3-ev-lvl1',      'EV',       'JLM-456-E', 'Audi e-tron GT (Carga EV)', '22.0 cm', DATE_SUB(NOW(3), INTERVAL 72 MINUTE),  65.00, 27, 26),
('bay-04', 4, 'Cajón 04', 't1-agu-main', 'z2-express-lvl2', 'OCCUPIED', 'XYZ-789-A', 'Porsche Macan GTS',          '16.4 cm', DATE_SUB(NOW(3), INTERVAL 250 MINUTE), 45.00, 6,  5)
ON DUPLICATE KEY UPDATE `status` = VALUES(`status`), `current_plate` = VALUES(`current_plate`);

INSERT INTO `list_entries` (`id`, `placa_normalizada`, `tipo_lista`, `justificacion`, `vigencia_hasta`, `is_active`)
VALUES 
('le-sec-01', 'SEG001MX', 'WHITE_LIST', 'Patrulla de Seguridad y Vigilancia Perimetral #01', NULL, 1),
('le-amb-01', 'AMB999MX', 'WHITE_LIST', 'Ambulancia de Emergencias CREI Aeropuerto', NULL, 1),
('le-bom-01', 'SOS000MX', 'WHITE_LIST', 'Unidad de Rescate y Extinción de Incendios AFAC', NULL, 1),
('le-blk-01', 'MAL001RO', 'BLACK_LIST', 'Vehículo con orden judicial por evasión de pago recurrente', NULL, 1)
ON DUPLICATE KEY UPDATE `justificacion` = VALUES(`justificacion`);

INSERT INTO `security_alerts` (`id`, `placa_normalizada`, `delito_reportado`, `fuente_boletin`, `confianza_coincidencia`, `estado_dictamen`, `dictaminado_por`, `justificacion_dictamen`, `fecha_dictamen`)
VALUES 
(
    'alert-01', 
    'ROB404MX', 
    'Reporte de Robo con Violencia y Portación de Armas (C5 / Fiscalía)', 
    'Plataforma Nacional de Alertas / C5', 
    0.9520, 
    'PENDING_REVIEW', 
    NULL, 
    'Detección automática por cámara LPR en Carril 1. Pluma bloqueada por seguridad preventiva.', 
    NULL
)
ON DUPLICATE KEY UPDATE `delito_reportado` = VALUES(`delito_reportado`);

INSERT INTO `stays` (`id`, `booking_id`, `placa_normalizada`, `carril_entrada`, `fecha_ingreso`, `carril_salida`, `fecha_salida`, `monto_calculado`, `estado_cobro`)
VALUES 
('stay-01', NULL, 'ABC123D', 'carril-lpr-01', DATE_SUB(NOW(3), INTERVAL 165 MINUTE), NULL, NULL, 0.00, 'PENDING')
ON DUPLICATE KEY UPDATE `placa_normalizada` = VALUES(`placa_normalizada`);

INSERT INTO `access_events` (`id`, `lane_id`, `lane_type`, `placa_normalizada`, `confianza_lpr`, `decision`, `motivo`, `timestamp`)
VALUES 
(UUID(), 'carril-lpr-01', 'ENTRY', 'ABC123D', 0.985, 'ALLOW', 'Acceso autorizado. Pluma levantada a 90°.', DATE_SUB(NOW(3), INTERVAL 165 MINUTE)),
(UUID(), 'carril-lpr-01', 'ENTRY', 'XYZ789A', 0.970, 'ALLOW', 'Acceso autorizado. Estancia iniciada.', DATE_SUB(NOW(3), INTERVAL 250 MINUTE)),
(UUID(), 'carril-lpr-01', 'ENTRY', 'SEG001MX', 0.999, 'ALLOW', 'Vehículo de Seguridad Oficial exento.', DATE_SUB(NOW(3), INTERVAL 60 MINUTE));

INSERT INTO `payments` (`id`, `stay_id`, `booking_id`, `monto`, `moneda`, `metodo`, `psp_reference`, `idempotency_key`, `estado`, `recibo_url`)
VALUES 
('pay-demo-01', NULL, NULL, 180.00, 'MXN', 'apple_pay', 'psp_apple_demo_987123', 'idemp_key_initial_180', 'SUCCEEDED', 'https://aeropark.mx/receipts/rec-987123.pdf')
ON DUPLICATE KEY UPDATE `monto` = VALUES(`monto`);

INSERT INTO `system_settings` (`clave`, `valor`, `descripcion`)
VALUES 
('BARRIER_STATUS', 'CLOSED', 'Estado actual de la pluma (OPEN, CLOSED, LOCKED_EMERGENCY)'),
('EMERGENCY_LOCKDOWN', '0', 'Bandera de Código Rojo (1 = Bloqueo total activo, 0 = Operación normal)'),
('SUPERVISOR_PIN', '9911', 'PIN numérico de autorización para desactivación de emergencia'),
('LPR_MIN_CONFIDENCE', '0.85', 'Umbral mínimo de certeza OCR para apertura automática')
ON DUPLICATE KEY UPDATE `valor` = VALUES(`valor`);

SET FOREIGN_KEY_CHECKS = 1;

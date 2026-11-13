-- ==============================================================================
-- 02_views_procedures.sql: Vistas de Negocio y Procedimientos Almacenados
-- Motor: MySQL 8.0+ / Azure Database for MySQL
-- ==============================================================================

USE `airport_parking`;

-- ------------------------------------------------------------------------------
-- 1. Vistas Analíticas y Operativas
-- ------------------------------------------------------------------------------

-- Vista 1: Gemelo Digital de Cajones IoT en Tiempo Real
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

-- Vista 2: Estancias Activas en el Aeropuerto
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

-- Vista 3: Métricas y KPIs Operativos del Día
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
-- 2. Procedimientos Almacenados de Alto Rendimiento
-- ------------------------------------------------------------------------------

DELIMITER $$

-- Procedimiento 1: Evaluación y Registro de Ingreso Vehicular por LPR
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

    -- 1. Validar Alerta de Seguridad (Código Rojo / Delito Grave)
    SELECT COUNT(*) INTO v_es_alerta 
    FROM `security_alerts` 
    WHERE `placa_normalizada` = p_placa 
      AND `estado_dictamen` IN ('PENDING_REVIEW', 'CONFIRMED');

    IF v_es_alerta > 0 THEN
        SET p_decision = 'REVIEW_REQUIRED';
        SET p_motivo = 'ALERTA DE SEGURIDAD: Placa con reporte policial activo en C5 / Fiscalía. Pluma bloqueada.';
    ELSE
        -- 2. Validar Lista Restringida (Blacklist)
        SELECT COUNT(*) INTO v_es_lista_negra 
        FROM `list_entries` 
        WHERE `placa_normalizada` = p_placa 
          AND `tipo_lista` = 'BLACK_LIST' 
          AND `is_active` = 1;

        IF v_es_lista_negra > 0 THEN
            SET p_decision = 'DENIED';
            SET p_motivo = 'ACCESO DENEGADO: Vehículo registrado en lista negra de control vehicular.';
        ELSE
            -- 3. Acceso Autorizado (Lista Blanca o Flujo Regular)
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

            -- Crear nueva estancia si no tiene una activa
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

    -- Registrar evento en bitácora LPR
    INSERT INTO `access_events` (
        `id`, `lane_id`, `lane_type`, `placa_normalizada`, `confianza_lpr`, `decision`, `motivo`, `evidencia_image_url`, `timestamp`
    ) VALUES (
        v_uuid_event, p_carril, 'ENTRY', p_placa, p_confianza_ocr, p_decision, p_motivo, p_evidencia_url, NOW(3)
    );
END$$


-- Procedimiento 2: Cálculo y Registro de Salida Vehicular
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

    -- Buscar estancia activa
    SELECT `id`, `fecha_ingreso`, `estado_cobro` 
    INTO p_stay_id, v_fecha_ingreso, p_estado_cobro
    FROM `stays` 
    WHERE `placa_normalizada` = p_placa AND `fecha_salida` IS NULL 
    ORDER BY `fecha_ingreso` DESC 
    LIMIT 1;

    IF p_stay_id IS NOT NULL THEN
        SET p_minutos_transcurridos = TIMESTAMPDIFF(MINUTE, v_fecha_ingreso, NOW());

        -- Obtener tarifa vigente activa
        SELECT `fraccion_minutos`, `costo_fraccion`, `tope_diario`, `tolerancia_salida_minutos`
        INTO v_fraccion_min, v_costo_fracc, v_tope_dia, v_tolerancia
        FROM `tariffs`
        WHERE `is_active` = 1
        ORDER BY `version` DESC
        LIMIT 1;

        -- Aplicar tolerancia de salida
        IF p_minutos_transcurridos <= v_tolerancia OR p_estado_cobro = 'EXEMPT' THEN
            SET p_monto_total = 0.00;
        ELSE
            -- Cálculo por fracciones de 15 minutos (techo matemático)
            SET v_fracciones = CEIL(p_minutos_transcurridos / v_fraccion_min);
            SET p_monto_total = v_fracciones * v_costo_fracc;

            -- Aplicar tope diario si excede 24 horas o tope establecido
            IF p_monto_total > v_tope_dia THEN
                SET p_monto_total = v_tope_dia;
            END IF;
        END IF;

        -- Actualizar estancia con salida
        UPDATE `stays`
        SET `carril_salida` = p_carril_salida,
            `fecha_salida` = NOW(3),
            `monto_calculado` = p_monto_total,
            `updated_at` = NOW(3)
        WHERE `id` = p_stay_id;

        -- Registrar evento de salida
        INSERT INTO `access_events` (
            `id`, `lane_id`, `lane_type`, `placa_normalizada`, `confianza_lpr`, `decision`, `motivo`, `timestamp`
        ) VALUES (
            UUID(), p_carril_salida, 'EXIT', p_placa, 0.98, 'ALLOW', CONCAT('Salida completada. Importe: $', p_monto_total, ' MXN'), NOW(3)
        );
    END IF;
END$$


-- Procedimiento 3: Actualización de Telemetría IoT de Cajón
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

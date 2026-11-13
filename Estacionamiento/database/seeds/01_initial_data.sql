-- ==============================================================================
-- 01_initial_data.sql: Semillas Iniciales para Desarrollo y Pruebas
-- Motor: MySQL 8.0+ / Azure Database for MySQL
-- ==============================================================================

USE `airport_parking`;

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- 1. Terminales y Zonas
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

-- 2. Tarifa Vigente ($45/hora = $11.25 por fracción de 15m)
INSERT INTO `tariffs` (`id`, `version`, `fraccion_minutos`, `costo_fraccion`, `tope_diario`, `tolerancia_salida_minutos`, `vigencia_desde`, `vigencia_hasta`, `is_active`)
VALUES 
('tar-v1-2026', 1, 15, 11.25, 350.00, 15, '2026-01-01 00:00:00', NULL, 1)
ON DUPLICATE KEY UPDATE `costo_fraccion` = VALUES(`costo_fraccion`);

-- 3. Clientes Demo (Laura Sánchez - Cliente | Carlos Mendoza - Supervisor Admin)
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

-- 4. Vehículos
INSERT INTO `vehicles` (`id`, `customer_id`, `placa_normalizada`, `alias`, `marca`, `modelo`, `color`, `es_predeterminado`, `is_active`)
VALUES 
('veh-01', 'cust-01', 'ABC123D', 'Tesla Blanco', 'Tesla', 'Model 3 Performance', 'Blanco Perla', 1, 1),
('veh-02', 'cust-01', 'XYZ789A', 'Porsche Macan', 'Porsche', 'Macan GTS', 'Gris Volcánico', 0, 1)
ON DUPLICATE KEY UPDATE `marca` = VALUES(`marca`), `modelo` = VALUES(`modelo`);

-- 5. Cajones IoT
INSERT INTO `parking_bays` (`id`, `bay_number`, `name`, `terminal_id`, `zone_id`, `status`, `current_plate`, `current_vehicle`, `sensor_distance`, `occupied_since`, `rate_per_hour`, `gpio_trig`, `gpio_echo`)
VALUES 
('bay-01', 1, 'Cajón 01', 't1-agu-main', 'z1-covered-lvl1', 'OCCUPIED', 'ABC-123-D', 'Tesla Model 3 Performance', '18.2 cm', DATE_SUB(NOW(3), INTERVAL 165 MINUTE), 45.00, 23, 22),
('bay-02', 2, 'Cajón 02', 't1-agu-main', 'z1-covered-lvl1', 'FREE',     '—',         'Despejado',                 '> 150 cm', NULL,                                45.00, 25, 24),
('bay-03', 3, 'Cajón 03', 't1-agu-main', 'z3-ev-lvl1',      'EV',       'JLM-456-E', 'Audi e-tron GT (Carga EV)', '22.0 cm', DATE_SUB(NOW(3), INTERVAL 72 MINUTE),  65.00, 27, 26),
('bay-04', 4, 'Cajón 04', 't1-agu-main', 'z2-express-lvl2', 'OCCUPIED', 'XYZ-789-A', 'Porsche Macan GTS',          '16.4 cm', DATE_SUB(NOW(3), INTERVAL 250 MINUTE), 45.00, 6,  5)
ON DUPLICATE KEY UPDATE `status` = VALUES(`status`), `current_plate` = VALUES(`current_plate`);

-- 6. Lista Blanca
INSERT INTO `list_entries` (`id`, `placa_normalizada`, `tipo_lista`, `justificacion`, `vigencia_hasta`, `is_active`)
VALUES 
('le-sec-01', 'SEG001MX', 'WHITE_LIST', 'Patrulla de Seguridad y Vigilancia Perimetral #01', NULL, 1),
('le-amb-01', 'AMB999MX', 'WHITE_LIST', 'Ambulancia de Emergencias CREI Aeropuerto', NULL, 1),
('le-bom-01', 'SOS000MX', 'WHITE_LIST', 'Unidad de Rescate y Extinción de Incendios AFAC', NULL, 1),
('le-blk-01', 'MAL001RO', 'BLACK_LIST', 'Vehículo con orden judicial por evasión de pago recurrente', NULL, 1)
ON DUPLICATE KEY UPDATE `justificacion` = VALUES(`justificacion`);

-- 7. Alerta de Seguridad
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

-- 8. Estancias y Eventos
INSERT INTO `stays` (`id`, `booking_id`, `placa_normalizada`, `carril_entrada`, `fecha_ingreso`, `carril_salida`, `fecha_salida`, `monto_calculado`, `estado_cobro`)
VALUES 
('stay-01', NULL, 'ABC123D', 'carril-lpr-01', DATE_SUB(NOW(3), INTERVAL 165 MINUTE), NULL, NULL, 0.00, 'PENDING')
ON DUPLICATE KEY UPDATE `placa_normalizada` = VALUES(`placa_normalizada`);

INSERT INTO `access_events` (`id`, `lane_id`, `lane_type`, `placa_normalizada`, `confianza_lpr`, `decision`, `motivo`, `timestamp`)
VALUES 
(UUID(), 'carril-lpr-01', 'ENTRY', 'ABC123D', 0.985, 'ALLOW', 'Acceso autorizado. Pluma levantada a 90°.', DATE_SUB(NOW(3), INTERVAL 165 MINUTE)),
(UUID(), 'carril-lpr-01', 'ENTRY', 'XYZ789A', 0.970, 'ALLOW', 'Acceso autorizado. Estancia iniciada.', DATE_SUB(NOW(3), INTERVAL 250 MINUTE)),
(UUID(), 'carril-lpr-01', 'ENTRY', 'SEG001MX', 0.999, 'ALLOW', 'Vehículo de Seguridad Oficial exento.', DATE_SUB(NOW(3), INTERVAL 60 MINUTE));

-- 9. Pagos
INSERT INTO `payments` (`id`, `stay_id`, `booking_id`, `monto`, `moneda`, `metodo`, `psp_reference`, `idempotency_key`, `estado`, `recibo_url`)
VALUES 
('pay-demo-01', NULL, NULL, 180.00, 'MXN', 'apple_pay', 'psp_apple_demo_987123', 'idemp_key_initial_180', 'SUCCEEDED', 'https://aeropark.mx/receipts/rec-987123.pdf')
ON DUPLICATE KEY UPDATE `monto` = VALUES(`monto`);

-- 10. Configuración
INSERT INTO `system_settings` (`clave`, `valor`, `descripcion`)
VALUES 
('BARRIER_STATUS', 'CLOSED', 'Estado actual de la pluma (OPEN, CLOSED, LOCKED_EMERGENCY)'),
('EMERGENCY_LOCKDOWN', '0', 'Bandera de Código Rojo (1 = Bloqueo total activo, 0 = Operación normal)'),
('SUPERVISOR_PIN', '9911', 'PIN numérico de autorización para desactivación de emergencia'),
('LPR_MIN_CONFIDENCE', '0.85', 'Umbral mínimo de certeza OCR para apertura automática')
ON DUPLICATE KEY UPDATE `valor` = VALUES(`valor`);

SET FOREIGN_KEY_CHECKS = 1;

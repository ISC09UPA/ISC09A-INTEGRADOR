-- ==============================================================================
-- 01_initial_data.sql: Semillas Iniciales para Desarrollo y Pruebas
-- ==============================================================================

SET NAMES utf8mb4;

-- Terminales y Zonas
INSERT IGNORE INTO `terminals` (`id`, `codigo`, `nombre`, `is_active`)
VALUES ('t1-agu-main', 'AGU-T1', 'Terminal 1 Principal - Aeropuerto Internacional', 1);

INSERT IGNORE INTO `zones` (`id`, `terminal_id`, `nombre`, `nivel`, `capacidad_total`, `tipo_cajon`, `is_active`)
VALUES 
('z1-standard-lvl1', 't1-agu-main', 'Zona A - Larga Estancia Techada', 'Nivel 1', 250, 'COVERED', 1),
('z2-standard-lvl2', 't1-agu-main', 'Zona B - Rotación Express', 'Nivel 2', 250, 'STANDARD', 1);

-- Tarifa Inicial Vigente (Versión 1)
-- Fracción: 15 min a $20.00 MXN ($80/hora), Tope Diario: $350.00 MXN, Gracia: 15 min
INSERT IGNORE INTO `tariffs` (`id`, `version`, `fraccion_minutos`, `costo_fraccion`, `tope_diario`, `tolerancia_salida_minutos`, `vigencia_desde`, `vigencia_hasta`, `is_active`)
VALUES 
('tar-v1-2026', 1, 15, 20.00, 350.00, 15, '2026-01-01 00:00:00', NULL, 1);

-- Lista Blanca Inicial (Vehículos de Seguridad y Servicios del Aeropuerto)
INSERT IGNORE INTO `list_entries` (`id`, `placa_normalizada`, `tipo_lista`, `justificacion`, `vigencia_hasta`, `is_active`)
VALUES 
('le-sec-01', 'SEG001MX', 'WHITE_LIST', 'Patrulla de Seguridad Aeroportuaria #1', NULL, 1),
('le-amb-01', 'AMB999MX', 'WHITE_LIST', 'Ambulancia de Emergencias Médicas', NULL, 1);

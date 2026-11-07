import sqlite3
from datetime import datetime

import config

# Tablas de la base: lecturas, riegos y fotos
ESQUEMA = """
CREATE TABLE IF NOT EXISTS lecturas (
    id             INTEGER PRIMARY KEY AUTOINCREMENT,
    fecha          TEXT    NOT NULL,          -- ISO 8601 con zona horaria
    humedad        INTEGER,                   -- %; NULL si falló el sensor
    temperatura    REAL,                      -- °C; NULL si falló el DHT22
    luz            REAL,                      -- lux; NULL si falló el BH1750
    nivel          INTEGER,                   -- %; NULL si solo hay flotador
    flotador_vacio INTEGER NOT NULL CHECK (flotador_vacio IN (0, 1)),
    caudal_lmin    REAL    NOT NULL,
    bomba          INTEGER NOT NULL CHECK (bomba IN (0, 1)),
    ventilador     INTEGER NOT NULL CHECK (ventilador IN (0, 1)),
    enviado        INTEGER NOT NULL DEFAULT 0 CHECK (enviado IN (0, 1))
);

CREATE TABLE IF NOT EXISTS riegos (
    id             INTEGER PRIMARY KEY AUTOINCREMENT,
    inicio         TEXT    NOT NULL,
    fin            TEXT    NOT NULL,
    duracion_s     REAL    NOT NULL,
    agua_ml        REAL    NOT NULL,
    tipo           TEXT    NOT NULL CHECK (tipo IN ('automatico', 'manual')),
    motivo_fin     TEXT    NOT NULL
                   CHECK (motivo_fin IN ('humedad_objetivo', 'tiempo_maximo', 'sin_agua', 'detenido')),
    humedad_inicio INTEGER,
    humedad_fin    INTEGER,
    enviado        INTEGER NOT NULL DEFAULT 0 CHECK (enviado IN (0, 1))
);

CREATE TABLE IF NOT EXISTS fotos (
    id             INTEGER PRIMARY KEY AUTOINCREMENT,
    fecha          TEXT    NOT NULL,
    ruta           TEXT    NOT NULL,
    enviado        INTEGER NOT NULL DEFAULT 0 CHECK (enviado IN (0, 1))
);

CREATE INDEX IF NOT EXISTS idx_lecturas_pendientes ON lecturas (id) WHERE enviado = 0;
CREATE INDEX IF NOT EXISTS idx_riegos_pendientes   ON riegos (id)   WHERE enviado = 0;
CREATE INDEX IF NOT EXISTS idx_fotos_pendientes    ON fotos (id)    WHERE enviado = 0;
"""

# Tablas permitidas
TABLAS = ("lecturas", "riegos", "fotos")


# Fecha y hora actual en texto
def fecha_actual():
    return datetime.now().astimezone().isoformat(timespec="seconds")


# Base de datos local
class BaseLocal:
    # Abre la base y crea las tablas si no existen
    def __init__(self, ruta=config.RUTA_DB_LOCAL):
        self._conexion = sqlite3.connect(ruta)
        self._conexion.row_factory = sqlite3.Row
        self._conexion.execute("PRAGMA journal_mode=WAL")  # aguanta mejor un corte de luz
        self._conexion.executescript(ESQUEMA)

    # Guarda una lectura de sensores
    def guardar_lectura(self, humedad, temperatura, luz, nivel, flotador_vacio, caudal_lmin,
                        bomba, ventilador):
        with self._conexion:
            self._conexion.execute(
                "INSERT INTO lecturas (fecha, humedad, temperatura, luz, nivel, flotador_vacio,"
                " caudal_lmin, bomba, ventilador) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)",
                (fecha_actual(), humedad, temperatura, luz, nivel, int(flotador_vacio),
                 caudal_lmin, int(bomba), int(ventilador)))

    # Guarda un riego terminado
    def guardar_riego(self, inicio, duracion_s, agua_ml, manual, motivo_fin,
                      humedad_inicio, humedad_fin):
        with self._conexion:
            self._conexion.execute(
                "INSERT INTO riegos (inicio, fin, duracion_s, agua_ml, tipo, motivo_fin,"
                " humedad_inicio, humedad_fin) VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
                (inicio, fecha_actual(), round(duracion_s, 1), round(agua_ml, 1),
                 "manual" if manual else "automatico", motivo_fin, humedad_inicio, humedad_fin))

    # Guarda la ruta de una foto
    def guardar_foto(self, ruta):
        with self._conexion:
            self._conexion.execute("INSERT INTO fotos (fecha, ruta) VALUES (?, ?)",
                                   (fecha_actual(), str(ruta)))

    # Devuelve las filas que faltan por enviar a la nube
    def pendientes(self, tabla, limite=100):
        _validar(tabla)
        filas = self._conexion.execute(
            f"SELECT * FROM {tabla} WHERE enviado = 0 ORDER BY id LIMIT ?", (limite,))
        return [dict(fila) for fila in filas]

    # Marca filas como ya enviadas
    def marcar_enviados(self, tabla, ids):
        _validar(tabla)
        with self._conexion:
            self._conexion.executemany(f"UPDATE {tabla} SET enviado = 1 WHERE id = ?",
                                       [(id_,) for id_ in ids])

    # Devuelve las últimas filas guardadas
    def ultimas(self, tabla, cantidad=10):
        _validar(tabla)
        filas = self._conexion.execute(f"SELECT * FROM {tabla} ORDER BY id DESC LIMIT ?",
                                       (cantidad,))
        return [dict(fila) for fila in filas]

    # Cierra la base de datos
    def cerrar(self):
        self._conexion.close()


# Revisa que el nombre de tabla sea válido
def _validar(tabla):
    if tabla not in TABLAS:  # el nombre va dentro del SQL, así que solo se aceptan las conocidas
        raise ValueError(f"tabla desconocida: {tabla}")

import sys
import threading
import time

import config
from actuadores.bomba import Bomba
from actuadores.indicadores import Indicadores
from actuadores.ventilador import Ventilador
from almacenamiento.db_local import BaseLocal, fecha_actual
from sensores.caudal import Caudalimetro
from sensores.humedad_suelo import SensorHumedad
from sensores.luz import SensorLuz
from sensores.nivel_agua import SensorNivel
from sensores.temperatura import SensorTemperatura

# Cada cuánto se revisa el riego y cada cuánto se imprime en pantalla
PERIODO_CONTROL_S = 0.5
PERIODO_REPORTE_S = 1


# Decide cuándo encender y apagar la bomba
class ControlRiego:
    # Guarda los dispositivos y el estado del riego
    def __init__(self, bomba, caudalimetro, indicadores, db):
        self.bomba = bomba
        self.caudalimetro = caudalimetro
        self.indicadores = indicadores
        self.db = db
        self.riego_manual = False
        self._inicio_riego = 0.0
        self._inicio_fecha = ""
        self._humedad_inicio = None
        self._fin_ultimo_riego = None  # None = todavía no ha regado
        self._pulsos_inicio = 0

    # Pide un riego manual (desde la terminal o la app)
    def solicitar_riego_manual(self):
        self.riego_manual = True

    # Revisa las condiciones y enciende o apaga la bomba
    def actualizar(self, humedad, sin_agua):
        ahora = time.monotonic()
        # Si está regando, ver si ya debe parar
        if self.bomba.encendida:
            tiempo_agotado = ahora - self._inicio_riego >= config.TIEMPO_MAX_BOMBA_S
            listo = (not self.riego_manual and humedad is not None
                     and humedad >= config.HUMEDAD_OBJETIVO)
            if sin_agua:
                self._apagar(ahora, humedad, "sin_agua")
            elif tiempo_agotado:
                self._apagar(ahora, humedad, "tiempo_maximo")
            elif listo:
                self._apagar(ahora, humedad, "humedad_objetivo")
        # Si no está regando y hay agua, ver si debe empezar
        elif not sin_agua:
            espera_cumplida = (self._fin_ultimo_riego is None
                               or ahora - self._fin_ultimo_riego >= config.ESPERA_ENTRE_RIEGOS_S)
            seco = humedad is not None and humedad < config.HUMEDAD_MINIMA
            if self.riego_manual or (seco and espera_cumplida):
                self._encender(ahora, humedad)
        # Sin agua no se puede hacer el riego manual
        elif self.riego_manual:
            print("!! Riego manual cancelado: no hay agua")
            self.riego_manual = False

    # Apaga la bomba al cerrar el programa
    def detener(self, humedad=None):
        if self.bomba.encendida:
            self._apagar(time.monotonic(), humedad, "detenido")

    # Enciende la bomba y anota cuándo empezó el riego
    def _encender(self, ahora, humedad):
        self.bomba.encender()
        self.indicadores.regando.on()
        self._inicio_riego = ahora
        self._inicio_fecha = fecha_actual()
        self._humedad_inicio = humedad
        self._pulsos_inicio = self.caudalimetro.pulsos
        print(">> Riego MANUAL iniciado" if self.riego_manual else ">> Riego automatico iniciado")

    # Apaga la bomba, calcula el agua usada y guarda el riego
    def _apagar(self, ahora, humedad, motivo):
        self.bomba.apagar()
        self.indicadores.regando.off()
        self._fin_ultimo_riego = ahora
        duracion = ahora - self._inicio_riego
        agua_ml = self.caudalimetro.mililitros(self.caudalimetro.pulsos - self._pulsos_inicio)
        print(f">> Riego terminado ({motivo}). Duracion: {duracion:.1f} s, agua: {agua_ml:.1f} mL")
        self.db.guardar_riego(self._inicio_fecha, duracion, agua_ml, self.riego_manual, motivo,
                              self._humedad_inicio, humedad)
        self.riego_manual = False


# Escucha la terminal: r + Enter pide un riego manual
def escuchar_terminal(control):
    for linea in sys.stdin:
        if linea.strip().lower() == "r":
            control.solicitar_riego_manual()


# Convierte un valor a texto para mostrarlo ("--" si no hay lectura)
def texto(valor, formato="", error=False):
    if error:
        return "ERR"
    return "--" if valor is None else format(valor, formato)


def main():
    # Crear sensores, actuadores y base de datos
    sensor_temp = SensorTemperatura(config.PIN_DHT22)
    sensor_humedad = SensorHumedad()
    sensor_nivel = SensorNivel()
    sensor_luz = SensorLuz()
    caudalimetro = Caudalimetro(config.PIN_CAUDALIMETRO)
    bomba = Bomba()
    ventilador = Ventilador()
    indicadores = Indicadores()
    db = BaseLocal()
    control = ControlRiego(bomba, caudalimetro, indicadores, db)
    print(f"Modo local: los datos se guardan en {config.RUTA_DB_LOCAL}")

    # Iniciar la cámara (si falla, el sistema sigue sin ella)
    try:
        from sensores.camara import Camara  # aquí y no arriba: si falta picamera2, el riego sigue
        camara = Camara()
    except Exception as e:
        print(f"!! Cámara no disponible: {e}")
        camara = None

    # Escuchar la terminal en segundo plano y prender el LED verde
    threading.Thread(target=escuchar_terminal, args=(control,), daemon=True).start()
    indicadores.funcionando.on()
    print("Sistema de riego iniciado. Escribe r + Enter para riego manual.")

    # Ciclo principal
    ultimo_reporte = ultimo_envio = time.monotonic()
    ultima_foto = None
    try:
        while True:
            ahora = time.monotonic()

            # Leer todos los sensores
            temperatura = sensor_temp.actualizar()
            caudal = caudalimetro.actualizar()
            humedad = sensor_humedad.leer()
            nivel = sensor_nivel.porcentaje()
            luz = sensor_luz.leer()
            flotador_vacio = sensor_nivel.flotador_vacio
            sin_agua = flotador_vacio or (nivel is not None and nivel < config.NIVEL_MINIMO)

            # LED rojo si falla algún sensor
            indicadores.error.value = sensor_temp.error or humedad is None

            # Controlar bomba y ventilador
            control.actualizar(humedad, sin_agua)
            ventilador.actualizar(temperatura)

            # Mostrar lecturas en pantalla
            if ahora - ultimo_reporte >= PERIODO_REPORTE_S:
                ultimo_reporte = ahora
                print(f"Hum {texto(humedad)}% | Nivel {texto(nivel)}% | Luz {texto(luz, '.0f')} lx"
                      f" | Temp {texto(temperatura, '.1f', sensor_temp.error)}C"
                      f" | Caudal {caudal:.2f} L/min"
                      f" | Flotador {'VACIO' if flotador_vacio else 'OK'}"
                      f" | Bomba {'ON' if bomba.encendida else 'off'}"
                      f" | Vent {'ON' if ventilador.encendido else 'off'}")

            # Guardar lecturas en la base de datos
            if ahora - ultimo_envio >= config.INTERVALO_LECTURA_S:
                ultimo_envio = ahora
                db.guardar_lectura(humedad, temperatura, luz, nivel, flotador_vacio, caudal,
                                   bomba.encendida, ventilador.encendido)

            # Tomar foto de la planta
            if camara and (ultima_foto is None or ahora - ultima_foto >= config.INTERVALO_FOTO_S):
                ultima_foto = ahora
                try:
                    ruta = camara.tomar_foto()
                    db.guardar_foto(ruta)
                    print(f">> Foto guardada: {ruta}")
                except Exception as e:
                    print(f"!! No se pudo tomar la foto: {e}")

            time.sleep(PERIODO_CONTROL_S)
    except KeyboardInterrupt:
        print("\nDeteniendo...")
    # Al salir: apagar todo y cerrar conexiones
    finally:
        control.detener()
        db.cerrar()
        bomba.apagar()
        ventilador.apagar()
        indicadores.apagar_todo()
        sensor_temp.cerrar()
        sensor_luz.cerrar()
        if camara:
            camara.cerrar()


if __name__ == "__main__":
    main()

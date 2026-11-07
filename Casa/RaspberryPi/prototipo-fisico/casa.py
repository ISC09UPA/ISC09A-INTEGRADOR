#!/usr/bin/env python3
"""Casa domótica local: python3 casa.py --modo simulacion|raspberry."""
import argparse
import os
import queue
import subprocess
import threading
from datetime import datetime, timezone

PIR, DHT, SERVO, BUZZER, ROJO, BLANCO = 16, 5, 18, 23, 24, 25  # BCM


class Simulacion:
    def __init__(self):
        self.movimiento = False

    def presencia(self):
        return self.movimiento

    def alarma(self, activa):
        print(f'\n[Simulación] LED rojo y buzzer: {"encendidos" if activa else "apagados"}')

    def luz(self, activa):
        print(f'[Simulación] Luz blanca: {"encendida" if activa else "apagada"}')
        return True

    def puerta(self, abierta):
        print(f'[Simulación] Puerta: {"abierta" if abierta else "cerrada"}')
        return True

    def clima(self):
        return 'Temperatura: 24 °C; humedad: 50 % (valores simulados)'

    def medicion(self):
        return {'temperatura_c': 24.0, 'humedad_pct': 50.0}

    def cerrar(self):
        pass


class Raspberry:
    def __init__(self, salidas):
        # Las bibliotecas de hardware solo se cargan en la Raspberry.
        from gpiozero import DigitalInputDevice, LED, OutputDevice, Servo
        import board
        import adafruit_dht
        self.dispositivos = []
        self.bloqueo_dht = threading.Lock()
        self.rojo = self.blanco = self.buzzer = self.servo = None
        try:
            self.pir = DigitalInputDevice(PIR, pull_up=False)
            self.dispositivos.append(self.pir)
            if 'rojo' in salidas:
                self.rojo = LED(ROJO)
                self.dispositivos.append(self.rojo)
            if 'blanco' in salidas:
                self.blanco = LED(BLANCO)
                self.dispositivos.append(self.blanco)
            if 'buzzer' in salidas:
                self.buzzer = OutputDevice(BUZZER, active_high=True, initial_value=False)
                self.dispositivos.append(self.buzzer)
            if 'servo' in salidas:
                self.servo = Servo(SERVO, initial_value=None)
                self.dispositivos.append(self.servo)
            self.dht = adafruit_dht.DHT11(getattr(board, f'D{DHT}'), use_pulseio=False)
        except Exception:
            for dispositivo in reversed(self.dispositivos):
                dispositivo.close()
            raise

    def presencia(self):
        return self.pir.is_active

    def alarma(self, activa):
        for salida in (self.rojo, self.buzzer):
            if salida is not None:
                salida.value = activa

    def luz(self, activa):
        if self.blanco is None:
            print('LED blanco no habilitado; revisa su conexión y usa --salidas blanco.')
            return False
        self.blanco.value = activa
        return True

    def puerta(self, abierta):
        if self.servo is None:
            print('Servo no habilitado; revisa la alimentación y usa --salidas servo.')
            return False
        self.servo.value = 0 if abierta else -1
        return True

    def clima(self):
        try:
            medida = self.medicion()
            return f'Temperatura: {medida["temperatura_c"]} °C; humedad: {medida["humedad_pct"]} %'
        except RuntimeError as error:
            return f'No se pudo leer el DHT11; intenta de nuevo: {error}'

    def medicion(self):
        with self.bloqueo_dht:
            temperatura = self.dht.temperature
            humedad = self.dht.humidity
        if temperatura is None or humedad is None:
            raise RuntimeError('Lectura incompleta')
        return {'temperatura_c': temperatura, 'humedad_pct': humedad}

    def cerrar(self):
        errores = []
        try:
            self.dht.exit()
        except Exception as error:
            errores.append(error)
        for dispositivo in reversed(self.dispositivos):
            try:
                dispositivo.close()
            except Exception as error:
                errores.append(error)
        if errores:
            print(f'Error al liberar dispositivos: {errores}')


def ejecutar(hw, simulacion, azure=None, intervalo=30):
    armado = False
    luz = False
    abierta = None  # No hay sensor que indique la posición real.
    estado_alarma = False
    detener = threading.Event()
    bloqueo = threading.Lock()
    entradas = queue.Queue()
    solicitudes = queue.Queue()

    def leer_entradas():
        while not detener.is_set():
            solicitudes.get()
            if detener.is_set():
                return
            try:
                entradas.put(input('Opción: ').strip())
            except (EOFError, KeyboardInterrupt):
                entradas.put('0')
                return

    def vigilar():
        nonlocal estado_alarma
        while not detener.wait(0.2):
            try:
                with bloqueo:
                    activa = armado and hw.presencia()
                    if activa != estado_alarma:
                        hw.alarma(activa)
                        estado_alarma = activa
                        print('\nALERTA: movimiento detectado.' if activa else '\nAlerta apagada.')
            except Exception as error:
                print(f'\nError al vigilar el PIR: {error}. Se detuvo la vigilancia.')
                detener.set()
                break

    def medir_y_enviar():
        while not detener.is_set():
            datos = {'fecha_utc': datetime.now(timezone.utc).isoformat(), 'simulacion': simulacion}
            try:
                with bloqueo:
                    datos.update({'movimiento': bool(hw.presencia()), 'alarma_armada': armado, 'alarma_activa': estado_alarma})
            except Exception:
                datos.update({'movimiento': None, 'alarma_armada': armado, 'alarma_activa': None})
            try:
                datos.update(hw.medicion())
                datos['dht_estado'] = 'ok'
            except Exception:
                datos.update({'temperatura_c': None, 'humedad_pct': None, 'dht_estado': 'error'})
            azure.publicar(datos)
            if detener.wait(intervalo):
                break

    hilo = threading.Thread(target=vigilar, daemon=True)
    hilo.start()
    recolector = None
    if azure is not None:
        recolector = threading.Thread(target=medir_y_enviar, daemon=True)
        recolector.start()
    lector = threading.Thread(target=leer_entradas, daemon=True)
    lector.start()
    print('Casa domótica: la alarma inicia desarmada. Ctrl+C para salir.')
    try:
        while not detener.is_set():
            print('\n1 Armar/desarmar alarma | 2 Luz | 3 Puerta | 4 Temperatura y humedad | 5 Estado | 0 Salir')
            if simulacion:
                print('6 Simular presencia/sin presencia')
            else:
                print('7 Detectar rostro y abrir puerta (acceso provisional)')
            solicitudes.put(None)
            while not detener.is_set():
                try:
                    opcion = entradas.get(timeout=0.2)
                    break
                except queue.Empty:
                    continue
            else:
                break
            # La lectura del DHT puede tardar; no debe bloquear la vigilancia PIR.
            if opcion == '4':
                print(hw.clima())
                continue
            if opcion == '7' and not simulacion:
                programa = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'rostro.py')
                python_cv = os.path.join(os.path.dirname(programa), '.venv-camara', 'bin', 'python')
                try:
                    resultado = subprocess.run([python_cv, programa], timeout=30)
                    if resultado.returncode == 0:
                        with bloqueo:
                            if hw.puerta(True):
                                abierta = True
                                print('Orden enviada: puerta abierta. Ciérrala con la opción 3.')
                except (OSError, subprocess.TimeoutExpired) as error:
                    print(f'No se pudo realizar la detección: {error}')
                continue
            with bloqueo:
                if opcion == '1':
                    armado = not armado
                    print('Alarma ' + ('armada' if armado else 'desarmada'))
                    if not armado and estado_alarma:
                        hw.alarma(False)
                        estado_alarma = False
                elif opcion == '2':
                    if hw.luz(not luz):
                        luz = not luz
                elif opcion == '3':
                    nueva_posicion = not abierta if abierta is not None else True
                    if hw.puerta(nueva_posicion):
                        abierta = nueva_posicion
                        print('Orden enviada: puerta ' + ('abierta' if abierta else 'cerrada'))
                elif opcion == '5':
                    puerta = 'desconocida' if abierta is None else ('abierta' if abierta else 'cerrada')
                    print(f'Alarma: {"armada" if armado else "desarmada"}; presencia: {"sí" if hw.presencia() else "no"}; luz: {"encendida" if luz else "apagada"}; puerta: {puerta} (última orden).')
                elif opcion == '6' and simulacion:
                    hw.movimiento = not hw.movimiento
                    print('Presencia simulada: ' + ('sí' if hw.movimiento else 'no'))
                elif opcion == '0':
                    break
                else:
                    print('Opción no válida.')
    except (KeyboardInterrupt, EOFError):
        print('\nCerrando programa.')
    finally:
        detener.set()
        hilo.join()
        if recolector is not None:
            recolector.join()
            azure.cerrar()
        for accion in (lambda: hw.alarma(False), hw.cerrar):
            try:
                accion()
            except Exception as error:
                print(f'Error al apagar o liberar un dispositivo: {error}')


def main():
    parser = argparse.ArgumentParser(description='Casa domótica local')
    parser.add_argument('--modo', choices=('simulacion', 'raspberry'), default='simulacion')
    parser.add_argument('--salidas', nargs='*', choices=('rojo', 'blanco', 'buzzer', 'servo'), default=[], help='Habilita únicamente las salidas ya conectadas y verificadas')
    parser.add_argument('--azure', action='store_true', help='Enviar telemetría a Azure IoT Hub')
    parser.add_argument('--intervalo', type=int, default=30, help='Segundos entre envíos (mínimo 5)')
    args = parser.parse_args()
    if args.intervalo < 5:
        parser.error('--intervalo debe ser de al menos 5 segundos')
    try:
        azure = None
        if args.azure:
            from azure_iot import TelemetriaAzure
            azure = TelemetriaAzure()
        hw = Simulacion() if args.modo == 'simulacion' else Raspberry(args.salidas)
        ejecutar(hw, args.modo == 'simulacion', azure, args.intervalo)
    except (ImportError, OSError, RuntimeError, ValueError) as error:
        print(f'No se pudo iniciar el modo {args.modo}: {error}')
        raise SystemExit(1)


if __name__ == '__main__':
    main()

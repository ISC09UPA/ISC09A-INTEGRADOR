#!/usr/bin/env python3
"""Prueba local del PIR y DHT11, sin activar salidas GPIO."""
import argparse
import math
import threading
import time

PIR_GPIO = 16  # pin físico 36
DHT_GPIO = 5   # pin físico 29
INTERVALO_DHT = 2.5


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--sensor', choices=('pir', 'dht', 'ambos'), default='ambos')
    parser.add_argument('--simulacion', action='store_true', help='No abre ningún GPIO')
    args = parser.parse_args()
    detener = threading.Event()
    dispositivos = []
    hilo = None
    pir = dht = None

    try:
        if args.simulacion:
            print('SIMULACIÓN: valores ficticios, sin GPIO.', flush=True)
        else:
            if args.sensor in ('pir', 'ambos'):
                from gpiozero import DigitalInputDevice
                pir = DigitalInputDevice(PIR_GPIO, pull_up=False)
                dispositivos.append(('PIR', pir.close))
            if args.sensor in ('dht', 'ambos'):
                import board
                import adafruit_dht
                dht = adafruit_dht.DHT11(getattr(board, f'D{DHT_GPIO}'), use_pulseio=False)
                dispositivos.append(('DHT11', dht.exit))

        def leer_dht():
            while not detener.is_set():
                try:
                    temperatura, humedad = (24.0, 50.0) if args.simulacion else (dht.temperature, dht.humidity)
                    if temperatura is None or humedad is None:
                        raise ValueError('lectura incompleta')
                    if not (math.isfinite(temperatura) and math.isfinite(humedad) and 0 <= humedad <= 100):
                        raise ValueError('lectura fuera de rango')
                    print(f'DHT11: {temperatura:.1f} °C | {humedad:.1f} % HR', flush=True)
                except Exception as error:
                    print(f'DHT11: lectura fallida ({error}); se intentará de nuevo.', flush=True)
                if detener.wait(INTERVALO_DHT):
                    break

        if args.sensor in ('dht', 'ambos'):
            hilo = threading.Thread(target=leer_dht)
            hilo.start()
        print('Observando sensores. Ctrl+C para salir.', flush=True)
        anterior = None
        while not detener.is_set():
            if args.sensor in ('pir', 'ambos'):
                presente = False if args.simulacion else bool(pir.is_active)
                if presente != anterior:
                    print('PIR: ' + ('movimiento detectado' if presente else 'sin movimiento'), flush=True)
                    anterior = presente
            detener.wait(0.2)
    except KeyboardInterrupt:
        print('\nFinalizando.', flush=True)
    except Exception as error:
        print(f'Error al iniciar o vigilar sensores: {error}', flush=True)
        return 1
    finally:
        detener.set()
        if hilo is not None:
            hilo.join()
        for nombre, cerrar in reversed(dispositivos):
            try:
                cerrar()
            except Exception as error:
                print(f'Error al liberar {nombre}: {error}', flush=True)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())

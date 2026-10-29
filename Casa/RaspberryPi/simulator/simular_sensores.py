#!/usr/bin/env python3
"""Muestra una secuencia finita de lecturas de sensores simulados."""
import argparse
import json
from pathlib import Path
import time

from casa_pi.hardware.simulated import atomic_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--intervalo", type=int, default=15,
                        help="Segundos por lectura (mínimo 11; predeterminado 15)")
    args = parser.parse_args()
    if args.intervalo < 11:
        parser.error("el intervalo debe ser de al menos 11 segundos")
    path = Path('/var/lib/casa-pi/simulator.json')
    original = json.loads(path.read_text())
    sequence = [(15, False), (55, True), (85, False)]
    print('Simulación de luz y movimiento. Ctrl+C para terminar.', flush=True)
    print('El agente está configurado para enviar resúmenes aproximadamente cada 10 segundos.', flush=True)
    try:
        for light, motion in sequence:
            data = dict(original, light_percent=light, motion=motion, fingerprint=None)
            atomic_json(path, data)
            print('Luz: {}% | Movimiento: {}'.format(light, 'sí' if motion else 'no'), flush=True)
            time.sleep(args.intervalo)
    except KeyboardInterrupt:
        print('\nSimulación detenida.', flush=True)
    finally:
        atomic_json(path, original)
        print('Entradas anteriores restauradas.', flush=True)


if __name__ == '__main__':
    main()

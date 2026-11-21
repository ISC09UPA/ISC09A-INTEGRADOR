#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Cruce Inteligente - semaforo controlado directo desde los GPIO de la
Raspberry Pi 4 (version de respaldo / practica de la UA2).

En el sistema completo los LED los controla el ESP32 (Embebidos/ESP32) y la
Raspberry Pi solo corre el backend y la vision (Embebidos/RaspberryPi/vision).
Este script sirve para probar el mismo ciclo SIN ESP32, con los LED
conectados a la Pi segun la tabla de reasignacion de pines del reporte
(GPIO BCM, solo 0-27, evitando I2C GPIO2/3 y UART GPIO14/15).

Uso:  python3 semaforo_rpi.py      (Ctrl+C para salir; libera los GPIO)
"""
import RPi.GPIO as GPIO
import time

# ---- Acceso 1 ----
VERDE1, AMARILLO1, ROJO1 = 17, 27, 22
# ---- Acceso 2 ----
VERDE2, AMARILLO2, ROJO2 = 23, 24, 25
# ---- Acceso 3 ----
VERDE3, AMARILLO3, ROJO3 = 5, 6, 13
# ---- Acceso 4 ----
VERDE4, AMARILLO4, ROJO4 = 19, 26, 21
# ---- Botones peatonales ----
BOTON1, BOTON2 = 4, 12
BOTON3, BOTON4 = 16, 20  # definidos, aun no conectados

PINES_SALIDA = [VERDE1, AMARILLO1, ROJO1, VERDE2, AMARILLO2, ROJO2,
                VERDE3, AMARILLO3, ROJO3, VERDE4, AMARILLO4, ROJO4]
PINES_BOTON = [BOTON1, BOTON2, BOTON3, BOTON4]


def apagar_todos():
    for pin in PINES_SALIDA:
        GPIO.output(pin, GPIO.LOW)


def parpadear(pin, veces, tiempo_s):
    for _ in range(veces):
        GPIO.output(pin, GPIO.LOW);  time.sleep(tiempo_s)
        GPIO.output(pin, GPIO.HIGH); time.sleep(tiempo_s)


def configurar():
    GPIO.setmode(GPIO.BCM)
    GPIO.setwarnings(False)
    for pin in PINES_SALIDA:
        GPIO.setup(pin, GPIO.OUT)
    for pin in PINES_BOTON:
        GPIO.setup(pin, GPIO.IN, pull_up_down=GPIO.PUD_UP)
    apagar_todos()


def ciclo_acceso(verde, amarillo, rojo_propio, boton):
    GPIO.output(rojo_propio, GPIO.LOW)
    GPIO.output(verde, GPIO.HIGH)
    inicio = time.time()
    while time.time() - inicio < 2.5:
        if boton is not None and GPIO.input(boton) == GPIO.LOW:
            print("Peaton presiono boton - acelerando cambio a rojo")
            break
        time.sleep(0.05)
    parpadear(verde, 3, 0.2)
    GPIO.output(verde, GPIO.LOW)
    GPIO.output(amarillo, GPIO.HIGH); time.sleep(1); GPIO.output(amarillo, GPIO.LOW)
    GPIO.output(rojo_propio, GPIO.HIGH); time.sleep(0.5)


def loop():
    while True:
        apagar_todos()
        GPIO.output(ROJO1, GPIO.HIGH); GPIO.output(ROJO2, GPIO.HIGH)
        GPIO.output(ROJO3, GPIO.HIGH); GPIO.output(ROJO4, GPIO.HIGH)
        time.sleep(0.5)
        ciclo_acceso(VERDE1, AMARILLO1, ROJO1, BOTON1)
        ciclo_acceso(VERDE2, AMARILLO2, ROJO2, BOTON2)
        ciclo_acceso(VERDE3, AMARILLO3, ROJO3, None)
        ciclo_acceso(VERDE4, AMARILLO4, ROJO4, None)


if __name__ == "__main__":
    try:
        configurar(); loop()
    except KeyboardInterrupt:
        pass
    finally:
        GPIO.cleanup()

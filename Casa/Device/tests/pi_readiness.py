#!/usr/bin/env python3
"""Chequeo de disponibilidad para el día de la demo en la Raspberry Pi.

Verifica reloj, red, placa de E/S, cámara, permisos y Home. No modifica nada.

Uso:
    python3 Device/tests/pi_readiness.py
    python3 Device/tests/pi_readiness.py --home https://home-api.example.com
"""
from __future__ import annotations

import argparse
import glob
import os
import platform
import shutil
import socket
import subprocess
import sys
import tempfile
import urllib.error
import urllib.request

FAILURES = 0
WARNINGS = 0


def ok(message: str) -> None:
    print(f"PASS  {message}")


def warn(message: str) -> None:
    global WARNINGS
    WARNINGS += 1
    print(f"WARN  {message}")


def fail(message: str) -> None:
    global FAILURES
    FAILURES += 1
    print(f"FAIL  {message}")


def check_platform() -> None:
    machine = platform.machine()
    if machine in ("aarch64", "arm64", "armv7l"):
        ok(f"arquitectura {machine}")
    else:
        warn(f"arquitectura {machine}: no es ARM (sirve como host sustituto, no como Pi)")


def check_clock() -> None:
    try:
        result = subprocess.run(
            ["timedatectl", "show", "-p", "NTPSynchronized", "--value"],
            capture_output=True, text=True, timeout=5, check=False,
        )
        if result.stdout.strip() == "yes":
            ok("reloj sincronizado por NTP")
        else:
            fail("reloj sin NTP: las órdenes vencerán al instante")
    except Exception:  # noqa: BLE001
        warn("no se pudo comprobar NTP")


def check_network() -> None:
    try:
        with socket.create_connection(("1.1.1.1", 443), timeout=5):
            ok("salida a internet")
    except OSError:
        fail("sin salida a internet")


def check_serial() -> None:
    devices = glob.glob("/dev/serial/by-id/usb-*") + glob.glob("/dev/ttyUSB*") + glob.glob("/dev/ttyACM*")
    if devices:
        ok(f"placa de E/S presente: {devices[0]}")
    else:
        fail("placa de E/S no detectada")


def check_groups() -> None:
    groups = set(os.getgroups())
    names = set()
    for group in ("dialout", "video"):
        try:
            import grp

            names.add(grp.getgrnam(group).gr_gid)
        except KeyError:
            continue
    missing = [group for group in names if group not in groups]
    if not missing:
        ok("permisos de dialout/video")
    else:
        warn("faltan grupos dialout/video en la sesión actual (systemd los añade con SupplementaryGroups)")


def check_camera() -> None:
    with tempfile.NamedTemporaryFile(suffix=".jpg", delete=False) as handle:
        path = handle.name
    try:
        for command in (
            ["rpicam-still", "-n", "--width", "1280", "--height", "720", "-o", path],
            ["libcamera-still", "-n", "--width", "1280", "--height", "720", "-o", path],
            ["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-f", "v4l2", "-i", "/dev/video0", "-frames:v", "1", path],
        ):
            if not shutil.which(command[0]):
                continue
            result = subprocess.run(command, capture_output=True, timeout=25, check=False)
            if result.returncode == 0 and os.path.exists(path) and os.path.getsize(path) > 1024:
                ok(f"cámara operativa con {command[0]} ({os.path.getsize(path)} bytes)")
                return
        fail("ninguna cámara capturó una imagen")
    except Exception as error:  # noqa: BLE001
        fail(f"cámara: {error}")
    finally:
        try:
            os.unlink(path)
        except OSError:
            pass


def check_resources() -> None:
    free = shutil.disk_usage("/").free / (1024**3)
    if free >= 1:
        ok(f"espacio libre {free:.1f} GB")
    else:
        warn(f"espacio libre bajo: {free:.1f} GB")
    try:
        pages = os.sysconf("SC_PHYS_PAGES")
        page_size = os.sysconf("SC_PAGE_SIZE")
        total = pages * page_size / (1024**2)
        if total >= 400:
            ok(f"memoria {total:.0f} MB")
        else:
            warn(f"memoria baja: {total:.0f} MB")
    except (ValueError, OSError):
        warn("no se pudo leer la memoria")


def check_home(url: str) -> None:
    last_error: str = ""
    for attempt in range(2):
        try:
            with urllib.request.urlopen(f"{url.rstrip('/')}/ready", timeout=45) as response:
                if 200 <= response.status < 300:
                    ok(f"Home /ready {response.status}")
                    return
                warn(f"Home respondió {response.status}")
                return
        except urllib.error.HTTPError as error:
            if error.code == 503:
                warn("Home /ready 503: App Service gratuito despertando; reintentar en 60 s")
                return
            last_error = f"HTTP {error.code}"
        except Exception as error:  # noqa: BLE001
            last_error = str(error)
        if attempt == 0:
            print("      … reintentando Home (arranque en frío del plan gratuito)")
    fail(f"Home inaccesible: {last_error}")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--home", default="https://home-api.example.com")
    options = parser.parse_args()

    check_platform()
    check_clock()
    check_network()
    check_serial()
    check_groups()
    check_camera()
    check_resources()
    check_home(options.home)

    print()
    if FAILURES:
        print(f"{FAILURES} fallo(s), {WARNINGS} advertencia(s).")
        return 1
    print(f"Listo para la demo ({WARNINGS} advertencia(s)).")
    return 0


if __name__ == "__main__":
    sys.exit(main())

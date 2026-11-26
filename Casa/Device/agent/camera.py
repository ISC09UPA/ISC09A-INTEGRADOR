"""Captura de imagen para CaptureImage.

Backends:
- `v4l2`: webcam USB vía ffmpeg (esta máquina y la Pi).
- `csi`: módulo de cámara de la Raspberry Pi vía rpicam-still / libcamera-still.
- `none`: deshabilitado explícitamente.
"""
from __future__ import annotations

import logging
import os
import shutil
import subprocess
import tempfile

MAX_CAPTURE_BYTES = 5 * 1024 * 1024


class CameraError(RuntimeError):
    pass


class Camera:
    def __init__(self, backend: str, device: str = "/dev/video0", logger: logging.Logger | None = None):
        self._backend = backend
        self._device = device
        self._log = logger or logging.getLogger("camera")

    def capture_jpeg(self) -> bytes:
        if self._backend == "none":
            raise CameraError("La cámara está deshabilitada por configuración.")
        if self._backend == "csi":
            return self._capture_csi()
        if self._backend == "auto":
            try:
                return self._capture_csi()
            except CameraError as error:
                self._log.info("Cámara CSI no disponible (%s); se intenta v4l2.", error)
            return self._capture_v4l2()
        return self._capture_v4l2()

    def _capture_v4l2(self) -> bytes:
        if not shutil.which("ffmpeg"):
            raise CameraError("ffmpeg no está disponible.")
        if not os.path.exists(self._device):
            raise CameraError(f"No existe el dispositivo de cámara {self._device}.")
        with tempfile.NamedTemporaryFile(suffix=".jpg", delete=False) as handle:
            path = handle.name
        try:
            command = [
                "ffmpeg", "-hide_banner", "-loglevel", "error", "-y",
                "-f", "v4l2", "-i", self._device,
                "-frames:v", "1", "-q:v", "4", path,
            ]
            result = subprocess.run(command, capture_output=True, timeout=20, check=False)
            if result.returncode != 0:
                raise CameraError(f"ffmpeg falló: {result.stderr.decode('utf-8', 'replace')[:300]}")
            return self._read(path)
        finally:
            try:
                os.unlink(path)
            except OSError:
                pass

    def _capture_csi(self) -> bytes:
        with tempfile.NamedTemporaryFile(suffix=".jpg", delete=False) as handle:
            path = handle.name
        try:
            attempts = [
                ["rpicam-still", "-n", "--width", "1280", "--height", "720", "-o", path],
                ["libcamera-still", "-n", "--width", "1280", "--height", "720", "-o", path],
            ]
            last_error = ""
            for command in attempts:
                if not shutil.which(command[0]):
                    continue
                result = subprocess.run(command, capture_output=True, timeout=20, check=False)
                if result.returncode == 0 and os.path.exists(path) and os.path.getsize(path) > 0:
                    return self._read(path)
                last_error = result.stderr.decode("utf-8", "replace")[:300]
            raise CameraError(f"No se pudo capturar con la cámara CSI. {last_error}")
        finally:
            try:
                os.unlink(path)
            except OSError:
                pass

    def _read(self, path: str) -> bytes:
        with open(path, "rb") as handle:
            data = handle.read()
        if len(data) < 1024 or not data.startswith(b"\xff\xd8"):
            raise CameraError("La captura no es un JPEG válido.")
        if len(data) > MAX_CAPTURE_BYTES:
            raise CameraError(f"La captura supera los 5 MiB ({len(data)} bytes).")
        return data

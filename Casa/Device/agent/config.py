"""Configuración del agente por variables de entorno.

Todo se puede sobreescribir sin editar código. El archivo `Device/deploy/env.example`
documenta cada variable.
"""
from __future__ import annotations

import os
from dataclasses import dataclass


def _env(name: str, default: str) -> str:
    value = os.environ.get(name)
    return value if value not in (None, "") else default


def _env_int(name: str, default: int) -> int:
    try:
        return int(_env(name, str(default)))
    except ValueError:
        return default


def _env_float(name: str, default: float) -> float:
    try:
        return float(_env(name, str(default)))
    except ValueError:
        return default


@dataclass(frozen=True)
class Config:
    device_id: str = "casa-pi-01"
    light_id: str = "sala"
    door_id: str = "puerta"
    serial_port: str = "/dev/ttyUSB0"
    serial_baud: int = 115200
    db_path: str = "Device/agent/agent-state.db"
    report_interval_s: float = 30.0
    transport: str = "local"
    local_host: str = "127.0.0.1"
    local_port: int = 8765
    local_home_url: str = ""
    camera_backend: str = "v4l2"
    camera_device: str = "/dev/video0"
    iot_connection_string: str = ""
    lighting_mode: str = "manual"
    security_mode: str = "disarmed"
    ldr_enabled: bool = True

    @classmethod
    def from_env(cls) -> "Config":
        return cls(
            device_id=_env("CASA_DEVICE_ID", "casa-pi-01"),
            light_id=_env("CASA_LIGHT_ID", "sala"),
            door_id=_env("CASA_DOOR_ID", "puerta"),
            serial_port=_env("CASA_SERIAL_PORT", "/dev/ttyUSB0"),
            serial_baud=_env_int("CASA_SERIAL_BAUD", 115200),
            db_path=_env("CASA_DB_PATH", "Device/agent/agent-state.db"),
            report_interval_s=_env_float("CASA_REPORT_INTERVAL_S", 30.0),
            transport=_env("CASA_TRANSPORT", "local"),
            local_host=_env("CASA_LOCAL_HOST", "127.0.0.1"),
            local_port=_env_int("CASA_LOCAL_PORT", 8765),
            local_home_url=_env("CASA_LOCAL_HOME_URL", ""),
            camera_backend=_env("CASA_CAMERA_BACKEND", "v4l2"),
            camera_device=_env("CASA_CAMERA_DEVICE", "/dev/video0"),
            iot_connection_string=_env("CASA_IOT_CONNECTION_STRING", ""),
            lighting_mode=_env("CASA_LIGHTING_MODE", "manual"),
            security_mode=_env("CASA_SECURITY_MODE", "disarmed"),
            ldr_enabled=_env("CASA_LDR_ENABLED", "1").lower() in ("1", "true", "yes", "on"),
        )

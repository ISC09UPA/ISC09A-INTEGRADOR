from dataclasses import dataclass
from pathlib import Path
import math

try:
    import tomllib
except ImportError:
    import tomli as tomllib


@dataclass(frozen=True)
class Settings:
    device_id: str = "raspberry-sim"
    mode: str = "simulado"
    transport: str = "local"
    room_id: str = "sala"
    door_id: str = "principal"
    sample_seconds: float = 1.0
    report_seconds: float = 30.0
    light_on_below: float = 30.0
    light_off_above: float = 60.0
    max_open_ms: int = 3000
    max_events: int = 1000
    data_dir: str = "/var/lib/casa-pi"

    def __post_init__(self):
        if self.mode not in ("simulado", "real") or self.transport not in ("local", "azure"):
            raise ValueError("Modo o transporte desconocido")
        for v in (self.device_id, self.room_id, self.door_id, self.data_dir):
            if not isinstance(v, str) or not v.strip():
                raise ValueError("Identificadores y directorio son obligatorios")
        for v in (self.sample_seconds, self.report_seconds, self.light_on_below, self.light_off_above):
            if isinstance(v, bool) or not isinstance(v, (int, float)) or not math.isfinite(v):
                raise ValueError("Valor numerico invalido")
        if not 0.05 <= self.sample_seconds <= 60 or not self.sample_seconds <= self.report_seconds <= 3600:
            raise ValueError("Intervalos fuera de rango")
        if not 0 <= self.light_on_below < self.light_off_above <= 100:
            raise ValueError("Umbrales invalidos; usar porcentaje de luz 0 a 100")
        if type(self.max_open_ms) is not int or not 100 <= self.max_open_ms <= 5000:
            raise ValueError("Duracion maxima invalida")
        if type(self.max_events) is not int or not 10 <= self.max_events <= 10000:
            raise ValueError("Limite de eventos invalido")


def load(path):
    with Path(path).open("rb") as f:
        return Settings(**tomllib.load(f))

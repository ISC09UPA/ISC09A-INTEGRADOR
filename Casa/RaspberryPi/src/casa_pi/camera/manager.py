from pathlib import Path
from threading import Lock


class Camera:
    """La camara real sigue pendiente. Solo acepta imagenes de prueba configuradas."""
    def __init__(self):
        self.lock = Lock()

    def capture(self, fixture):
        with self.lock:
            path = Path(fixture)
            if not path.is_file() or path.stat().st_size > 5 * 1024 * 1024:
                raise ValueError("Imagen de prueba no disponible o demasiado grande")
            return path.read_bytes()

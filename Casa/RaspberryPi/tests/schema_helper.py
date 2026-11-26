"""Offline schema validation helper for agent tests.

Uses jsonschema from the repository's contract validation venv if it is not
available in the current interpreter. Tests remain runnable without the venv,
but schema validation is skipped in that case.
"""
import json
import sys
import subprocess
from pathlib import Path


_REPO_ROOT = Path(__file__).resolve().parents[2]
_VENV_PYTHON = _REPO_ROOT / "contracts" / ".venv" / "bin" / "python"


def _ensure_jsonschema():
    try:
        import jsonschema  # noqa: F401
        return True
    except ImportError:
        pass

    if _VENV_PYTHON.exists():
        try:
            result = subprocess.run(
                [str(_VENV_PYTHON), "-c",
                 "import sysconfig; print(sysconfig.get_path('purelib'))"],
                capture_output=True, text=True, timeout=5,
            )
            if result.returncode == 0:
                site = result.stdout.strip()
                if site and str(site) not in sys.path:
                    sys.path.insert(0, str(site))
        except Exception:
            pass

    try:
        import jsonschema  # noqa: F401
        return True
    except ImportError:
        return False


def load_json(path):
    with open(path, "r", encoding="utf-8") as fh:
        return json.load(fh)


def _build_registry():
    from jsonschema import Draft202012Validator
    from referencing import Registry, Resource

    schemas_dir = _REPO_ROOT / "contracts" / "v1" / "schemas"
    resources = []
    for path in sorted(schemas_dir.glob("*.json")):
        data = load_json(path)
        schema_id = data.get("$id")
        if schema_id:
            resources.append((schema_id, Resource.from_contents(data)))
    return Registry().with_resources(resources)


def validate(data, schema_name):
    if not _ensure_jsonschema():
        raise RuntimeError(
            "jsonschema no esta disponible. Ejecuta los tests con el venv de "
            "contratos (contracts/.venv) o instala jsonschema en el entorno de pruebas."
        )
    from jsonschema import Draft202012Validator, FormatChecker
    registry = _build_registry()
    for sid in registry:
        if sid.endswith(f"/{schema_name}"):
            validator = Draft202012Validator(
                {"$ref": sid},
                registry=registry,
                format_checker=FormatChecker(),
            )
            validator.validate(data)
            return
    raise LookupError(f"schema {schema_name} not found in registry")

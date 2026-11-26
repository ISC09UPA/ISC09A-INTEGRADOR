#!/usr/bin/env bash
# Instalación del agente en la Raspberry Pi (o en esta máquina como sustituto).
#
# Uso:
#   sudo ./Device/deploy/install.sh            # instala y arranca el servicio
#   ./Device/deploy/install.sh --check         # solo verifica requisitos
#
# Requisitos:
#   - El repositorio copiado en /opt/casa-pi.
#   - /etc/casa-pi/agent.env rellenado (permisos 600 root).
set -euo pipefail

APP_DIR="/opt/casa-pi"
ETC_DIR="/etc/casa-pi"
STATE_DIR="/var/lib/casa-pi"
SERVICE="casa-pi.service"
CHECK_ONLY=0

if [[ "${1:-}" == "--check" ]]; then
  CHECK_ONLY=1
fi

problems=0
ok()   { printf 'OK    %s\n' "$*"; }
warn() { printf 'WARN  %s\n' "$*"; }
fail() { printf 'FAIL  %s\n' "$*"; problems=$((problems + 1)); }

if [[ ${CHECK_ONLY} -eq 0 && ${EUID} -ne 0 ]]; then
  echo "Ejecutar con sudo (o usar --check)." >&2
  exit 1
fi

echo "== Requisitos =="
command -v python3 >/dev/null 2>&1 && ok "python3 $(python3 -V 2>&1 | cut -d' ' -f2)" || fail "falta python3"
if python3 -m venv "$(mktemp -d)/probe" >/dev/null 2>&1; then
  ok "módulo venv funcional"
else
  fail "python3-venv no funcional (apt install python3-venv)"
fi

[[ -f "${ETC_DIR}/agent.env" ]] && ok "configuración ${ETC_DIR}/agent.env" || fail "falta ${ETC_DIR}/agent.env (copia Device/deploy/env.example)"
[[ -d "${APP_DIR}/Device/agent" ]] && ok "código en ${APP_DIR}" || fail "falta ${APP_DIR}/Device/agent"
[[ -f "${APP_DIR}/Device/agent/requirements.txt" ]] && ok "requirements.txt" || fail "falta requirements.txt"

if compgen -G "/dev/serial/by-id/usb-*" >/dev/null || compgen -G "/dev/ttyUSB*" >/dev/null; then
  ok "placa de E/S detectada"
else
  warn "placa de E/S no conectada (el agente reintenta cada 5 s)"
fi

if command -v rpicam-still >/dev/null 2>&1 || command -v libcamera-still >/dev/null 2>&1; then
  ok "cámara CSI: herramientas presentes"
elif compgen -G "/dev/video*" >/dev/null; then
  warn "sin herramientas CSI; se usará webcam v4l2"
else
  warn "sin cámara detectada"
fi

if [[ ${CHECK_ONLY} -eq 1 ]]; then
  echo
  if [[ ${problems} -gt 0 ]]; then
    echo "${problems} requisito(s) faltante(s)."
    exit 1
  fi
  echo "Requisitos completos."
  exit 0
fi

echo
echo "== Instalación =="
id -u casa >/dev/null 2>&1 || useradd --system --create-home --shell /usr/sbin/nologin casa
usermod -aG dialout,video casa || true
install -d -m 0755 "${APP_DIR}"
install -d -m 0700 "${ETC_DIR}"
install -d -m 0755 -o casa -g casa "${STATE_DIR}"

if [[ ! -d "${APP_DIR}/venv" ]]; then
  python3 -m venv "${APP_DIR}/venv"
fi
"${APP_DIR}/venv/bin/pip" install --upgrade pip >/dev/null
"${APP_DIR}/venv/bin/pip" install -r "${APP_DIR}/Device/agent/requirements.txt"

install -m 0644 "${APP_DIR}/Device/deploy/casa-pi.service" "/etc/systemd/system/${SERVICE}"
systemctl daemon-reload
systemctl enable "${SERVICE}"
systemctl restart "${SERVICE}"

sleep 2
systemctl --no-pager --lines=15 status "${SERVICE}" || true

echo
echo "Instalado. Registros: journalctl -u ${SERVICE} -f"
echo "El agente reporta DeviceStateReported v2 al arrancar; sin eso Home rechaza las luces."

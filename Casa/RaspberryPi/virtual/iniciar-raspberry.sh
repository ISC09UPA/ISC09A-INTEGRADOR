#!/bin/bash
set -euo pipefail

RPI_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
IMAGE="$RPI_DIR/raspi4.img"
KERNEL="$RPI_DIR/kernel8.img"
DTB="$RPI_DIR/bcm2710-rpi-3-b-plus.dtb"

for file in "$IMAGE" "$KERNEL" "$DTB"; do
  if [[ ! -f "$file" ]]; then
    echo "No encuentro este archivo: $file" >&2
    exit 1
  fi
done

if ! command -v qemu-system-aarch64 >/dev/null 2>&1; then
  echo "No encuentro QEMU. Instálalo con: brew install qemu" >&2
  exit 1
fi

echo "Iniciando Raspberry Pi 3 virtual. SSH: ssh -p 50022 domotica@127.0.0.1"
echo "Para apagarla, ejecuta 'sudo poweroff' dentro de la Raspberry."

exec qemu-system-aarch64 \
  -M raspi3b \
  -m 1G \
  -smp 4 \
  -accel tcg \
  -kernel "$KERNEL" \
  -dtb "$DTB" \
  -drive "file=$IMAGE,if=sd,format=raw" \
  -append 'console=ttyAMA0,115200 root=/dev/mmcblk0p2 rootfstype=ext4 rootwait rw' \
  -nographic \
  -device usb-net,netdev=net0 \
  -netdev user,id=net0,hostfwd=tcp:127.0.0.1:50022-:22

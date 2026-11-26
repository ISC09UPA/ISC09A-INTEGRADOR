# Raspberry virtual

Esta carpeta contiene los archivos para iniciar una Raspberry virtual en la Mac. El programa histórico se llama `casa-pi` y está instalado dentro de la máquina virtual, en `/opt/casa-pi/`. El agente de Raspberry se documenta en [Dispositivo](../../Documentacion/DISPOSITIVO.md).

## Iniciar y entrar

Desde la raíz del proyecto:

```bash
./Casa/RaspberryPi/virtual/iniciar-raspberry.sh
```

En otra terminal, con la máquina encendida:

```bash
ssh -p 50022 domotica@127.0.0.1
```

Dentro de la Raspberry:

```bash
cd /opt/casa-pi
ls
probar-azure
casa-pi-sim status
```

Para apagar de forma ordenada: `sudo poweroff`.

## Archivos principales

| Archivo | Para qué sirve |
|---|---|
| `iniciar-raspberry.sh` | Inicia la máquina virtual con QEMU. |
| `raspi4.img` | Disco de la máquina virtual, con Linux y los datos guardados. |
| `raspi4.img.xz` | Imagen comprimida de referencia. |
| `kernel8.img`, `bcm2710-rpi-3-b-plus.dtb` | Archivos de arranque del script actual. |
| `bcm2711-rpi-4-b.dtb` | Archivo para Pi 4 que el script actual no usa. |
| `ssh`, `userconf.txt` | Aprovisionamiento local; no se versionan. |

Las imágenes de VM son locales y no se suben a Git. No compartas `userconf.txt`.

## Limitaciones

La carpeta se llama `virtual`, pero el script emula una **Pi 3**. No reproduce cámara CSI, sensores, GPIO ni pines de una Pi 4 física. No sirve como aceptación de hardware de Etapa 11.

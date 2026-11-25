
import { telemetriaService } from '@/services';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';
import { useSesion } from '../../store/useSesionStore';
import type { EstadoDispositivo } from '../../types';

function exigirAdmin(): void {
  if (useSesion.getState().usuario?.rol !== 'admin') {
    throw new Error('Esta acción requiere una cuenta de administrador.');
  }
}

export function useSimuladorInvalidador(): void {
  const qc = useQueryClient();

  useEffect(
    () =>
      telemetriaService.subscribirCambios(() => {
        qc.invalidateQueries({ queryKey: ['lecturas'] });
        qc.invalidateQueries({ queryKey: ['dispositivo'] });
        qc.invalidateQueries({ queryKey: ['prediccion'] });
        qc.invalidateQueries({ queryKey: ['plantas'] });
        qc.invalidateQueries({ queryKey: ['planta'] });
      }),
    [qc],
  );
}

export function useDispositivo() {
  return useQuery({
    queryKey: ['dispositivo'],
    queryFn: telemetriaService.estadoDispositivo,
  });
}

export function useRegarAhora() {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: (vars: {
      plantaId: string;
      volumenMl: number;
      operador: string;
    }) => {
      exigirAdmin();

      return telemetriaService.regarAhora(
        vars.plantaId,
        vars.volumenMl,
        vars.operador,
      );
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['eventos'] });
      qc.invalidateQueries({ queryKey: ['lecturas'] });
      qc.invalidateQueries({ queryKey: ['dispositivo'] });
    },
  });
}

type TipoActuador = [
  'bomba',
  'ventilador',
  'leds',
  'valvula',
  'modoAuto',
  'emergencia',
  'online',
];

const ACCIONES: Record<
  TipoActuador[number],
  (valor: boolean) => Promise<EstadoDispositivo>
> = {
  bomba: telemetriaService.alternarBomba,
  ventilador: telemetriaService.alternarVentilador,
  leds: telemetriaService.alternarLeds,
  valvula: telemetriaService.alternarValvula,
  modoAuto: telemetriaService.establecerModoAuto,
  online: telemetriaService.establecerDispositivoEnLinea,
  emergencia: () => telemetriaService.paradaEmergencia(),
};

export function useActuador() {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: ({
      tipo,
      valor,
    }: {
      tipo: TipoActuador[number];
      valor?: boolean;
    }) => {
      exigirAdmin();

      return ACCIONES[tipo](Boolean(valor));
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['dispositivo'] });
      qc.invalidateQueries({ queryKey: ['eventos'] });
    },
  });
}

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';
import type { EstadoDispositivo } from '../../types';
import { telemetriaService, plantasService } from '@/services';

export function useSimuladorInvalidador(): void {
  const qc = useQueryClient();
  useEffect(() => telemetriaService.subscribirCambios(() => {
    qc.invalidateQueries({ queryKey: ['lecturas'] });
    qc.invalidateQueries({ queryKey: ['dispositivo'] });
    qc.invalidateQueries({ queryKey: ['prediccion'] });
    qc.invalidateQueries({ queryKey: ['plantas'] });
    qc.invalidateQueries({ queryKey: ['planta'] });
  }), [qc]);
}

export function useLecturas() {
  return useQuery({
    queryKey: ['lecturas'],
    queryFn: telemetriaService.lecturaActual,
  });
}

export function useDispositivo() {
  return useQuery({
    queryKey: ['dispositivo'],
    queryFn: telemetriaService.estadoDispositivo,
  });
}

export function usePlantas() {
  return useQuery({
    queryKey: ['plantas'],
    queryFn: plantasService.listarPlantas,
  });
}

export function usePlanta(id: string) {
  return useQuery({
    queryKey: ['planta', id],
    queryFn: () => plantasService.obtenerPlanta(id),
  });
}

export function useHistorialPlanta(id: string) {
  return useQuery({
    queryKey: ['historial', id],
    queryFn: () => telemetriaService.historialPorPlanta(id),
  });
}

export function usePrediccion(plantaId: string) {
  return useQuery({
    queryKey: ['prediccion', plantaId],
    queryFn: () => telemetriaService.prediccion(plantaId),
  });
}

export function useRegarAhora() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (vars: { plantaId: string; volumenMl: number; operador: string }) =>
      telemetriaService.regarAhora(vars.plantaId, vars.volumenMl, vars.operador),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['eventos'] });
      qc.invalidateQueries({ queryKey: ['lecturas'] });
      qc.invalidateQueries({ queryKey: ['dispositivo'] });
    },
  });
}

type TipoActuador = ['bomba', 'ventilador', 'leds', 'valvula', 'modoAuto', 'emergencia', 'online'];

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
    mutationFn: ({ tipo, valor }: { tipo: TipoActuador[number]; valor?: boolean }) =>
      ACCIONES[tipo](Boolean(valor)),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['dispositivo'] });
      qc.invalidateQueries({ queryKey: ['eventos'] });
    },
  });
}

import { simulador } from '@/mocks/simulador';
import { Planta } from '../types';
import { espera, leer } from './bd';

export async function listarPlantas(): Promise<Planta[]> {
  await espera(280);
  const bd = await leer();
  const humedades = simulador.humedades();
  return bd.plantas.map((p) => ({
    ...p,
    diagnostico: {
      ...p.diagnostico,
      humedadSueloPct: humedades[p.id] ?? p.diagnostico.humedadSueloPct,
    },
  }));
}

export async function obtenerPlanta(id: string): Promise<Planta> {
  await espera(220);
  const bd = await leer();
  const planta = bd.plantas.find((p) => p.id === id);
  if (!planta) throw new Error('Planta no encontrada');
  const humedades = simulador.humedades();
  return {
    ...planta,
    diagnostico: {
      ...planta.diagnostico,
      humedadSueloPct: humedades[id] ?? planta.diagnostico.humedadSueloPct,
    },
  };
}
import { espera, escribir, leer } from './bd';
import { nuevoIdRegla } from './ids';
import type { Regla, Umbrales } from '../types';

// Reglas SI/ENTONCES ejecutadas localmente en la RPi (mock).

export async function listarReglas(): Promise<Regla[]> {
  await espera(280);
  const bd = await leer();
  const orden = { alta: 0, media: 1, baja: 2 } as const;
  return [...bd.reglas].sort((a, b) => orden[a.prioridad] - orden[b.prioridad]);
}

export async function guardarRegla(
  regla: Omit<Regla, 'id'> & { id?: string },
): Promise<Regla> {
  await espera(450);
  const id = regla.id ?? nuevoIdRegla();
  const final: Regla = { ...regla, id };
  await escribir((bd) => {
    const indice = bd.reglas.findIndex((r) => r.id === id);
    if (indice >= 0) bd.reglas[indice] = final;
    else bd.reglas.push(final);
  });
  return final;
}

export async function alternarRegla(id: string, activa: boolean): Promise<void> {
  await espera(250);
  await escribir((bd) => {
    const regla = bd.reglas.find((r) => r.id === id);
    if (regla && !regla.bloqueadaPorSeguridad) regla.activa = activa;
  });
}

export async function probarRegla(id: string): Promise<string> {
  await espera(600);
  const bd = await leer();
  const regla = bd.reglas.find((r) => r.id === id);
  if (!regla) throw new Error('Regla no encontrada');
  return `Pulso de prueba enviado a la RPi para "${regla.nombre}" (GPIO simulado, 3 s).`;
}

export async function eliminarRegla(id: string): Promise<void> {
  await espera(300);
  await escribir((bd) => {
    bd.reglas = bd.reglas.filter((r) => r.id !== id);
  });
}

export async function obtenerUmbrales(): Promise<Umbrales> {
  await espera(200);
  const bd = await leer();
  return { ...bd.umbrales };
}

export async function guardarUmbrales(umbrales: Umbrales): Promise<void> {
  await espera(300);
  await escribir((bd) => {
    bd.umbrales = { ...umbrales };
  });
}

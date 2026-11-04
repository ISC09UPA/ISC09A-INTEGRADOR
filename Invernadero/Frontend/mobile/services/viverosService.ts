import { espera, leer } from './bd';
import { UBICACION_USUARIO_MOCK } from '@/mocks/datos';
import type { Vivero } from '../types';

// Viveros cercanos (mock) y ubicación del usuario. El componente de mapa
// está aislado en src/components/mapa/ para cambiar de proveedor después.

export interface PuntoMapa {
  latitud: number;
  longitud: number;
}

export async function listarViverosCercanos(): Promise<{ usuario: PuntoMapa; viveros: Vivero[] }> {
  await espera(300);
  const bd = await leer();
  return {
    usuario: { ...UBICACION_USUARIO_MOCK },
    viveros: [...bd.viveros].sort((a, b) => a.distanciaKm - b.distanciaKm),
  };
}

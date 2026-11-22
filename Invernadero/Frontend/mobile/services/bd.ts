
import AsyncStorage from '@react-native-async-storage/async-storage';

import {
  ESCANEOS_SEMILLA,
  PLANTAS_SEMILLA,
  REGLAS_SEMILLA,
  UMBRALES_SEMILLA,
  USUARIOS_SEMILLA,
  VIVEROS_SEMILLA,
} from '@/mocks/datos';

import type {
  EscaneoPlanta,
  Planta,
  Regla,
  Umbrales,
  Usuario,
  Vivero,
} from '../types';

// Nueva versión para cargar las semillas y estructura actualizadas.
const CLAVE = 'riegosmart.bd.v2';

interface EstadoBd {
  plantas: Planta[];
  reglas: Regla[];
  umbrales: Umbrales;
  usuarios: Usuario[];
  escaneos: EscaneoPlanta[];
  viveros: Vivero[];
}

let estado: EstadoBd | null = null;
let cargando: Promise<EstadoBd> | null = null;

// Cola para ejecutar las escrituras en orden.
let colaPersistencia: Promise<void> = Promise.resolve();

function estadoSemilla(): EstadoBd {
  return {
    plantas: structuredClone(PLANTAS_SEMILLA) as Planta[],
    reglas: structuredClone(REGLAS_SEMILLA) as Regla[],
    umbrales: { ...UMBRALES_SEMILLA },
    usuarios: [...USUARIOS_SEMILLA],
    escaneos: structuredClone(ESCANEOS_SEMILLA) as EscaneoPlanta[],
    viveros: [...VIVEROS_SEMILLA],
  };
}

async function cargar(): Promise<EstadoBd> {
  if (estado) return estado;

  cargando ??= (async () => {
    const base = estadoSemilla();

    try {
      const bruto = await AsyncStorage.getItem(CLAVE);

      estado = bruto
        ? ({ ...base, ...JSON.parse(bruto) } as EstadoBd)
        : base;
    } catch (error) {
      if (__DEV__) {
        console.error('Error al cargar la base de datos local:', error);
      }

      estado = base;
    }

    return estado;
  })();

  return cargando;
}

// Guardar el estado completo en AsyncStorage.
async function persistir(): Promise<void> {
  if (!estado) return;

  const copia = JSON.stringify(estado);

  const escritura = colaPersistencia
    .catch(() => undefined)
    .then(() => AsyncStorage.setItem(CLAVE, copia));

  colaPersistencia = escritura;

  await escritura;
}

export function espera(ms: number = 350): Promise<void> {
  return new Promise((res) =>
    setTimeout(res, ms + Math.random() * 150),
  );
}

export async function leer(): Promise<EstadoBd> {
  await cargar();
  return estado as EstadoBd;
}

export async function escribir(
  mutacion: (bd: EstadoBd) => void,
): Promise<void> {
  const bd = await leer();

  mutacion(bd);

  // No terminar hasta que AsyncStorage confirme la escritura.
  await persistir();
}

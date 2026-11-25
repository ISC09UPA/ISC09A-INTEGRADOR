
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
  /** Contraseñas de cuentas registradas (usuarioId -> contraseña). Mock local. */
  claves: Record<string, string>;
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
    claves: {},
  };
}

// Validación de los datos recuperados del almacenamiento.
const ROLES: string[] = ['admin', 'operador'];

function esObjeto(valor: unknown): valor is Record<string, unknown> {
  return typeof valor === 'object' && valor !== null && !Array.isArray(valor);
}

function conId(valor: unknown): boolean {
  return esObjeto(valor) && typeof valor.id === 'string';
}

function usuarioValido(valor: unknown): valor is Usuario {
  return (
    esObjeto(valor) &&
    typeof valor.id === 'string' &&
    typeof valor.nombre === 'string' &&
    typeof valor.email === 'string' &&
    ROLES.includes(valor.rol as string)
  );
}

/** Mezcla lo guardado con las semillas, descartando datos con formato incorrecto. */
function fusionarGuardado(base: EstadoBd, bruto: string): EstadoBd {
  let guardado: unknown;

  try {
    guardado = JSON.parse(bruto);
  } catch {
    return base;
  }

  if (!esObjeto(guardado)) return base;

  const resultado: EstadoBd = { ...base };

  if (Array.isArray(guardado.plantas)) {
    resultado.plantas = guardado.plantas.filter(conId) as Planta[];
  }

  if (Array.isArray(guardado.reglas)) {
    resultado.reglas = guardado.reglas.filter(conId) as Regla[];
  }

  if (Array.isArray(guardado.escaneos)) {
    resultado.escaneos = guardado.escaneos.filter(conId) as EscaneoPlanta[];
  }

  if (Array.isArray(guardado.viveros)) {
    resultado.viveros = guardado.viveros.filter(conId) as Vivero[];
  }

  if (Array.isArray(guardado.usuarios)) {
    const validos = guardado.usuarios.filter(usuarioValido);

    if (validos.length > 0) {
      resultado.usuarios = validos;
    }
  }

  if (esObjeto(guardado.umbrales)) {
    const numericos = Object.entries(guardado.umbrales).filter(
      (par): par is [string, number] => typeof par[1] === 'number',
    );

    resultado.umbrales = {
      ...base.umbrales,
      ...Object.fromEntries(numericos),
    };
  }

  if (esObjeto(guardado.claves)) {
    const textos = Object.entries(guardado.claves).filter(
      (par): par is [string, string] => typeof par[1] === 'string',
    );

    resultado.claves = Object.fromEntries(textos);
  }

  return resultado;
}

async function cargar(): Promise<EstadoBd> {
  if (estado) return estado;

  cargando ??= (async () => {
    const base = estadoSemilla();

    try {
      const bruto = await AsyncStorage.getItem(CLAVE);

      estado = bruto ? fusionarGuardado(base, bruto) : base;
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

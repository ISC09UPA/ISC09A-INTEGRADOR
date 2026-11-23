
import type {
  ActuadorRegla,
  CategoriaRegla,
  PrioridadRegla,
  Regla,
  SensorRegla,
} from '../../../types';

export type Categoria = CategoriaRegla;
export type Prioridad = PrioridadRegla;

// Sensores y actuadores que actualmente permite configurar el formulario.
export type Sensor = Exclude<SensorRegla, 'horario'>;
export type Operador = '<' | '>' | '>=' | '<=';
export type Actuador = Extract<ActuadorRegla, 'bomba' | 'ventilador'>;
export type Accion = 'encender' | 'apagar';

export interface Condicion {
  sensor: Sensor;
  operador: Operador;
  umbral: number;
}

export interface AccionRegla {
  actuador: Actuador;
  accion: Accion | 'regar';
  dosisMl?: number;
}

export interface DatosRegla {
  nombre: string;
  prioridad: Prioridad;
  condicion: Condicion;
  accion: AccionRegla;
}

export const SENSORES: {
  id: Sensor;
  etiqueta: string;
  unidad: string;
}[] = [
  { id: 'humedadSuelo', etiqueta: 'Humedad sustrato', unidad: '%' },
  { id: 'temperatura', etiqueta: 'Temperatura', unidad: '°C' },
  { id: 'luz', etiqueta: 'Luz', unidad: ' lx' },
  { id: 'nivelDeposito', etiqueta: 'Nivel tanque', unidad: '%' },
];

export const OPERADORES: {
  id: Operador;
  etiqueta: string;
  simbolo: string;
}[] = [
  { id: '<', etiqueta: 'Menor que <', simbolo: '<' },
  { id: '>', etiqueta: 'Mayor que >', simbolo: '>' },
  { id: '>=', etiqueta: 'Mayor o igual ≥', simbolo: '≥' },
  { id: '<=', etiqueta: 'Menor o igual ≤', simbolo: '≤' },
];

export const ACTUADORES: {
  id: Actuador;
  etiqueta: string;
}[] = [
  { id: 'bomba', etiqueta: 'Bomba GPIO 17' },
  { id: 'ventilador', etiqueta: 'Ventilador GPIO 27' },
];

export const PRIORIDADES: {
  id: Prioridad;
  etiqueta: string;
}[] = [
  { id: 'alta', etiqueta: 'Alta' },
  { id: 'media', etiqueta: 'Media' },
  { id: 'baja', etiqueta: 'Baja' },
];

export const FILTROS: {
  id: Categoria | 'todas';
  etiqueta: string;
}[] = [
  { id: 'todas', etiqueta: 'Todas' },
  { id: 'riego', etiqueta: 'Riego' },
  { id: 'clima', etiqueta: 'Clima' },
  { id: 'iluminacion', etiqueta: 'Iluminación' },
  { id: 'seguridad', etiqueta: 'Seguridad' },
];

function categoriaDe(sensor: Sensor): Categoria {
  if (sensor === 'temperatura') return 'clima';
  if (sensor === 'luz') return 'iluminacion';
  return 'riego';
}

/**
 * Convierte los datos del formulario al modelo central Regla.
 * Si se edita una regla, conserva sus propiedades originales.
 */
export function construirRegla(
  datos: DatosRegla,
  base?: Regla,
): Omit<Regla, 'id'> & { id?: string } {
  const esBomba = datos.accion.actuador === 'bomba';

  return {
    ...base,
    ...(base ? { id: base.id } : {}),
    nombre: datos.nombre,
    categoria: categoriaDe(datos.condicion.sensor),
    prioridad: datos.prioridad,
    activa: base?.activa ?? true,
    si: {
      sensor: datos.condicion.sensor,
      operador: datos.condicion.operador,
      umbral: String(datos.condicion.umbral),
      duranteMin: base?.si.duranteMin,
    },
    entonces: esBomba
      ? {
          actuador: 'bomba',
          accion: 'regar_volumen',
          volumenMl: datos.accion.dosisMl ?? 180,
          hastaValor: base?.entonces.hastaValor,
        }
      : {
          actuador: 'ventilador',
          accion:
            datos.accion.accion === 'apagar' ? 'apagar' : 'encender',
        },
  };
}

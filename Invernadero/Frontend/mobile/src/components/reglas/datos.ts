// Datos de ejemplo (en el front salen de reglasService).
export type Categoria = 'riego' | 'clima' | 'iluminacion' | 'seguridad';
export type Prioridad = 'alta' | 'media' | 'baja';

export type Sensor = 'humedadSuelo' | 'temperatura' | 'luz' | 'nivelDeposito';
export type Operador = '<' | '>=' | '<=';
export type Actuador = 'bomba' | 'ventilador';
export type Accion = 'regar' | 'encender' | 'apagar';

/** Condición SI editable desde el formulario. */
export interface Condicion {
  sensor: Sensor;
  operador: Operador;
  umbral: number;
}

/** Acción ENTONCES editable desde el formulario. */
export interface AccionRegla {
  actuador: Actuador;
  accion: Accion;
  dosisMl?: number;
}

export interface Regla {
  id: string;
  nombre: string;
  categoria: Categoria;
  prioridad: Prioridad;
  activa: boolean;
  /** Texto ya armado de la condición SI. */
  si: string;
  /** Texto ya armado de la acción ENTONCES. */
  entonces: string;
  ultimoDisparo?: string;
  /** Las de seguridad no se pueden pausar, editar ni eliminar. */
  bloqueada?: boolean;
  /** Si existe, la regla se puede editar con el formulario. */
  condicion?: Condicion;
  accion?: AccionRegla;
}

export const SENSORES: { id: Sensor; etiqueta: string; unidad: string }[] = [
  { id: 'humedadSuelo', etiqueta: 'Humedad sustrato', unidad: '%' },
  { id: 'temperatura', etiqueta: 'Temperatura', unidad: '°C' },
  { id: 'luz', etiqueta: 'Luz', unidad: ' lx' },
  { id: 'nivelDeposito', etiqueta: 'Nivel tanque', unidad: '%' },
];

export const OPERADORES: { id: Operador; etiqueta: string; simbolo: string }[] = [
  { id: '<', etiqueta: 'Menor que <', simbolo: '<' },
  { id: '>=', etiqueta: 'Mayor o igual ≥', simbolo: '≥' },
  { id: '<=', etiqueta: 'Menor o igual ≤', simbolo: '≤' },
];

export const ACTUADORES: { id: Actuador; etiqueta: string }[] = [
  { id: 'bomba', etiqueta: 'Bomba GPIO 17' },
  { id: 'ventilador', etiqueta: 'Ventilador GPIO 27' },
];

export const PRIORIDADES: { id: Prioridad; etiqueta: string }[] = [
  { id: 'alta', etiqueta: 'Alta' },
  { id: 'media', etiqueta: 'Media' },
  { id: 'baja', etiqueta: 'Baja' },
];

/** Datos que captura el formulario. */
export interface DatosRegla {
  nombre: string;
  prioridad: Prioridad;
  condicion: Condicion;
  accion: AccionRegla;
}

function categoriaDe(sensor: Sensor): Categoria {
  if (sensor === 'temperatura') return 'clima';
  if (sensor === 'luz') return 'iluminacion';
  return 'riego';
}

/** Arma los textos SI / ENTONCES a partir de la condición y la acción. */
function armarTextos(c: Condicion, a: AccionRegla) {
  const sensor = SENSORES.find((s) => s.id === c.sensor)!;
  const simbolo = OPERADORES.find((o) => o.id === c.operador)!.simbolo;
  const actuador = ACTUADORES.find((x) => x.id === a.actuador)!.etiqueta;
  const entonces =
    a.accion === 'regar'
      ? `Activar ${actuador} (${a.dosisMl ?? 180} mL)`
      : a.accion === 'encender'
        ? `Encender ${actuador}`
        : `Apagar ${actuador}`;
  return { si: `${sensor.etiqueta} ${simbolo} ${c.umbral}${sensor.unidad}`, entonces };
}

/** Crea una regla nueva o actualiza `base` con lo capturado en el formulario. */
export function construirRegla(datos: DatosRegla, base?: Regla): Regla {
  return {
    ...base,
    id: base?.id ?? `r-${Date.now()}`,
    activa: base?.activa ?? true,
    nombre: datos.nombre,
    prioridad: datos.prioridad,
    categoria: categoriaDe(datos.condicion.sensor),
    condicion: datos.condicion,
    accion: datos.accion,
    ...armarTextos(datos.condicion, datos.accion),
  };
}

export const FILTROS: { id: Categoria | 'todas'; etiqueta: string }[] = [
  { id: 'todas', etiqueta: 'Todas' },
  { id: 'riego', etiqueta: 'Riego' },
  { id: 'clima', etiqueta: 'Clima' },
  { id: 'iluminacion', etiqueta: 'Iluminación' },
  { id: 'seguridad', etiqueta: 'Seguridad' },
];

export const REGLAS: Regla[] = [
  {
    id: 'r-riego-sequedad',
    nombre: 'Riego automático por sequedad',
    categoria: 'riego',
    prioridad: 'alta',
    activa: true,
    si: 'Humedad sustrato < 35% durante 10 min',
    entonces: 'Activar Bomba GPIO 17 (180 mL hasta 60%)',
    ultimoDisparo: 'Hace 6 h',
    condicion: { sensor: 'humedadSuelo', operador: '<', umbral: 35 },
    accion: { actuador: 'bomba', accion: 'regar', dosisMl: 180 },
  },
  {
    id: 'r-bloqueo-deposito',
    nombre: 'Seguridad antibloqueo por depósito bajo',
    categoria: 'seguridad',
    prioridad: 'alta',
    activa: true,
    si: 'Nivel tanque < 10%',
    entonces: 'Bloquear Bomba GPIO 17 y alertar al móvil',
    bloqueada: true,
  },
  {
    id: 'r-max-bomba',
    nombre: 'Tiempo máximo de bomba',
    categoria: 'seguridad',
    prioridad: 'alta',
    activa: true,
    si: 'Humedad sustrato < 99%',
    entonces: 'Bloquear Bomba GPIO 17 y alertar al móvil',
    bloqueada: true,
  },
  {
    id: 'r-ventilador-calor',
    nombre: 'Control térmico de mediodía',
    categoria: 'clima',
    prioridad: 'media',
    activa: true,
    si: 'Temperatura ≥ 30°C',
    entonces: 'Encender Ventilador GPIO 27',
    ultimoDisparo: 'Hace 2 h',
    condicion: { sensor: 'temperatura', operador: '>=', umbral: 30 },
    accion: { actuador: 'ventilador', accion: 'encender' },
  },
  {
    id: 'r-ventilador-frio',
    nombre: 'Apagar ventilador al enfriarse',
    categoria: 'clima',
    prioridad: 'baja',
    activa: true,
    si: 'Temperatura ≤ 28°C',
    entonces: 'Apagar Ventilador GPIO 27',
    condicion: { sensor: 'temperatura', operador: '<=', umbral: 28 },
    accion: { actuador: 'ventilador', accion: 'apagar' },
  },
  {
    id: 'r-luces-fotoperiodo',
    nombre: 'Ciclo diurno de crecimiento',
    categoria: 'iluminacion',
    prioridad: 'baja',
    activa: true,
    si: 'Horario entre 07:00-21:00',
    entonces: 'Encender Luces LED GPIO 22',
  },
];

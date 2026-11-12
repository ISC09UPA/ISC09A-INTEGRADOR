// Datos de ejemplo (en el front salen de reglasService).
export type Categoria = 'riego' | 'clima' | 'iluminacion' | 'seguridad';
export type Prioridad = 'alta' | 'media' | 'baja';

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
  /** Las de seguridad no se pueden pausar. */
  bloqueada?: boolean;
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
  },
  {
    id: 'r-ventilador-frio',
    nombre: 'Apagar ventilador al enfriarse',
    categoria: 'clima',
    prioridad: 'baja',
    activa: true,
    si: 'Temperatura ≤ 28°C',
    entonces: 'Apagar Ventilador GPIO 27',
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

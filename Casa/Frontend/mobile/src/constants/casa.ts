import type { IconName } from '@/constants/casa-theme';

export interface Sensor {
  icon: IconName;
  valor: string;
  etiqueta: string;
}

export interface Evento {
  tipo: string;
  lugar: string;
  hora: string;
}

export interface Casa {
  nombre: string;
  actualizado: string;
  /** Luz ambiente en porcentaje (0–100). */
  ambiente: number;
  ultimoEvento: Evento;
  sensores: readonly Sensor[];
}

/** Estados que el usuario puede alternar desde la app. */
export interface Controles {
  luz: boolean;
  armada: boolean;
  noche: boolean;
}

export type Control = keyof Controles;

// Datos de ejemplo hasta conectar con la Raspberry
export const casa: Casa = {
  nombre: 'Casa de prueba',
  actualizado: '18:42',
  ambiente: 38,
  ultimoEvento: { tipo: 'Movimiento', lugar: 'Sala', hora: '18:42' },
  sensores: [
    { icon: 'thermometer', valor: '22°', etiqueta: 'Temperatura' },
    { icon: 'droplet', valor: '48%', etiqueta: 'Humedad' },
    { icon: 'lock', valor: 'Cerrada', etiqueta: 'Puerta' },
  ],
};

export const controlesIniciales: Controles = { luz: true, armada: true, noche: false };

/** Alterna un control. Activar el modo noche apaga la luz y arma la alarma. */
export const alternar = (c: Controles, key: Control): Controles =>
  key === 'noche' && !c.noche ? { luz: false, armada: true, noche: true } : { ...c, [key]: !c[key] };

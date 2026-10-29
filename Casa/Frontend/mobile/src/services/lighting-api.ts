// Servicio mock de iluminación. Reemplazar estas funciones por llamadas a la API real
// manteniendo la misma forma de los datos; la UI y el hook no necesitan cambios.

export type LightingMode = 'auto' | 'manual';

export type LightingStatus = {
  mode: LightingMode;
  lightOn: boolean;
  /** Porcentaje de luz ambiental (0–100), igual que el sensor de la Raspberry. */
  ambientLevel: number;
  lastUpdate: Date;
};

export type LightCommandResult = {
  lightOn: boolean;
  confirmedAt: Date;
};

const CONFIRMATION_DELAY_MS = 1500;

function mockReportTime() {
  const date = new Date();
  date.setHours(18, 42, 0, 0);
  return date;
}

export async function fetchLightingStatus(): Promise<LightingStatus> {
  return {
    mode: 'auto',
    lightOn: true,
    ambientLevel: 38,
    lastUpdate: mockReportTime(),
  };
}

/** Envía la orden a la casa y resuelve cuando la casa la confirma. */
export function sendLightCommand(turnOn: boolean): Promise<LightCommandResult> {
  return new Promise((resolve) => {
    setTimeout(() => resolve({ lightOn: turnOn, confirmedAt: new Date() }), CONFIRMATION_DELAY_MS);
  });
}

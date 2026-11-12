
import AsyncStorage from '@react-native-async-storage/async-storage';

const CLAVE_CONFIGURACION = '@riegoSmart/configuracion';

export interface ConfiguracionApp {
  formatoFecha: 'dd/MM/yyyy' | 'MM/dd/yyyy' | 'yyyy-MM-dd';
  formatoHora: '12' | '24';
  notificaciones: boolean;
  sonido: boolean;
  vibracion: boolean;
  avisosInternos: boolean;
}

export const CONFIGURACION_INICIAL: ConfiguracionApp = {
  formatoFecha: 'dd/MM/yyyy',
  formatoHora: '24',
  notificaciones: true,
  sonido: true,
  vibracion: true,
  avisosInternos: true,
};

export async function obtenerConfiguracion(): Promise<ConfiguracionApp> {
  try {
    const datos = await AsyncStorage.getItem(CLAVE_CONFIGURACION);

    if (!datos) return CONFIGURACION_INICIAL;

    return {
      ...CONFIGURACION_INICIAL,
      ...JSON.parse(datos),
    };
  } catch {
    return CONFIGURACION_INICIAL;
  }
}

export async function guardarConfiguracion(
  configuracion: ConfiguracionApp,
): Promise<void> {
  await AsyncStorage.setItem(
    CLAVE_CONFIGURACION,
    JSON.stringify(configuracion),
  );
}


import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useRef,
  useState,
  type ReactNode,
} from 'react';

import {
  CONFIGURACION_INICIAL,
  guardarConfiguracion,
  obtenerConfiguracion,
  type ConfiguracionApp,
} from '../../services/configuracionService';

import { fijarHaptico } from '../../utils/haptico';

interface ConfiguracionContextType {
  config: ConfiguracionApp;
  cargada: boolean;
  actualizar: <K extends keyof ConfiguracionApp>(
    propiedad: K,
    valor: ConfiguracionApp[K],
  ) => Promise<boolean>;
}

const ConfiguracionContext =
  createContext<ConfiguracionContextType | null>(null);

export function ConfiguracionProvider({
  children,
}: {
  children: ReactNode;
}) {
  const [config, setConfig] =
    useState<ConfiguracionApp>(CONFIGURACION_INICIAL);

  const configRef = useRef(config);

  const [cargada, setCargada] = useState(false);

  useEffect(() => {
    let activo = true;

    async function cargar() {
      try {
        const preferencias = await obtenerConfiguracion();

        if (!activo) return;

        configRef.current = preferencias;
        setConfig(preferencias);
        fijarHaptico(preferencias.vibracion);
      } catch (error) {
        if (__DEV__) {
          console.error(
            'Error al cargar la configuración:',
            error,
          );
        }

        // Se conservan los valores iniciales.
      } finally {
        if (activo) setCargada(true);
      }
    }

    void cargar();

    return () => {
      activo = false;
    };
  }, []);

  const actualizar = useCallback(
    async <K extends keyof ConfiguracionApp,>(
      propiedad: K,
      valor: ConfiguracionApp[K],
    ): Promise<boolean> => {
      const anterior = configRef.current;

      const nuevaConfiguracion = {
        ...anterior,
        [propiedad]: valor,
      };

      setConfig(nuevaConfiguracion);
      configRef.current = nuevaConfiguracion;

      if (propiedad === 'vibracion') {
        fijarHaptico(nuevaConfiguracion.vibracion);
      }

      try {
        await guardarConfiguracion(nuevaConfiguracion);
        return true;
      } catch {
        setConfig(anterior);
        configRef.current = anterior;

        if (propiedad === 'vibracion') {
          fijarHaptico(anterior.vibracion);
        }

        return false;
      }
    },
    [],
  );

  return (
    <ConfiguracionContext.Provider
      value={{ config, cargada, actualizar }}
    >
      {children}
    </ConfiguracionContext.Provider>
  );
}

export function useConfiguracion() {
  const contexto = useContext(ConfiguracionContext);

  if (!contexto) {
    throw new Error(
      'useConfiguracion debe utilizarse dentro de ConfiguracionProvider.',
    );
  }

  return contexto;
}

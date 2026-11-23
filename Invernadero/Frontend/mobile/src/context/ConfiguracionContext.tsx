
import {
    createContext,
    useCallback,
    useContext,
    useEffect,
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

  const [cargada, setCargada] = useState(false);

  useEffect(() => {
    let activo = true;

    async function cargar() {
      const preferencias = await obtenerConfiguracion();

      if (!activo) return;

      setConfig(preferencias);
      fijarHaptico(preferencias.vibracion);
      setCargada(true);
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
      const anterior = config;

      const nuevaConfiguracion = {
        ...anterior,
        [propiedad]: valor,
      };

      setConfig(nuevaConfiguracion);

      if (propiedad === 'vibracion') {
        fijarHaptico(nuevaConfiguracion.vibracion);
      }

      try {
        await guardarConfiguracion(nuevaConfiguracion);
        return true;
      } catch {
        setConfig(anterior);

        if (propiedad === 'vibracion') {
          fijarHaptico(anterior.vibracion);
        }

        return false;
      }
    },
    [config],
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

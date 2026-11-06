import { useCallback, useEffect, useState } from 'react';
import { listar } from '@/api/lockersService';
import { mensajeError } from '@/api/errores';
import type { Locker } from '@/types';

export interface DatosLockers {
  lockers: Locker[];
  cargando: boolean;
  error: string;
}

export function useLockers() {
  const [lockers, setLockers] = useState<Locker[]>([]);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState('');

  const recargar = useCallback(async () => {
    try {
      const datos = await listar();
      setLockers(datos);
      setError('');
    } catch (e) {
      setError(mensajeError(e));
    } finally {
      setCargando(false);
    }
  }, []);

  useEffect(() => {
    let activo = true;

    (async () => {
      try {
        const datos = await listar();
        if (!activo) return;
        setLockers(datos);
        setError('');
      } catch (e) {
        if (activo) setError(mensajeError(e));
      } finally {
        if (activo) setCargando(false);
      }
    })();

    return () => {
      activo = false;
    };
  }, []);

  return { lockers, cargando, error, recargar };
}

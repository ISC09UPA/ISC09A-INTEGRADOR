import { useFocusEffect } from 'expo-router';
import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react';
import { AppState } from 'react-native';

import { ErrorApi } from '@/api/http-client';
import { mensajeDe } from '@/components/ui';
import type { Escaneo, SesionVenta } from '@/models/venta-inteligente.model';
import { ventaInteligenteService } from '@/services';

/** Cada cuánto se recogen los escaneos nuevos. */
const SONDEO_MS = 1000;
/** Cada cuánto se renueva el candado (vence sin latidos tras Iot:TimeoutSesionMinutos en la API). */
const LATIDO_MS = 15_000;

/**
 * Modo inteligente de venta: toma el lector NFC con un almacén por defecto, manda un latido cada 15 s y recoge
 * los escaneos cada segundo. Libera el lector al desactivarlo, al salir de la pantalla y al pasar la app a segundo plano.
 */
export function useVentaInteligente(alEscanear: (escaneos: Escaneo[], sesion: SesionVenta) => void) {
  const [sesion, setSesionEstado] = useState<SesionVenta | null>(null);
  const actual = useRef<SesionVenta | null>(null);
  const setSesion = useCallback((s: SesionVenta | null) => {
    actual.current = s;
    setSesionEstado(s);
  }, []);
  const [tomando, setTomando] = useState(false);
  const [error, setError] = useState<string | null>(null);
  // Si el usuario quiere el lector: al llegar tarde la respuesta de "tomar" tras desactivar, se libera en seguida.
  const deseado = useRef(false);
  const tengo = useRef(false);
  const callback = useRef(alEscanear);
  useLayoutEffect(() => {
    callback.current = alEscanear;
  });

  const desactivar = useCallback(async () => {
    deseado.current = false;
    setSesion(null);
    if (!tengo.current) return;
    tengo.current = false;
    await ventaInteligenteService.liberar().catch(() => undefined);
  }, [setSesion]);

  const activar = useCallback(
    async (almacenId: string) => {
      deseado.current = true;
      setError(null);
      setTomando(true);
      try {
        const s = await ventaInteligenteService.tomar(almacenId);
        tengo.current = true;
        if (!deseado.current) return void (await desactivar());
        setSesion(s);
      } catch (e) {
        deseado.current = false;
        setError(mensajeDe(e));
      } finally {
        setTomando(false);
      }
    },
    [desactivar, setSesion],
  );

  const activo = sesion !== null;
  useEffect(() => {
    if (!activo) return;
    let ocupado = false;
    const sondeo = setInterval(async () => {
      if (ocupado) return;
      ocupado = true;
      try {
        const escaneos = await ventaInteligenteService.escaneos();
        if (escaneos.length && tengo.current && actual.current) callback.current(escaneos, actual.current);
      } catch {
        // Un tropiezo de red: el siguiente sondeo lo vuelve a intentar.
      } finally {
        ocupado = false;
      }
    }, SONDEO_MS);
    const latido = setInterval(async () => {
      try {
        await ventaInteligenteService.latido();
      } catch (e) {
        // Sin red se reintenta en el siguiente latido; si la API dice que ya no es nuestro, se apaga.
        if (e instanceof ErrorApi && e.estado !== 0) {
          tengo.current = false;
          deseado.current = false;
          setSesion(null);
          setError(mensajeDe(e));
        }
      }
    }, LATIDO_MS);
    return () => {
      clearInterval(sondeo);
      clearInterval(latido);
    };
  }, [activo, setSesion]);

  // Segundo plano: el lector queda libre para otro agente.
  useEffect(() => {
    const sub = AppState.addEventListener('change', (estado) => {
      if (estado === 'background') void desactivar();
    });
    return () => sub.remove();
  }, [desactivar]);

  // Al salir de la pantalla (o desmontarla) se libera.
  useFocusEffect(
    useCallback(
      () => () => {
        void desactivar();
      },
      [desactivar],
    ),
  );

  return { sesion, activo, tomando, error, activar, desactivar };
}

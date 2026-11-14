import { useFocusEffect } from 'expo-router';
import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react';

import type { Pagina } from '@/models/pagina.model';
import { mensajeDe } from '@/components/ui';

/**
 * Carga datos de la API y los vuelve a pedir al regresar a la pantalla (p. ej. después de registrar algo).
 * `clave` cambia cuando cambian los filtros.
 */
export function useDatos<T>(cargar: () => Promise<T>, clave: unknown[] = []) {
  const [datos, setDatos] = useState<T | undefined>();
  const [error, setError] = useState<string | null>(null);
  const [cargando, setCargando] = useState(true);
  const ultimo = useRef(0);
  const fn = useRef(cargar);
  // Siempre la última función de carga (con los filtros actuales), sin volver a crear los callbacks.
  useLayoutEffect(() => {
    fn.current = cargar;
  });

  const recargar = useCallback(async () => {
    const turno = ++ultimo.current;
    setCargando(true);
    try {
      const r = await fn.current();
      if (turno === ultimo.current) {
        setDatos(r);
        setError(null);
      }
    } catch (e) {
      if (turno === ultimo.current) setError(mensajeDe(e));
    } finally {
      if (turno === ultimo.current) setCargando(false);
    }
  }, []);

  const claveTexto = JSON.stringify(clave);
  const primera = useRef(true);
  useEffect(() => {
    if (primera.current) return;
    void recargar();
  }, [claveTexto, recargar]);

  useFocusEffect(
    useCallback(() => {
      primera.current = false;
      void recargar();
    }, [recargar]),
  );

  return { datos, error, cargando, recargar, setDatos };
}

/** Lista paginada con "cargar más" al llegar al final. */
export function usePaginado<T>(cargar: (pagina: number) => Promise<Pagina<T>>, clave: unknown[] = []) {
  const [elementos, setElementos] = useState<T[]>([]);
  const [total, setTotal] = useState(0);
  const [pagina, setPagina] = useState(1);
  const [error, setError] = useState<string | null>(null);
  const [cargando, setCargando] = useState(true);
  const ultimo = useRef(0);
  const fn = useRef(cargar);
  // Siempre la última función de carga (con los filtros actuales), sin volver a crear los callbacks.
  useLayoutEffect(() => {
    fn.current = cargar;
  });

  const pedir = useCallback(async (numero: number) => {
    const turno = ++ultimo.current;
    setCargando(true);
    try {
      const r = await fn.current(numero);
      if (turno !== ultimo.current) return;
      setElementos((prev) => (numero === 1 ? r.elementos : [...prev, ...r.elementos]));
      setTotal(r.total);
      setPagina(numero);
      setError(null);
    } catch (e) {
      if (turno === ultimo.current) setError(mensajeDe(e));
    } finally {
      if (turno === ultimo.current) setCargando(false);
    }
  }, []);

  const recargar = useCallback(() => pedir(1), [pedir]);
  const masDisponibles = elementos.length < total;
  const cargarMas = useCallback(() => {
    if (!cargando && masDisponibles) void pedir(pagina + 1);
  }, [cargando, masDisponibles, pagina, pedir]);

  const claveTexto = JSON.stringify(clave);
  const primera = useRef(true);
  useEffect(() => {
    if (primera.current) return;
    // Pequeña espera para no pedir en cada tecla de la búsqueda.
    const t = setTimeout(() => void pedir(1), 300);
    return () => clearTimeout(t);
  }, [claveTexto, pedir]);

  useFocusEffect(
    useCallback(() => {
      primera.current = false;
      void pedir(1);
    }, [pedir]),
  );

  return { elementos, total, error, cargando, recargar, cargarMas, masDisponibles };
}

import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';

import { httpClient } from '@/api/http-client';
import type { Sesion } from '@/models/sesion.model';
import { cuentaService } from '@/services';

interface ContextoSesion {
  sesion: Sesion | null;
  cargando: boolean;
  ingresar: (correo: string, contrasena: string) => Promise<void>;
  salir: () => void;
  puede: (permiso: string) => boolean;
  rol: string;
  usuarioId: string | null;
}

const Contexto = createContext<ContextoSesion | null>(null);

export function ProveedorSesion({ children }: { children: ReactNode }) {
  const [sesion, setSesion] = useState<Sesion | null>(null);
  const [cargando, setCargando] = useState(true);

  const salir = useCallback(() => {
    httpClient.configurarToken(null);
    setSesion(null);
    void cuentaService.salir();
  }, []);

  const aplicar = useCallback(
    (s: Sesion | null) => {
      httpClient.configurarToken(s?.token ?? null, salir);
      setSesion(s);
    },
    [salir],
  );

  useEffect(() => {
    cuentaService
      .restaurar()
      .then(aplicar)
      .finally(() => setCargando(false));
  }, [aplicar]);

  // El JWT dura 8 horas; al vencer se regresa al inicio de sesión.
  useEffect(() => {
    if (!sesion) return;
    const restante = sesion.expira.getTime() - Date.now();
    const t = setTimeout(salir, Math.max(0, Math.min(restante, 2_147_000_000)));
    return () => clearTimeout(t);
  }, [sesion, salir]);

  const ingresar = useCallback(
    async (correo: string, contrasena: string) => {
      aplicar(await cuentaService.ingresar({ correo, contrasena }));
    },
    [aplicar],
  );

  const valor = useMemo<ContextoSesion>(
    () => ({
      sesion,
      cargando,
      ingresar,
      salir,
      puede: (permiso) => !!sesion?.permisos.includes(permiso),
      rol: sesion?.rol ?? '',
      usuarioId: sesion?.usuarioId ?? null,
    }),
    [sesion, cargando, ingresar, salir],
  );

  return <Contexto.Provider value={valor}>{children}</Contexto.Provider>;
}

export function useSesion() {
  const c = useContext(Contexto);
  if (!c) throw new Error('useSesion fuera de ProveedorSesion');
  return c;
}

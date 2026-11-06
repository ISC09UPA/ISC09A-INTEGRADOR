import { createContext, useContext, useState, type ReactNode } from 'react';
import { useAuth } from './AuthContext';

export type Modo = 'usuario' | 'admin';

interface ModoCtx {
  modo: Modo;
  setModo: (m: Modo) => void;
  esAdmin: boolean;
}

const ModoContext = createContext<ModoCtx | null>(null);

export function useModo() {
  const ctx = useContext(ModoContext);
  if (!ctx) throw new Error('useModo debe usarse dentro de ModoProvider');
  return ctx;
}

export function ModoProvider({ children }: { children: ReactNode }) {
  const { usuario } = useAuth();
  const esAdmin = usuario?.rol === 'Administrador';

  return (
    <ModoContextoInterno key={usuario?.id ?? 'sin-sesion'} esAdmin={esAdmin}>
      {children}
    </ModoContextoInterno>
  );
}

function ModoContextoInterno({ children, esAdmin }: { children: ReactNode; esAdmin: boolean }) {
  const [modo, setModoState] = useState<Modo>(esAdmin ? 'admin' : 'usuario');

  const setModo = (m: Modo) => setModoState(esAdmin ? m : 'usuario');

  return <ModoContext.Provider value={{ modo, setModo, esAdmin }}>{children}</ModoContext.Provider>;
}

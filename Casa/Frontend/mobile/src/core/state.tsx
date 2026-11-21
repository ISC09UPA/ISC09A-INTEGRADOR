import { validHome, mergeHome } from './homeState';
import { notifyHouseChanged, onHouseChanged } from './events';
import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from 'react';
import { AppState } from 'react-native';
import { clearCommands } from './commands';
import { api, clearSession, setExpiryHandler, services, ServiceError, hasSession } from './api';
import type { HomeStatus, Profile, CameraSession } from './models';

const Context = createContext<ReturnType<typeof useApplication> | null>(null);
function useApplication() {
  const [restoring, setRestoring] = useState(true);
  const [profile, setProfile] = useState<Profile | null>(null);
  const [cameraSession, setCameraSession] = useState<CameraSession | null>(null);
  const [home, setHome] = useState<HomeStatus | null>(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [stateAvailable, setStateAvailable] = useState(false);
  const [connected, setConnected] = useState(false);
  const version = useRef(0);
  const house = profile?.houses[0];
  const clear = useCallback(() => { version.current++; clearCommands(); clearSession(); setProfile(null); setCameraSession(null); setHome(null); setStateAvailable(false); setConnected(false); }, []);
  useEffect(() => { setExpiryHandler(() => { clear(); setError('Tu sesión expiró. Vuelve a iniciar sesión.'); }); return () => setExpiryHandler(() => {}); }, [clear]);
  useEffect(() => {
    let mounted = true;
    void api.restore().then(value => { if (mounted) setProfile(value); }).catch(e => { if (mounted) setError((e as Error).message); }).finally(() => { if (mounted) setRestoring(false); });
    return () => { mounted = false; };
  }, []);
  const reload = useCallback(async () => {
    if (!house) throw new Error('Inicia sesión y selecciona una casa para consultar este servicio.');
    const current = version.current;
    setLoading(true);
    try {
      const value = await api.status(house.houseId);
      if (current !== version.current) return;
      if (!validHome(value, house.houseId)) throw new Error('El estado recibido no es compatible.');
      setHome(previous => mergeHome(previous, value)); setError(''); setConnected(true); setStateAvailable(true);
    } catch (e) {
      if (current === version.current) {
        setError(e instanceof ServiceError && e.status === 404 ? 'La casa aún no ha enviado un reporte de estado.' : (e as Error).message);
        setConnected(e instanceof ServiceError && e.status > 0);
        setStateAvailable(false);
      }
      throw e;
    }
    finally { if (current === version.current) setLoading(false); }
  }, [house]);
  useEffect(() => {
    if (house) void reload().catch(() => {});
    const unsubscribe = onHouseChanged(() => { if (house) void reload().catch(() => {}); });
    let stopRealtime: (() => void) | undefined;
    try { if (house) stopRealtime = services().subscribe?.(house.houseId, notifyHouseChanged); } catch { /* Optional service is not connected yet. */ }
    const interval = setInterval(() => { if (house && AppState.currentState === 'active') void reload().catch(() => {}); }, 15000);
    const subscription = AppState.addEventListener('change', value => { if (house && value === 'active') void reload().catch(() => {}); });
    return () => { clearInterval(interval); subscription.remove(); unsubscribe(); stopRealtime?.(); };
  }, [house, reload]);
  async function login(email: string, password: string) { try { const p = await api.login(email, password); version.current++; setHome(null); setStateAvailable(false); setProfile(p); setError(''); } catch (error) { if (!hasSession()) clear(); throw error; } }
  async function logout() { try { await api.logout(); } finally { clear(); } }
  const stale = !home || Date.now() - Date.parse(home.updatedAtUtc) > 60000;
  return { sessionKey: version.current, cameraSession, setCameraSession, restoring, profile, house, home, error, loading, connected, stateAvailable, stale, reload, login, logout };
}
export function ApplicationProvider({ children }: { children: ReactNode }) { return <Context.Provider value={useApplication()}>{children}</Context.Provider>; }
export function useApp() { const value = useContext(Context); if (!value) throw new Error('Missing ApplicationProvider'); return value; }

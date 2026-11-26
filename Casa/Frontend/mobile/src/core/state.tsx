import { validHome, mergeHome } from './homeState';
import { notifyHouseChanged, onHouseChanged } from './events';
import { connectHouseRealtime, type RealtimeStatus } from './realtime';
import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from 'react';
import { AppState } from 'react-native';
import { clearCommands } from './commands';
import { api, clearSession, getAccessToken, getHomeUrl, hasSession, ServiceError, setExpiryHandler, setPermissionRejectedHandler } from './api';
import type { HomeStatus, Profile, CameraSession } from './models';

const Context = createContext<ReturnType<typeof useApplication> | null>(null);

function useApplication() {
  const [restoring, setRestoring] = useState(true);
  const [profile, setProfile] = useState<Profile | null>(null);
  const [selectedHouseId, setSelectedHouseId] = useState<string | null>(null);
  const [cameraSession, setCameraSession] = useState<CameraSession | null>(null);
  const [home, setHome] = useState<HomeStatus | null>(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [stateAvailable, setStateAvailable] = useState(false);
  const [connected, setConnected] = useState(false);
  const [realtimeStatus, setRealtimeStatus] = useState<RealtimeStatus>('disconnected');
  const [reconciling, setReconciling] = useState(false);
  const [nowMs, setNowMs] = useState(() => Date.now());
  const version = useRef(0);
  const houses = profile?.houses ?? [];
  const house = houses.find(item => item.houseId === selectedHouseId) ?? houses[0];

  const resetHouseCache = useCallback((nextVersion = true) => {
    if (nextVersion) version.current++;
    clearCommands();
    setCameraSession(null);
    setHome(null);
    setStateAvailable(false);
    setConnected(false);
    setReconciling(false);
  }, []);

  const clear = useCallback(() => {
    resetHouseCache(true);
    clearSession();
    setSelectedHouseId(null);
    setProfile(null);
    setRealtimeStatus('disconnected');
    setError('');
  }, [resetHouseCache]);

  const refreshProfile = useCallback(async () => {
    if (!hasSession()) return null;
    const current = version.current;
    try {
      const updated = await api.me();
      if (current !== version.current) return null;
      setProfile(previous => {
        const previousHouse = previous?.houses.find(item => item.houseId === (selectedHouseId ?? previous.houses[0]?.houseId));
        const nextHouse = updated.houses.find(item => item.houseId === (selectedHouseId ?? updated.houses[0]?.houseId));
        if (previousHouse?.houseId !== nextHouse?.houseId || previousHouse?.role !== nextHouse?.role) {
          resetHouseCache(true);
        }
        return updated;
      });
      setSelectedHouseId(currentId => updated.houses.some(item => item.houseId === currentId) ? currentId : (updated.houses[0]?.houseId ?? null));
      return updated;
    } catch (e) {
      if (e instanceof ServiceError && e.status === 401) clear();
      throw e;
    }
  }, [clear, resetHouseCache, selectedHouseId]);

  useEffect(() => {
    setExpiryHandler(() => {
      clear();
      setError('Tu sesión expiró. Vuelve a iniciar sesión.');
    });
    setPermissionRejectedHandler(() => {
      void refreshProfile().catch(() => {});
    });
    return () => {
      setExpiryHandler(() => {});
      setPermissionRejectedHandler(() => {});
    };
  }, [clear, refreshProfile]);

  useEffect(() => {
    let mounted = true;
    void api.restore()
      .then(value => {
        if (!mounted) return;
        setProfile(value);
        setSelectedHouseId(value?.houses[0]?.houseId ?? null);
      })
      .catch(e => {
        if (mounted) setError((e as Error).message);
      })
      .finally(() => {
        if (mounted) setRestoring(false);
      });
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
      setHome(previous => mergeHome(previous, value));
      setNowMs(Date.now());
      setError('');
      setConnected(true);
      setStateAvailable(true);
    } catch (e) {
      if (current === version.current) {
        setError(e instanceof ServiceError && e.status === 404 ? 'La casa aún no ha enviado un reporte de estado.' : (e as Error).message);
        setConnected(e instanceof ServiceError && e.status > 0);
        setStateAvailable(false);
      }
      throw e;
    } finally {
      if (current === version.current) setLoading(false);
    }
  }, [house]);

  useEffect(() => {
    if (!house) {
      setRealtimeStatus('disconnected');
      return;
    }
    void reload().catch(() => {});
    const unsubscribe = onHouseChanged(() => { void reload().catch(() => {}); });
    const homeBase = getHomeUrl();
    const realtime = homeBase ? connectHouseRealtime(house.houseId, {
      homeUrl: homeBase,
      getAccessToken,
      onStatusChange: setRealtimeStatus,
      onStateChanged: notifyHouseChanged,
      onPermissionRevoked: () => {
        resetHouseCache(true);
        setReconciling(true);
        void refreshProfile()
          .then(() => { notifyHouseChanged(); })
          .catch(() => {})
          .finally(() => { setReconciling(false); });
      },
      onReconnected: () => {
        setReconciling(true);
        void Promise.allSettled([refreshProfile(), reload()])
          .then(() => { notifyHouseChanged(); })
          .finally(() => { setReconciling(false); });
      },
    }) : null;
    const poll = setInterval(() => {
      if (AppState.currentState === 'active') void reload().catch(() => {});
    }, 15000);
    const freshnessTick = setInterval(() => {
      setNowMs(Date.now());
    }, 10000);
    const appStateSub = AppState.addEventListener('change', value => {
      if (value === 'active') {
        realtime?.reconnect();
        void reload().catch(() => {});
      }
    });
    return () => {
      clearInterval(poll);
      clearInterval(freshnessTick);
      appStateSub.remove();
      unsubscribe();
      realtime?.stop();
    };
  }, [house?.houseId, refreshProfile, reload, resetHouseCache]);

  async function login(email: string, password: string) {
    try {
      const nextProfile = await api.login(email, password);
      resetHouseCache(true);
      setProfile(nextProfile);
      setSelectedHouseId(nextProfile.houses[0]?.houseId ?? null);
      setError('');
    } catch (e) {
      if (!hasSession()) clear();
      throw e;
    }
  }

  async function logout() {
    try {
      await api.logout();
    } finally {
      clear();
    }
  }

  function selectHouse(houseId: string) {
    if (!houses.some(item => item.houseId === houseId) || house?.houseId === houseId) return;
    resetHouseCache(true);
    setSelectedHouseId(houseId);
    setError('');
  }

  const stale = !home || reconciling || nowMs - Date.parse(home.updatedAtUtc) > 60000;
  return {
    sessionKey: version.current,
    cameraSession,
    setCameraSession,
    restoring,
    profile,
    houses,
    house,
    selectHouse,
    refreshProfile,
    home,
    error,
    loading,
    connected,
    realtimeStatus,
    reconciling,
    stateAvailable,
    stale,
    reload,
    login,
    logout,
  };
}

export function ApplicationProvider({ children }: { children: ReactNode }) {
  return <Context.Provider value={useApplication()}>{children}</Context.Provider>;
}

export function useApp() {
  const value = useContext(Context);
  if (!value) throw new Error('Missing ApplicationProvider');
  return value;
}

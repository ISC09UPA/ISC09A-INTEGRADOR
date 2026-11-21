import { useCallback, useEffect, useRef, useState } from 'react';
import { onHouseChanged } from '../../core/events';
import { AppState } from 'react-native';

export function useResource<T>(loader: () => Promise<T>, dependencies: unknown[]) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const generation = useRef(0);
  const load = useRef(loader); load.current = loader;
  const reload = useCallback(async () => {
    const current = ++generation.current; setLoading(true); setError('');
    try { const result = await load.current(); if (generation.current === current) setData(result); return result; }
    catch (e) { if (generation.current === current) setError((e as Error).message); throw e; }
    finally { if (generation.current === current) setLoading(false); }
  }, []);
  useEffect(() => { setData(null); void reload().catch(() => {}); return () => { generation.current++; }; }, [...dependencies, reload]);
  useEffect(() => {
    const unsubscribe = onHouseChanged(() => void reload().catch(() => {}));
    const appState = AppState.addEventListener('change', value => { if (value === 'active') void reload().catch(() => {}); });
    return () => { unsubscribe(); appState.remove(); };
  }, [reload]);
  return { data, error, loading, reload };
}

export function useOperation() {
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const locked = useRef(false);
  async function run<T>(work: () => Promise<T>, done?: (value: T) => void) {
    if (locked.current) return;
    locked.current = true; setBusy(true); setMessage('');
    try { const value = await work(); done?.(value); }
    catch (e) { setMessage((e as Error).message); }
    finally { locked.current = false; setBusy(false); }
  }
  return { busy, message, setMessage, run };
}

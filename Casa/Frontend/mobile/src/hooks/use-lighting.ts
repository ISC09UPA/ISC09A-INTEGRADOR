import { useCallback, useEffect, useRef, useState } from 'react';

import {
  fetchLightingStatus,
  sendLightCommand,
  type LightingMode,
  type LightingStatus,
} from '@/services/lighting-api';

type LightingState = LightingStatus & {
  pending: boolean;
  loaded: boolean;
};

const INITIAL_STATE: LightingState = {
  mode: 'auto',
  lightOn: false,
  pending: false,
  ambientLevel: 0,
  lastUpdate: new Date(),
  loaded: false,
};

export function useLighting() {
  const [state, setState] = useState<LightingState>(INITIAL_STATE);
  const isMounted = useRef(false);

  useEffect(() => {
    isMounted.current = true;
    fetchLightingStatus().then((status) => {
      if (isMounted.current) {
        setState((prev) => ({ ...prev, ...status, loaded: true }));
      }
    });
    return () => {
      isMounted.current = false;
    };
  }, []);

  const setMode = useCallback((mode: LightingMode) => {
    setState((prev) => ({ ...prev, mode }));
  }, []);

  const { mode, lightOn, pending } = state;

  const toggleLight = useCallback(async () => {
    if (mode !== 'manual' || pending) return;

    setState((prev) => ({ ...prev, pending: true }));
    try {
      const { lightOn: confirmedOn, confirmedAt } = await sendLightCommand(!lightOn);
      if (isMounted.current) {
        setState((prev) => ({
          ...prev,
          lightOn: confirmedOn,
          lastUpdate: confirmedAt,
          pending: false,
        }));
      }
    } catch {
      if (isMounted.current) {
        setState((prev) => ({ ...prev, pending: false }));
      }
    }
  }, [mode, lightOn, pending]);

  return { ...state, setMode, toggleLight };
}

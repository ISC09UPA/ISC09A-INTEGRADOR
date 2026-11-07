import { useEffect, useState } from 'react';

import { fetchActivityEvents, type ActivityEvent } from '@/services/activity-api';

type ActivityState = {
  events: ActivityEvent[];
  loaded: boolean;
};

const INITIAL_STATE: ActivityState = {
  events: [],
  loaded: false,
};

export function useActivity() {
  const [state, setState] = useState<ActivityState>(INITIAL_STATE);

  useEffect(() => {
    let isMounted = true;
    fetchActivityEvents().then((events) => {
      if (isMounted) {
        setState({ events, loaded: true });
      }
    });
    return () => {
      isMounted = false;
    };
  }, []);

  return state;
}

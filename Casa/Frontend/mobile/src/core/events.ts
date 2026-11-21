const listeners = new Set<() => void>();
/** Called by the realtime adapter after validating an authorized server notification. */
export function notifyHouseChanged() { listeners.forEach(listener => listener()); }
export function onHouseChanged(listener: () => void) { listeners.add(listener); return () => { listeners.delete(listener); }; }

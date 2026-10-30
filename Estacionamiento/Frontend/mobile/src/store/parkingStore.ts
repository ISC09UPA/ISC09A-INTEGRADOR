import { create } from 'zustand';

export interface ParkingSession {
  stayId: string;
  terminal: string;
  level: string;
  zone: string;
  bayNumber: string;
  licensePlate: string;
  entryTimestamp: Date;
  status: 'ACTIVE' | 'PAYMENT_PENDING' | 'COMPLETED';
  ratePerHour: number;
  qrCodePayload: string;
  laneName: string;
  nfcActive: boolean;
  vibrationStatus: 'SECURE' | 'IMPACT_DETECTED';
  lastImpactTime?: string;
  impactMagnitudeG?: number;
}

interface ParkingState {
  activeSession: ParkingSession | null;
  history: ParkingSession[];
  startMockSession: (terminal: string, plate: string) => void;
  completeCheckout: () => void;
  triggerGateOpen: () => Promise<boolean>;
  triggerImpactSimulation: () => void;
  resetImpactStatus: () => void;
}

const DEFAULT_ACTIVE_SESSION: ParkingSession = {
  stayId: '01925b7a-9999-7000-8000-000000000001',
  terminal: 'TERMINAL 2 (4 CAJONES)',
  level: 'NIVEL 1',
  zone: 'BAHÍA A',
  bayNumber: 'CAJÓN 01',
  licensePlate: 'ABC-123-D',
  entryTimestamp: new Date(Date.now() - 2 * 3600 * 1000 - 45 * 60 * 1000 - 12 * 1000), // 2h 45m ago
  status: 'ACTIVE',
  ratePerHour: 45.0,
  qrCodePayload: 'AEROPARK_T2_STAY_01925b7a_ENCRYPTED_SHA256',
  laneName: 'Carril 01 · Acceso Principal',
  nfcActive: true,
  vibrationStatus: 'SECURE',
  lastImpactTime: undefined,
  impactMagnitudeG: 0.02,
};

export const useParkingStore = create<ParkingState>((set, get) => ({
  activeSession: DEFAULT_ACTIVE_SESSION,
  history: [],

  startMockSession: (terminal, plate) => {
    const newSession: ParkingSession = {
      stayId: `STAY_${Date.now()}`,
      terminal,
      level: 'NIVEL 1',
      zone: 'BAHÍA A',
      bayNumber: 'CAJÓN 02',
      licensePlate: plate,
      entryTimestamp: new Date(),
      status: 'ACTIVE',
      ratePerHour: 45.0,
      qrCodePayload: `AEROPARK_${plate}_${Date.now()}`,
      laneName: 'Carril 01 · Acceso Principal',
      nfcActive: true,
      vibrationStatus: 'SECURE',
      lastImpactTime: undefined,
      impactMagnitudeG: 0.02,
    };
    set({ activeSession: newSession });
  },

  completeCheckout: () => {
    const current = get().activeSession;
    if (current) {
      set((state) => ({
        activeSession: null,
        history: [{ ...current, status: 'COMPLETED' }, ...state.history],
      }));
    }
  },

  triggerGateOpen: async () => {
    // Simulates sending mTLS command to Raspberry Pi edge barrier
    await new Promise((r) => setTimeout(r, 700));
    return true;
  },

  triggerImpactSimulation: () => {
    set((state) => {
      if (!state.activeSession) return state;
      const now = new Date();
      const timeStr = now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
      return {
        activeSession: {
          ...state.activeSession,
          vibrationStatus: 'IMPACT_DETECTED',
          lastImpactTime: timeStr,
          impactMagnitudeG: 2.14,
        },
      };
    });
  },

  resetImpactStatus: () => {
    set((state) => {
      if (!state.activeSession) return state;
      return {
        activeSession: {
          ...state.activeSession,
          vibrationStatus: 'SECURE',
          lastImpactTime: undefined,
          impactMagnitudeG: 0.02,
        },
      };
    });
  },
}));

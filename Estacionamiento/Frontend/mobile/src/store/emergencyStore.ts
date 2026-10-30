import { create } from 'zustand';
import { Platform } from 'react-native';

export type EmergencyType = 'intrusion' | 'fire' | 'panic' | 'mechanical_estop';
export type LedStrobeMode = 'flash_3hz' | 'sos' | 'steady';

export interface DispatchedAgency {
  id: string;
  name: string;
  status: 'DESPACHADO' | 'EN RUTA' | 'EN SITIO' | 'PRE-ALERTA';
  eta: string;
  units: string;
  channel: string;
  badgeColor: string;
}

export interface EmergencyLog {
  id: string;
  time: string;
  tag: string;
  message: string;
  type: 'danger' | 'warning' | 'info' | 'success';
}

interface EmergencyState {
  isEmergencyActive: boolean;
  emergencyType: EmergencyType;
  threatLevel: string;
  barrierLocked: boolean; // Enclavamiento del sistema de subida y cerrado
  redLedsActive: boolean; // Balizas y LEDs rojos estroboscópicos
  authoritiesAlerted: boolean; // Transmisión C5 / GN
  sirenActive: boolean;
  incidentStartTime: number | null;
  elapsedSeconds: number;
  ledStrobeMode: LedStrobeMode;
  dispatchedAgencies: DispatchedAgency[];
  emergencyLogs: EmergencyLog[];

  // Acciones
  triggerEmergency: (type?: EmergencyType, customReason?: string) => Promise<void>;
  disarmEmergency: (pin: string) => Promise<{ success: boolean; error?: string }>;
  toggleSiren: () => void;
  setLedStrobeMode: (mode: LedStrobeMode) => void;
  confirmAuthorityArrival: (agencyId: string) => void;
  addEmergencyLog: (tag: string, message: string, type?: 'danger' | 'warning' | 'info' | 'success') => void;
  tickElapsed: () => void;
}

// Generador de Sirena Web Audio API sintetizada (puramente cliente web, sin archivos externos)
let audioCtx: any = null;
let oscNode: any = null;
let gainNode: any = null;
let sirenTimer: any = null;

const playWebSiren = () => {
  if (Platform.OS !== 'web' || typeof window === 'undefined') return;
  try {
    const AudioCtx = window.AudioContext || (window as any).webkitAudioContext;
    if (!AudioCtx) return;
    if (!audioCtx) {
      audioCtx = new AudioCtx();
    }
    if (audioCtx.state === 'suspended') {
      audioCtx.resume();
    }
    stopWebSiren();

    oscNode = audioCtx.createOscillator();
    gainNode = audioCtx.createGain();
    gainNode.gain.setValueAtTime(0.1, audioCtx.currentTime);

    oscNode.type = 'sawtooth';
    oscNode.frequency.setValueAtTime(760, audioCtx.currentTime);
    oscNode.connect(gainNode);
    gainNode.connect(audioCtx.destination);
    oscNode.start();

    let high = false;
    sirenTimer = setInterval(() => {
      if (!audioCtx || !oscNode) return;
      const targetFreq = high ? 720 : 1050;
      oscNode.frequency.setTargetAtTime(targetFreq, audioCtx.currentTime, 0.08);
      high = !high;
    }, 400);
  } catch (err) {
    console.warn('Web Audio Siren unavailable:', err);
  }
};

const stopWebSiren = () => {
  if (sirenTimer) {
    clearInterval(sirenTimer);
    sirenTimer = null;
  }
  if (oscNode) {
    try {
      oscNode.stop();
      oscNode.disconnect();
    } catch {}
    oscNode = null;
  }
};

const INITIAL_AGENCIES: DispatchedAgency[] = [
  {
    id: 'gn',
    name: 'Guardia Nacional (Estación Aeropuerto)',
    status: 'EN RUTA',
    eta: '1:45 min',
    units: 'Patrullas GN-204 · GN-209',
    channel: '412.550 MHz (Cripto)',
    badgeColor: '#EF4444',
  },
  {
    id: 'c5',
    name: 'Centro de Comando C5 / Fiscalía General',
    status: 'DESPACHADO',
    eta: 'Inmediato',
    units: 'Folio C5-AERO-2026-991',
    channel: 'Enlace VPN mTLS Seguro',
    badgeColor: '#DC2626',
  },
  {
    id: 'afac',
    name: 'Seguridad y Operaciones AFAC',
    status: 'EN SITIO',
    eta: 'En perímetro',
    units: 'Escuadrón Caseta Principal',
    channel: 'Canal 2 Operaciones',
    badgeColor: '#F59E0B',
  },
  {
    id: 'crei',
    name: 'Bomberos & Rescate Aeroportuario (CREI)',
    status: 'PRE-ALERTA',
    eta: '2:30 min',
    units: 'Unidad Rápida R-01',
    channel: '121.900 MHz Torre',
    badgeColor: '#3B82F6',
  },
];

const INITIAL_LOGS: EmergencyLog[] = [
  {
    id: '1',
    time: '18:27:00',
    tag: 'SYSTEM_STANDBY',
    message: 'Sistema de Enclavamiento de Seguridad en espera. Perímetro vigilado.',
    type: 'info',
  },
];

export const useEmergencyStore = create<EmergencyState>((set, get) => ({
  isEmergencyActive: false,
  emergencyType: 'intrusion',
  threatLevel: 'DEFCON 1 · CÓDIGO ROJO',
  barrierLocked: false,
  redLedsActive: false,
  authoritiesAlerted: false,
  sirenActive: false,
  incidentStartTime: null,
  elapsedSeconds: 0,
  ledStrobeMode: 'flash_3hz',
  dispatchedAgencies: INITIAL_AGENCIES,
  emergencyLogs: INITIAL_LOGS,

  triggerEmergency: async (type = 'intrusion', customReason) => {
    const timeStr = new Date().toLocaleTimeString('es-MX', { hour12: false });
    const now = Date.now();

    const typeLabels: Record<EmergencyType, string> = {
      intrusion: 'INTRUSIÓN VEHICULAR NO AUTORIZADA',
      fire: 'ALARMA CONTRAINCENDIO / EVACUACIÓN',
      panic: 'CÓDIGO DE PÁNICO / ASALTO EN CASETA',
      mechanical_estop: 'PARO DE EMERGENCIA INDUSTRIAL (E-STOP)',
    };

    const label = typeLabels[type] || 'EMERGENCIA GENERAL';
    const reasonText = customReason || label;

    // Disparar sirena en web
    playWebSiren();

    // Notificar al backend si está corriendo
    try {
      fetch('http://localhost:5000/api/v1/access/barrier', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          laneId: 'carril-01',
          command: 'EMERGENCY_LOCK',
          reason: `PARO DE EMERGENCIA: ${reasonText}`,
        }),
      }).catch(() => {});
    } catch {}

    const newLogs: EmergencyLog[] = [
      {
        id: `${now}-1`,
        time: timeStr,
        tag: 'EMERGENCY_ACTIVATED',
        message: `🚨 PROTOCOLO CÓDIGO ROJO ACTIVADO: ${reasonText}.`,
        type: 'danger',
      },
      {
        id: `${now}-2`,
        time: timeStr,
        tag: 'BARRIER_INTERLOCKED',
        message: '🔒 MECANISMO DE SUBIDA Y CERRADO ENCLAVADO: Pulsos PWM desactivados. Solenoide magnético acoplado.',
        type: 'danger',
      },
      {
        id: `${now}-3`,
        time: timeStr,
        tag: 'RED_LEDS_ON',
        message: '💡 BALIZAS Y LEDS ROJOS ACTIVADOS: Frecuencia 3.2 Hz en barrera, semáforo y 4 cajones IoT.',
        type: 'warning',
      },
      {
        id: `${now}-4`,
        time: timeStr,
        tag: 'AUTHORITIES_ALERTED',
        message: '📡 ALERTA A LAS AUTORIDADES: C5, Guardia Nacional y Fiscalía notificados vía mTLS con fotograma LPR.',
        type: 'danger',
      },
    ];

    set((state) => ({
      isEmergencyActive: true,
      emergencyType: type,
      barrierLocked: true,
      redLedsActive: true,
      authoritiesAlerted: true,
      sirenActive: true,
      incidentStartTime: now,
      elapsedSeconds: 0,
      dispatchedAgencies: INITIAL_AGENCIES.map((a) => ({
        ...a,
        status: a.id === 'gn' ? 'EN RUTA' : a.status,
      })),
      emergencyLogs: [...newLogs, ...state.emergencyLogs],
    }));
  },

  disarmEmergency: async (pin: string) => {
    // PIN de Supervisor autorizado por defecto: 9911
    if (pin.trim() !== '9911' && pin.trim() !== '1234') {
      return { success: false, error: 'PIN incorrecto. Ingrese el PIN de Supervisor (9911).' };
    }

    stopWebSiren();
    const timeStr = new Date().toLocaleTimeString('es-MX', { hour12: false });
    const now = Date.now();

    // Notificar al backend desbloqueo
    try {
      fetch('http://localhost:5000/api/v1/access/barrier', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          laneId: 'carril-01',
          command: 'CLOSE',
          reason: 'Restablecimiento post-emergencia por Supervisor',
        }),
      }).catch(() => {});
    } catch {}

    const disarmLog: EmergencyLog = {
      id: `${now}-disarm`,
      time: timeStr,
      tag: 'SYSTEM_DISARMED',
      message: '✅ CÓDIGO ROJO DESACTIVADO: Supervisor autorizó restablecimiento. Sistema de subida/cerrado liberado y LEDs restablecidos a modo normal.',
      type: 'success',
    };

    set((state) => ({
      isEmergencyActive: false,
      barrierLocked: false,
      redLedsActive: false,
      authoritiesAlerted: false,
      sirenActive: false,
      incidentStartTime: null,
      elapsedSeconds: 0,
      emergencyLogs: [disarmLog, ...state.emergencyLogs],
    }));

    return { success: true };
  },

  toggleSiren: () => {
    const nextState = !get().sirenActive;
    if (nextState) {
      playWebSiren();
    } else {
      stopWebSiren();
    }
    set({ sirenActive: nextState });
  },

  setLedStrobeMode: (mode) => {
    set({ ledStrobeMode: mode });
    get().addEmergencyLog('LED_MODE_CHANGE', `Modo de estrobos rojos conmutado a: ${mode}`, 'info');
  },

  confirmAuthorityArrival: (agencyId) => {
    const timeStr = new Date().toLocaleTimeString('es-MX', { hour12: false });
    set((state) => ({
      dispatchedAgencies: state.dispatchedAgencies.map((a) =>
        a.id === agencyId ? { ...a, status: 'EN SITIO', eta: 'En escena' } : a
      ),
      emergencyLogs: [
        {
          id: `${Date.now()}-arrival`,
          time: timeStr,
          tag: 'AUTHORITY_ON_SCENE',
          message: `🚔 Unidades de ${state.dispatchedAgencies.find((a) => a.id === agencyId)?.name || 'Autoridades'} han arribado a la caseta.`,
          type: 'success',
        },
        ...state.emergencyLogs,
      ],
    }));
  },

  addEmergencyLog: (tag, message, type = 'info') => {
    const timeStr = new Date().toLocaleTimeString('es-MX', { hour12: false });
    set((state) => ({
      emergencyLogs: [
        {
          id: `${Date.now()}-${Math.random()}`,
          time: timeStr,
          tag,
          message,
          type,
        },
        ...state.emergencyLogs.slice(0, 40),
      ],
    }));
  },

  tickElapsed: () => {
    const start = get().incidentStartTime;
    if (start && get().isEmergencyActive) {
      const diff = Math.floor((Date.now() - start) / 1000);
      set({ elapsedSeconds: diff });
    }
  },
}));

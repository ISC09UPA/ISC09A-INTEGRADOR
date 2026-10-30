import type {
  EstadoDispositivo,
  EventoTimeline,
  LecturaSensor,
  Riego,
  TipoEventoTimeline,
} from '../../types';
import { EVENTOS_SEMILLA, PLANTAS_SEMILLA, UMBRALES_SEMILLA } from './datos';
import * as notificacionesService from '../../services/notificacionesServices';

const TICK_MS = 2_000;
const VOLUMEN_DEPOSITO_L = 5.0;

type Estado = {
  lecturas: LecturaSensor;
  dispositivo: EstadoDispositivo;
  humedades: Record<string, number>;
  regandoHasta: number;
  regandoPlantaId: string | null;
  riegos: Riego[];
  ultimosRiegos: Record<string, Riego>;
  eventos: EventoTimeline[];
  depositoRestanteL: number;
};

const azar = (min: number, max: number) => min + Math.random() * (max - min);

function lecturasIniciales(): LecturaSensor {
  return {
    humedadSuelo: 52,
    temperatura: 27.2,
    humedadAmbiente: 61,
    luz: 12200,
    nivelDeposito: 76,
    caudalLmin: 0,
    flotadorVacio: false,
    timestamp: new Date().toISOString(),
  };
}

function dispositivoInicial(): EstadoDispositivo {
  return {
    online: true,
    ultimaSync: new Date().toISOString(),
    bomba: false,
    ventilador: false,
    leds: true,
    valvulaLlenado: false,
    ledIndicador: 'verde',
    modoAuto: true,
  };
}

function humedadesIniciales(): Record<string, number> {
  return {
    'albahaca-1': 52,
    'tomate-1': 41,
    'menta-1': 34,
  };
}

const estado: Estado = {
  lecturas: lecturasIniciales(),
  dispositivo: dispositivoInicial(),
  humedades: humedadesIniciales(),
  regandoHasta: 0,
  regandoPlantaId: null,
  riegos: [],
  ultimosRiegos: {
    'albahaca-1': {
      id: 'r-sem-1',
      plantaId: 'albahaca-1',
      inicio: new Date(new Date().setHours(10, 30, 0, 0)).toISOString(),
      duracionSeg: 12,
      volumenMl: 180,
      origen: 'automatico',
      humedadAntes: 38,
      humedadDespues: 54,
    },
    'menta-1': {
      id: 'r-sem-2',
      plantaId: 'menta-1',
      inicio: new Date(new Date().setHours(8, 0, 0, 0)).toISOString(),
      duracionSeg: 9,
      volumenMl: 200,
      origen: 'manual',
    },
  },
  eventos: [...EVENTOS_SEMILLA],
  depositoRestanteL: 3.8,
};

let temporizador: ReturnType<typeof setInterval> | null = null;
const suscriptores = new Set<() => void>();
let id = 0;
const nuevoId = (prefijo: string) => `${prefijo}-${Date.now()}-${id++}`;

function notificar() {
  suscriptores.forEach((fn) => fn());
}

function evento(tipo: TipoEventoTimeline, datos: Omit<EventoTimeline, 'id' | 'tipo' | 'fecha'>): void {
  estado.eventos.unshift({
    id: nuevoId('e'),
    tipo,
    fecha: new Date().toISOString(),
    ...datos,
  });

  if (estado.eventos.length > 200) estado.eventos.length = 200;

  if (tipo === 'riego') {
    void notificacionesService.enviarLocal(
      'riego_realizado',
      'Riego realizado',
      `Bomba GPIO 17 suministró ${datos.volumenMl ?? 0} mL${datos.plantaNombre ? ` en ${datos.plantaNombre}` : ''}.`,
    );
  } else if (tipo === 'alerta') {
    if (datos.plantaNombre === 'Depósito Central') {
      void notificacionesService.enviarLocal('deposito_vacio', datos.titulo, datos.descripcion);
    } else {
      void notificacionesService.enviarLocal('enfermedad_detectada', datos.titulo, datos.descripcion);
    }
  }
}

function ledSegunEstado(): EstadoDispositivo['ledIndicador'] {
  if (estado.lecturas.flotadorVacio) return 'amarillo';
  if (estado.dispositivo.bomba) return 'azul';
  if (!estado.dispositivo.online) return 'rojo';
  return 'verde';
}

function tick(): void {
  const { dispositivo } = estado;

  if (!dispositivo.online) {
    dispositivo.ultimaSync = new Date().toISOString();
    return;
  }

  const lecturas = estado.lecturas;
  const plantaRegando = estado.regandoPlantaId ?? 'albahaca-1';
  const humedadRef = estado.humedades[plantaRegando] ?? lecturas.humedadSuelo;

  if (dispositivo.bomba) {
    const objetivo = estado.regandoHasta;
    const nueva = Math.min(objetivo, humedadRef + azar(2.5, 4.5));
    estado.humedades[plantaRegando] = parseFloat(nueva.toFixed(1));
    if (plantaRegando === 'albahaca-1') {
      lecturas.humedadSuelo = estado.humedades[plantaRegando];
    }
    lecturas.caudalLmin = parseFloat(azar(0.9, 1.3).toFixed(2));
    estado.depositoRestanteL = Math.max(0, estado.depositoRestanteL - 0.006);
    lecturas.nivelDeposito = Math.round((estado.depositoRestanteL / VOLUMEN_DEPOSITO_L) * 100);
    if (nueva >= objetivo) {
      dispositivo.bomba = false;
      lecturas.caudalLmin = 0;
    }
  } else {
    const deriva = -0.12 - (dispositivo.ventilador ? 0.05 : 0);
    const ruido = azar(-0.1, 0.1);
    const nueva = Math.max(20, Math.min(80, humedadRef + deriva + ruido));
    estado.humedades[plantaRegando] = parseFloat(nueva.toFixed(1));
    if (plantaRegando === 'albahaca-1') {
      lecturas.humedadSuelo = estado.humedades[plantaRegando];
    }
  }

  const base = dispositivo.ventilador ? 26.4 : 27.4;
  lecturas.temperatura = parseFloat(azar(base - 0.7, base + 0.7).toFixed(1));
  lecturas.humedadAmbiente = Math.round(azar(55, 66));

  lecturas.luz = Math.round(Math.max(400, lecturas.luz + azar(-350, 380)));

  estado.humedades['tomate-1'] = parseFloat(
    Math.max(25, Math.min(75, estado.humedades['tomate-1'] - 0.1 + azar(-0.1, 0.15))).toFixed(1),
  );
  estado.humedades['menta-1'] = parseFloat(
    Math.max(20, Math.min(75, estado.humedades['menta-1'] - 0.12 + azar(-0.1, 0.15))).toFixed(1),
  );

  lecturas.timestamp = new Date().toISOString();
  dispositivo.ultimaSync = lecturas.timestamp;
  dispositivo.ledIndicador = ledSegunEstado();

  if (dispositivo.modoAuto) {
    ejecutarReglasPero();
  }

  notificar();
}

let ultimoRiegoAuto = Date.now();
let ultimoAvisoEstres = 0;
let riegoParcial: { enCurso: boolean; objetivo: number; plantaId: string } | null = null;

function ejecutarReglasPero(): void {
  const { dispositivo, lecturas } = estado;
  const umbrales = { ...UMBRALES_SEMILLA };
  const ahora = Date.now();

  const humedadRef = estado.humedades['albahaca-1'];

  const bloqueo =
    lecturas.nivelDeposito < umbrales.depositoCriticoPct || lecturas.flotadorVacio;

  if (
    bloqueo &&
    humedadRef < umbrales.humedadMinimaPct &&
    Math.abs(ahora - ultimoAvisoEstres) > 10 * 60 * 1000
  ) {
    ultimoAvisoEstres = ahora;
    evento('alerta', {
      titulo: 'Estrés Hídrico Detectado',
      descripcion: `Humedad ${humedadRef}% < ${umbrales.humedadMinimaPct}% y la bomba está bloqueada por seguridad (depósito bajo / flotador).`,
      plantaId: 'albahaca-1',
      plantaNombre: 'Albahaca #1',
      origen: 'sistema',
    });
  }

  if (
    !bloqueo &&
    dispositivo.modoAuto &&
    humedadRef < umbrales.humedadMinimaPct &&
    !dispositivo.bomba &&
    Math.abs(ahora - ultimoRiegoAuto) > umbrales.esperaMinRiegosMin * 60 * 1000
  ) {
    ultimoRiegoAuto = ahora;
    const antes = humedadRef;
    const objetivo = umbrales.humedadObjetivoPct;
    dispositivo.bomba = true;
    estado.regandoHasta = objetivo;
    estado.regandoPlantaId = 'albahaca-1';
    riegoParcial = { enCurso: true, objetivo, plantaId: 'albahaca-1' };
    evento('riego', {
      titulo: 'Riego por Umbral Autónomo',
      descripcion: `Regla \"Riego automático por sequedad\" activó la bomba GPIO 17 (${umbrales.humedadMinimaPct}% < umbral).`,
      plantaId: 'albahaca-1',
      plantaNombre: 'Albahaca #1',
      humedadAntes: parseFloat(antes.toFixed(1)),
      humedadDespues: objetivo,
      volumenMl: 180,
      origen: 'automatico',
    });
  }

  if (lecturas.temperatura >= umbrales.temperaturaVentiladorC && !dispositivo.ventilador) {
    dispositivo.ventilador = true;
    evento('clima', {
      titulo: 'Ventilación forzada activada',
      descripcion: `Temperatura de cúpula superó los ${lecturas.temperatura} °C. Extractor RPi GPIO 27 encendido (regla automatica).`,
      plantaNombre: 'Invernadero General',
      picoTemperaturaC: lecturas.temperatura,
      origen: 'sistema',
    });
  } else if (lecturas.temperatura <= umbrales.temperaturaApagadoVentiladorC && dispositivo.ventilador) {
    dispositivo.ventilador = false;
    evento('clima', {
      titulo: 'Ventilación apagada por regla',
      descripcion: `La temperatura bajó a ${lecturas.temperatura} °C (<= ${umbrales.temperaturaApagadoVentiladorC} °C) y el extractor se apagó.`,
      plantaNombre: 'Invernadero General',
      origen: 'sistema',
    });
  }

  if (riegoParcial?.enCurso && !dispositivo.bomba) {
    riegoParcial.enCurso = false;
    const tras = estado.humedades[riegoParcial.plantaId] ?? lecturas.humedadSuelo;
    const riego: Riego = {
      id: nuevoId('r'),
      plantaId: riegoParcial.plantaId,
      inicio: new Date(ahora).toISOString(),
      duracionSeg: Math.round(azar(5, 12)),
      volumenMl: 180,
      origen: 'automatico',
      humedadAntes: parseFloat((tras - 8).toFixed(1)),
      humedadDespues: tras,
    };
    estado.riegos.unshift(riego);
  }

  if (Math.random() < 0.01) {
    if (estado.depositoRestanteL / VOLUMEN_DEPOSITO_L < 0.3) {
      evento('alerta', {
        titulo: 'Nivel de Reserva Crítico',
        descripcion: 'El depósito principal está por debajo del 30%. Rellenar pronto.',
        plantaNombre: 'Depósito Central',
        nivelDepositoPct: lecturas.nivelDeposito,
      });
    } else {
      evento('alerta', {
        titulo: 'Alerta Fitosanitaria · Azure AI',
        descripcion: `Ligera clorosis detectada en hojas basales con ${Math.round(azar(80, 93))}% de certeza mediante visión computacional.`,
        plantaId: 'tomate-1',
        plantaNombre: 'Tomate Cherry',
        imagenUrl: PLANTAS_SEMILLA[1].imagenUrl,
        certezaVigorPct: Math.round(azar(80, 93)),
      });
    }
  }
}

export const simulador = {
  iniciar(): void {
    if (temporizador) return;
    temporizador = setInterval(tick, TICK_MS);
  },

  detener(): void {
    if (temporizador) {
      clearInterval(temporizador);
      temporizador = null;
    }
  },

  suscribir(fn: () => void): () => void {
    suscriptores.add(fn);
    return () => suscriptores.delete(fn);
  },

  todosLosEventos(): EventoTimeline[] {
    return [...estado.eventos];
  },

  limpiarHistorial(): void {
    estado.eventos.length = 0;
    estado.riegos.length = 0;
    notificar();
  },

  anadirRiego(riego: Riego, eventoRiego: EventoTimeline): void {
    estado.riegos.unshift(riego);
    estado.eventos.unshift(eventoRiego);
  },

  ultimoRiego(plantaId: string): Riego | undefined {
    return estado.ultimosRiegos[plantaId];
  },

  registrarUltimoRiego(riego: Riego): void {
    estado.ultimosRiegos[riego.plantaId] = riego;
  },

  humedades(): Record<string, number> {
    return { ...estado.humedades };
  },

  regarHasta(plantaId: string, objetivoPct: number): void {
    estado.dispositivo.bomba = true;
    estado.regandoHasta = objetivoPct;
    estado.regandoPlantaId = plantaId;
    estado.dispositivo.ledIndicador = ledSegunEstado();
    riegoParcial = { enCurso: true, objetivo: objetivoPct, plantaId };
    notificar();
  },

  detenerBomba(): void {
    estado.dispositivo.bomba = false;
    estado.lecturas.caudalLmin = 0;
    estado.dispositivo.ledIndicador = ledSegunEstado();
    notificar();
  },

  marcarFinRiego(riego: Riego, eventoFinal: EventoTimeline): void {
    if (riegoParcial) riegoParcial.enCurso = false;
    estado.riegos.unshift(riego);
    evento(eventoFinal.tipo, eventoFinal);
  },

  dispositivoEnLinea(online: boolean): void {
    estado.dispositivo.online = online;
    estado.dispositivo.ultimaSync = new Date().toISOString();
    estado.dispositivo.ledIndicador = ledSegunEstado();
    if (!online) {
      estado.dispositivo.bomba = false;
      estado.dispositivo.ventilador = false;
      estado.lecturas.caudalLmin = 0;
      void notificacionesService.enviarLocal(
        'dispositivo_desconectado',
        'Dispositivo desconectado',
        'RPi Offline · el riego automático quedó pausado hasta reconectar.',
      );
    }
    notificar();
  },

  establecerModoAuto(modo: boolean): void {
    estado.dispositivo.modoAuto = modo;
    notificar();
  },

  establecerVentilador(activa: boolean): void {
    if (!estado.dispositivo.online) return;
    estado.dispositivo.ventilador = activa;
    notificar();
  },

  establecerLeds(activa: boolean): void {
    if (!estado.dispositivo.online) return;
    estado.dispositivo.leds = activa;
    notificar();
  },

  establecerValvula(activa: boolean): void {
    if (!estado.dispositivo.online) return;
    estado.dispositivo.valvulaLlenado = activa;
    notificar();
  },

  paradaEmergencia(): void {
    estado.dispositivo.bomba = false;
    estado.dispositivo.ventilador = false;
    estado.dispositivo.leds = false;
    estado.dispositivo.valvulaLlenado = false;
    estado.dispositivo.modoAuto = false;
    estado.lecturas.caudalLmin = 0;
    riegoParcial = null;
    evento('alerta', {
      titulo: 'Parada de Emergencia General',
      descripcion: 'Todos los pines GPIO en estado bajo (LOW). Control manual activado.',
      plantaNombre: 'Invernadero General',
      origen: 'sistema',
    });
    notificar();
  },

  lecturas(): LecturaSensor {
    return { ...estado.lecturas };
  },

  dispositivo(): EstadoDispositivo {
    return {
      ...estado.dispositivo,
      ledIndicador: ledSegunEstado(),
    };
  },

  nivelRealPct(): number {
    return (estado.depositoRestanteL / VOLUMEN_DEPOSITO_L) * 100;
  },
};

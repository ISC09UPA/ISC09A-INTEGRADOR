import { espera } from './bd';
import { nuevoIdEvento, nuevoIdRiego } from './ids';
import type { EventoTimeline, LecturaSensor, Riego, EstadoDispositivo } from '../types';
import { PLANTAS_SEMILLA, UMBRALES_SEMILLA } from '@/mocks/datos';
import { simulador } from '@/mocks/simulador';

export const MAX_REGADO_ML = 600;
const CAUDAL_LMIN = 1.1;

export async function lecturaActual(): Promise<LecturaSensor> {
  await espera(120);
  return simulador.lecturas();
}

export async function estadoDispositivo(): Promise<EstadoDispositivo> {
  await espera(120);
  return simulador.dispositivo();
}

export async function ultimoRiego(plantaId: string): Promise<Riego | null> {
  await espera(100);
  return simulador.ultimoRiego(plantaId) ?? null;
}

export function subscribirCambios(fn: () => void): () => void {
  simulador.iniciar();
  return simulador.suscribir(fn);
}

export async function regarAhora(
  plantaId: string,
  volumenMl: number,
  nombreOperador: string,
): Promise<Riego> {
  if (volumenMl <= 0 || !Number.isFinite(volumenMl)) {
    throw new Error('Volumen inválido');
  }
  if (volumenMl > MAX_REGADO_ML) {
    throw new Error(`El máximo seguro es ${MAX_REGADO_ML} mL por riego manual.`);
  }
  const dispositivo = simulador.dispositivo();
  if (!dispositivo.online) {
    throw new Error('RPi Offline: imposible accionar la bomba sin conexión.');
  }

  await espera(400);

  const planta = PLANTAS_SEMILLA.find((p) => p.id === plantaId) ?? PLANTAS_SEMILLA[0];
  const antes = simulador.lecturas().humedadSuelo;
  const objetivo = Math.min(85, antes + (volumenMl / 1000) * 55);
  simulador.regarHasta(planta.id, objetivo);

  const riego: Riego = {
    id: nuevoIdRiego(),
    plantaId: planta.id,
    inicio: new Date().toISOString(),
    duracionSeg: Math.max(2, Math.round((volumenMl / 1000 / CAUDAL_LMIN) * 60)),
    volumenMl,
    origen: 'manual',
    humedadAntes: antes,
    humedadDespues: parseFloat(objetivo.toFixed(1)),
    operadorNombre: nombreOperador,
  };
  const evento: EventoTimeline = {
    id: nuevoIdEvento(),
    tipo: 'riego',
    titulo: 'Riego Manual Aplicado',
    descripcion: `Dispensación de ${volumenMl} mL solicitada por ${nombreOperador} para ${planta.nombre}.`,
    fecha: riego.inicio,
    plantaId: planta.id,
    plantaNombre: planta.nombre,
    humedadAntes: antes,
    humedadDespues: riego.humedadDespues,
    volumenMl,
    origen: 'manual',
  };
  simulador.marcarFinRiego(riego, evento);
  simulador.registrarUltimoRiego(riego);
  return riego;
}

export async function alternarBomba(activa: boolean): Promise<EstadoDispositivo> {
  await espera(250);
  if (activa) {
    simulador.regarHasta('albahaca-1', UMBRALES_SEMILLA.humedadObjetivoPct);
  } else {
    simulador.detenerBomba();
  }
  return simulador.dispositivo();
}

export async function alternarVentilador(activa: boolean): Promise<EstadoDispositivo> {
  await espera(250);
  simulador.establecerVentilador(activa);
  return simulador.dispositivo();
}

export async function alternarLeds(activa: boolean): Promise<EstadoDispositivo> {
  await espera(250);
  simulador.establecerLeds(activa);
  return simulador.dispositivo();
}

export async function alternarValvula(activa: boolean): Promise<EstadoDispositivo> {
  await espera(250);
  simulador.establecerValvula(activa);
  return simulador.dispositivo();
}

export async function establecerModoAuto(modo: boolean): Promise<EstadoDispositivo> {
  await espera(200);
  simulador.establecerModoAuto(modo);
  return simulador.dispositivo();
}

export async function paradaEmergencia(): Promise<EstadoDispositivo> {
  await espera(300);
  simulador.paradaEmergencia();
  return simulador.dispositivo();
}

export async function establecerDispositivoEnLinea(online: boolean): Promise<EstadoDispositivo> {
  await espera(150);
  simulador.dispositivoEnLinea(online);
  return simulador.dispositivo();
}

export async function historialPorPlanta(
  plantaId: string,
): Promise<{ dia: string; humedad: number; temperatura: number; luz: number; aguaMl: number }[]> {
  await espera(300);
  const dias: { dia: string; humedad: number; temperatura: number; luz: number; aguaMl: number }[] = [];
  const planta = PLANTAS_SEMILLA.find((p) => p.id === plantaId) ?? PLANTAS_SEMILLA[0];
  for (let i = 13; i >= 0; i--) {
    const fecha = new Date(Date.now() - i * 24 * 60 * 60 * 1000);
    const media = (planta.humedadOptima[0] + planta.humedadOptima[1]) / 2;
    dias.push({
      dia: fecha.toISOString().slice(0, 10),
      humedad: Number((media + Math.sin(i / 2) * 8 + (i % 3) * 1.4).toFixed(1)),
      temperatura: Number((25 + Math.sin(i / 3) * 2.2 + (i % 4) * 0.3).toFixed(1)),
      luz: Math.round(11000 + Math.cos(i) * 1800 + (i % 5) * 210),
      aguaMl: Math.round(250 + Math.sin(i / 1.6) * 120 + (i % 4) * 45),
    });
  }
  return dias;
}

export async function prediccion(plantaId: string): Promise<{
  proximoRiegoEstimado: string;
  volumenSugeridoMl: number;
  tendencia: 'creciente' | 'estable' | 'decreciente';
  resumen: string;
}> {
  await espera(200);
  const planta = PLANTAS_SEMILLA.find((p) => p.id === plantaId) ?? PLANTAS_SEMILLA[0];
  const humedad = simulador.lecturas().humedadSuelo;
  const objetivo = planta.humedadOptima[0];
  const horasRestantes = Math.max(0.5, ((humedad - objetivo) / 0.14) / 60); // ~0.14%/min de caída
  const fechaRiego = new Date(Date.now() + horasRestantes * 60 * 60 * 1000);
  return {
    proximoRiegoEstimado: fechaRiego.toISOString(),
    volumenSugeridoMl: planta.riegoSugeridoMl,
    tendencia: 'creciente',
    resumen: 'Crecimiento óptimo · Estomas hidratados',
  };
}

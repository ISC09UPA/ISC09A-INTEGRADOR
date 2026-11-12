import { PLANTAS_SEMILLA } from '@/mocks/datos';
import type { EventoTimeline, OrigenRiego, Riego } from '../types';

export interface LecturaApi {
  id: number;
  sensorId: string;
  tipo: string;
  valor: number;
  unidad: string;
  invernaderoId: number | null;
  fechaHora: string;
}

export interface RiegoApi {
  id: number;
  invernaderoId: number | null;
  zona: string | null;
  fechaHoraInicio: string;
  duracionSegundos: number;
  volumenMl: number | null;
  origen: OrigenRiego;
  humedadDetectada: number | null;
  umbral: number | null;
}

export interface LecturasActualesApi {
  humedadSuelo?: number;
  temperatura?: number;
  humedadAmbiente?: number;
  luz?: number;
  nivelDeposito?: number;
  caudalLmin?: number;
  flotadorVacio?: boolean;
  timestamp?: string;
}

const INVERNADERO_ID = Number.parseInt(process.env.EXPO_PUBLIC_INVERNADERO_ID ?? '1', 10);

function masReciente(lectura: LecturaApi, anterior?: LecturaApi): boolean {
  return !anterior || Date.parse(lectura.fechaHora) > Date.parse(anterior.fechaHora);
}

export function mapearLecturasActuales(lecturas: LecturaApi[]): LecturasActualesApi {
  const ultimas = new Map<string, LecturaApi>();
  for (const lectura of lecturas) {
    const tipo = lectura.tipo.trim().toLowerCase();
    const anterior = ultimas.get(tipo);
    if (masReciente(lectura, anterior)) ultimas.set(tipo, lectura);
  }

  const resultado: LecturasActualesApi = {};
  const mapearNumero = (tipo: string, asignar: (valor: number) => void) => {
    const lectura = ultimas.get(tipo);
    if (lectura) asignar(lectura.valor);
  };

  mapearNumero('humedad_suelo', (valor) => { resultado.humedadSuelo = valor; });
  mapearNumero('temperatura', (valor) => { resultado.temperatura = valor; });
  mapearNumero('humedad_aire', (valor) => { resultado.humedadAmbiente = valor; });
  mapearNumero('luz', (valor) => { resultado.luz = valor; });
  mapearNumero('nivel_deposito', (valor) => { resultado.nivelDeposito = valor; });
  mapearNumero('caudal', (valor) => { resultado.caudalLmin = valor; });

  const flotador = ultimas.get('flotador_vacio');
  if (flotador) resultado.flotadorVacio = flotador.valor !== 0;

  const fechas = [...ultimas.values()].map((lectura) => lectura.fechaHora);
  const fechaMasReciente = fechas.sort(
    (a, b) => Date.parse(b) - Date.parse(a),
  )[0];
  if (fechaMasReciente) resultado.timestamp = fechaMasReciente;

  return resultado;
}

export function mapearRiegoApi(riego: RiegoApi): Riego {
  const planta = PLANTAS_SEMILLA.find(
    (item) =>
      item.id === riego.zona ||
      item.nombre === riego.zona ||
      item.sector === riego.zona,
  );

  return {
    id: String(riego.id),
    plantaId: planta?.id ?? String(riego.invernaderoId ?? INVERNADERO_ID),
    inicio: riego.fechaHoraInicio,
    duracionSeg: riego.duracionSegundos,
    volumenMl: riego.volumenMl ?? 0,
    origen: riego.origen,
    humedadAntes: riego.humedadDetectada ?? undefined,
  };
}

export function mapearRiegoTimeline(riego: Riego): EventoTimeline {
  const planta = PLANTAS_SEMILLA.find((item) => item.id === riego.plantaId);

  return {
    id: riego.id,
    tipo: 'riego',
    titulo: `Riego ${riego.origen}`,
    descripcion: `Riego registrado de ${riego.volumenMl} mL en ${riego.plantaId}.`,
    fecha: riego.inicio,
    plantaId: riego.plantaId,
    plantaNombre: planta?.nombre ?? riego.plantaId,
    humedadAntes: riego.humedadAntes,
    volumenMl: riego.volumenMl,
    origen: riego.origen,
  };
}

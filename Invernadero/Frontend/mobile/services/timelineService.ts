import { espera } from './bd';
import { simulador } from '@/mocks/simulador';
import { eventoACsv } from '../utils/csv';
import { EventoTimeline, TipoEventoTimeline } from '../types';
import { backendApiEnabled } from './backendApi';
import { obtenerRiegosBackend } from './telemetriaService';
import { mapearRiegoTimeline } from './telemetriaMapping';

const TAMAÑO_PAGINA = 10;

export interface FiltrosTimeline {
  tipo?: TipoEventoTimeline | 'todo';
  plantaId?: string | 'todas';
  pagina?: number;
}

export async function listarEventos(filtros: FiltrosTimeline = {}): Promise<{
  eventos: EventoTimeline[];
  totalFiltrado: number;
  hayMas: boolean;
  siguientePagina: number | null;
}> {
  if (backendApiEnabled) {
    const riegos = await obtenerRiegosBackend();
    const todos: EventoTimeline[] = riegos.map(mapearRiegoTimeline);
    const filtrados = todos.filter((evento) => {
      const cumpleTipo = !filtros.tipo || filtros.tipo === 'todo' || filtros.tipo === 'riego';
      const cumplePlanta =
        !filtros.plantaId ||
        filtros.plantaId === 'todas' ||
        evento.plantaId === filtros.plantaId;
      return cumpleTipo && cumplePlanta;
    });
    const pagina = filtros.pagina ?? 0;
    const inicio = pagina * TAMAÑO_PAGINA;
    const eventos = filtrados.slice(inicio, inicio + TAMAÑO_PAGINA);
    const hayMas = filtrados.length > inicio + eventos.length;

    return {
      eventos,
      totalFiltrado: filtrados.length,
      hayMas,
      siguientePagina: hayMas ? pagina + 1 : null,
    };
  }

  await espera(filtros.pagina ? 350 : 250);
  const todos = simulador.todosLosEventos().sort((a, b) => b.fecha.localeCompare(a.fecha));
  const filtrados = todos.filter((e) => {
    const cumpleTipo = !filtros.tipo || filtros.tipo === 'todo' || e.tipo === filtros.tipo;
    const cumplePlanta =
      !filtros.plantaId || filtros.plantaId === 'todas' || e.plantaId === filtros.plantaId;
    return cumpleTipo && cumplePlanta;
  });
  const pagina = filtros.pagina ?? 0;
  const inicio = pagina * TAMAÑO_PAGINA;
  const eventos = filtrados.slice(inicio, inicio + TAMAÑO_PAGINA);
  const hayMas = filtrados.length > inicio + eventos.length;
  return {
    eventos,
    totalFiltrado: filtrados.length,
    hayMas,
    siguientePagina: hayMas ? pagina + 1 : null,
  };
}

export async function exportarCsv(): Promise<string> {
  if (backendApiEnabled) {
    const riegos = await obtenerRiegosBackend();
    const encabezado = 'inicio,zona,origen,duracionSeg,volumenMl,humedadDetectada';
    const filas = riegos.map((riego) =>
      [
        riego.inicio,
        riego.plantaId,
        riego.origen,
        riego.duracionSeg,
        riego.volumenMl,
        riego.humedadAntes ?? '',
      ]
        .map((valor) => {
          const texto = String(valor);
          return /[",\r\n]/.test(texto) ? `"${texto.replace(/"/g, '""')}"` : texto;
        })
        .join(','),
    );

    return [encabezado, ...filas].join('\r\n');
  }

  const todos = simulador.todosLosEventos().sort((a, b) => b.fecha.localeCompare(a.fecha));
  return eventoACsv(todos);
}

export async function limpiarHistorial(): Promise<void> {
  if (backendApiEnabled) {
    throw new Error('No se puede borrar el historial del Backend desde la app.');
  }

  await espera(300);
  simulador.limpiarHistorial();
}

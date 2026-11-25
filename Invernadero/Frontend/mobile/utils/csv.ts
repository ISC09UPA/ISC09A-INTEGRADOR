
import type { EventoTimeline } from '../types';

// Neutraliza valores que podrían ejecutarse como fórmulas en Excel o Sheets.
const PREFIJO_FORMULA = /^[=+\-@\t\r]/;

type ValorCsv = string | number | boolean | null | undefined;

/** Escapa un valor CSV y neutraliza la inyección de fórmulas. */
export function escapar(valor: ValorCsv): string {
  if (valor === undefined || valor === null) return '';

  let texto = String(valor);

  // Solo se neutralizan textos, no valores numéricos negativos.
  if (typeof valor === 'string' && PREFIJO_FORMULA.test(texto)) {
    texto = `'${texto}`;
  }

  return /[",\r\n]/.test(texto)
    ? `"${texto.replace(/"/g, '""')}"`
    : texto;
}

export function filaCsv(valores: ValorCsv[]): string {
  return valores.map(escapar).join(',');
}

const SALTO = '\r\n';

export function eventoACsv(eventos: EventoTimeline[]): string {
  const encabezado = filaCsv([
    'id',
    'tipo',
    'titulo',
    'descripcion',
    'fecha',
    'planta',
    'volumenMl',
    'humedadAntes',
    'humedadDespues',
    'nivelDepositoPct',
    'certezaPct',
  ]);

  const filas = eventos.map((e) =>
    filaCsv([
      e.id,
      e.tipo,
      e.titulo,
      e.descripcion,
      e.fecha,
      e.plantaNombre,
      e.volumenMl,
      e.humedadAntes,
      e.humedadDespues,
      e.nivelDepositoPct,
      e.certezaVigorPct,
    ]),
  );

  return [
    'RiegoSmart IA - Historial de eventos',
    encabezado,
    ...filas,
  ].join(SALTO);
}

export interface FilaRiego {
  inicio: string;
  zona: string;
  origen: string;
  duracionSeg: number;
  volumenMl: number;
  humedadDetectada?: number;
}

export function riegosACsv(riegos: FilaRiego[]): string {
  const encabezado = filaCsv([
    'inicio',
    'zona',
    'origen',
    'duracionSeg',
    'volumenMl',
    'humedadDetectada',
  ]);

  const filas = riegos.map((r) =>
    filaCsv([
      r.inicio,
      r.zona,
      r.origen,
      r.duracionSeg,
      r.volumenMl,
      r.humedadDetectada,
    ]),
  );

  return [encabezado, ...filas].join(SALTO);
}

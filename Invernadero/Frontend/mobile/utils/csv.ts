import { EventoTimeline } from "../types";


function escapar(valor: string | number | undefined): string {
  const texto = valor === undefined || valor === null ? '' : String(valor);
  if (/[",\n]/.test(texto)) return `"${texto.replace(/"/g, '""')}"`;
  return texto;
}

export function eventoACsv(eventos: EventoTimeline[]): string {
  const encabezado = [
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
  ].join(',');

  const filas = eventos.map((e) =>
    [
      escapar(e.id),
      escapar(e.tipo),
      escapar(e.titulo),
      escapar(e.descripcion),
      escapar(e.fecha),
      escapar(e.plantaNombre),
      escapar(e.volumenMl),
      escapar(e.humedadAntes),
      escapar(e.humedadDespues),
      escapar(e.nivelDepositoPct),
      escapar(e.certezaVigorPct),
    ].join(','),
  );

  return ['RiegoSmart IA - Historial de eventos', encabezado, ...filas].join('\n');
}

export interface FilaRiego {
  inicio: string;
  origen: string;
  duracionSeg: number;
  volumenMl: number;
}

export function riegosACsv(riegos: FilaRiego[]): string {
  const encabezado = 'inicio,origen,duracionSeg,volumenMl';
  return [encabezado, ...riegos.map((r) => [r.inicio, r.origen, r.duracionSeg, r.volumenMl].join(','))].join('\n');
}

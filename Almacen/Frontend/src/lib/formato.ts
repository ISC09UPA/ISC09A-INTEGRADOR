import type { EstadoAprobacion, EstadoDocumento, TipoDocumento, TipoMovimientoInventario } from '@/types/enums';

// Cómo se muestran importes, cantidades, fechas y estados (igual que Components/Comunes/Formato.cs).

const dinero = new Intl.NumberFormat('es-MX', { style: 'currency', currency: 'MXN', minimumFractionDigits: 2, maximumFractionDigits: 2 });
const costo4 = new Intl.NumberFormat('es-MX', { style: 'currency', currency: 'MXN', minimumFractionDigits: 4, maximumFractionDigits: 4 });
const entero = new Intl.NumberFormat('es-MX', { maximumFractionDigits: 0 });
const decimal3 = new Intl.NumberFormat('es-MX', { minimumFractionDigits: 3, maximumFractionDigits: 3 });
const MESES = ['ene', 'feb', 'mar', 'abr', 'may', 'jun', 'jul', 'ago', 'sep', 'oct', 'nov', 'dic'];

export const Formato = {
  dinero: (v: number) => dinero.format(v ?? 0),
  cantidad: (v: number) => (Number.isInteger(v) ? entero.format(v) : decimal3.format(v)),
  costo: (v: number) => (Math.round(v * 100) / 100 === v ? dinero.format(v) : costo4.format(v)),
  porcentaje: (fraccion: number) => `${Math.round(fraccion * 10000) / 100} %`,
  numero: (v: number) => entero.format(v),

  /** "2026-10-22" → "22 oct 2026", sin pasar por zonas horarias. */
  fecha: (iso: string) => {
    const [a, m, d] = iso.slice(0, 10).split('-').map(Number);
    return `${String(d).padStart(2, '0')} ${MESES[m - 1]} ${a}`;
  },

  /** Fecha y hora en la zona de la Ciudad de México, como la web. */
  fechaHora: (iso: string) => {
    const f = new Date(iso);
    const partes = new Intl.DateTimeFormat('es-MX', {
      timeZone: 'America/Mexico_City',
      day: '2-digit',
      month: 'short',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      hour12: false,
    }).formatToParts(f);
    const p = (t: string) => partes.find((x) => x.type === t)?.value ?? '';
    return `${p('day')} ${p('month').replace('.', '')} ${p('year')} ${p('hour')}:${p('minute')}`;
  },

  folio: (prefijo: string, folio: number) => `${prefijo}-${String(folio).padStart(5, '0')}`,

  movimiento: (tipo: TipoMovimientoInventario) =>
    ({
      Inicial: 'Stock inicial',
      Recepcion: 'Recepción',
      Venta: 'Venta',
      TraspasoSalida: 'Traspaso (salida)',
      TraspasoEntrada: 'Traspaso (entrada)',
      Ajuste: 'Ajuste',
      CancelacionRecepcion: 'Cancelación de recepción',
      CancelacionVenta: 'Cancelación de venta',
    })[tipo] ?? tipo,

  prefijo: (tipo: TipoDocumento) => (tipo === 'Recepcion' ? 'REC' : tipo === 'Venta' ? 'VTA' : 'TRA'),

  ruta: (tipo: TipoDocumento, id: string) =>
    (tipo === 'Recepcion' ? `/recepciones/${id}` : tipo === 'Venta' ? `/ventas/${id}` : `/traspasos/${id}`) as `/${string}`,
};

export type ColorEstado = 'success' | 'error' | 'warning' | 'default';

export function colorEstadoDocumento(estado: EstadoDocumento): ColorEstado {
  return estado === 'Confirmado' ? 'success' : estado === 'Cancelado' ? 'error' : 'default';
}

export function colorEstadoAprobacion(estado: EstadoAprobacion): ColorEstado {
  return estado === 'Aprobado' ? 'success' : estado === 'Rechazado' ? 'error' : 'warning';
}

export const TasasIva = [
  { valor: 0.16, texto: '16 %' },
  { valor: 0.08, texto: '8 % (frontera)' },
  { valor: 0, texto: '0 %' },
];

/** Importe de una línea redondeado a centavos, como Importe.DeLinea del dominio. */
export function importeLinea(cantidad: number, precio: number, tasa: number) {
  if (!(cantidad > 0) || !(precio >= 0)) return { subtotal: 0, iva: 0, total: 0 };
  const subtotal = redondear(cantidad * precio);
  const iva = redondear(subtotal * tasa);
  return { subtotal, iva, total: subtotal + iva };
}

export function redondear(v: number) {
  return Math.round((v + Number.EPSILON) * 100) / 100;
}

/** Acepta "1,5" o "1.5"; vacío o inválido → NaN. */
export function leerNumero(texto: string) {
  const limpio = texto.trim().replace(',', '.');
  return limpio === '' ? NaN : Number(limpio);
}

// Fechas como texto "yyyy-MM-dd" (DateOnly en la API), calculadas en hora de México.
export const Fechas = {
  hoy: () => new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Mexico_City' }).format(new Date()),
  inicioMes: () => `${Fechas.hoy().slice(0, 8)}01`,
  inicioAnio: () => `${Fechas.hoy().slice(0, 4)}-01-01`,
  mesAnterior: () => {
    const [a, m] = Fechas.hoy().split('-').map(Number);
    const anio = m === 1 ? a - 1 : a;
    const mes = m === 1 ? 12 : m - 1;
    const ultimo = new Date(Date.UTC(anio, mes, 0)).getUTCDate();
    const mm = String(mes).padStart(2, '0');
    return { desde: `${anio}-${mm}-01`, hasta: `${anio}-${mm}-${ultimo}` };
  },
  restarDias: (dias: number) => {
    const [a, m, d] = Fechas.hoy().split('-').map(Number);
    return new Date(Date.UTC(a, m - 1, d - dias)).toISOString().slice(0, 10);
  },
  valida: (texto: string) => /^\d{4}-\d{2}-\d{2}$/.test(texto) && !Number.isNaN(Date.parse(texto)),
};

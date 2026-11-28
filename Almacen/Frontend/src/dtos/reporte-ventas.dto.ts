import type { PuntoDto } from '@/dtos/punto.dto';

/** Reporte de ventas del periodo. Contrato de la API. */
export interface ReporteVentasDto {
  numeroVentas: number;
  subtotal: number;
  iva: number;
  total: number;
  ticketPromedio: number;
  porPeriodo: PuntoDto[];
  topClientes: PuntoDto[];
  topProductos: PuntoDto[];
  porAlmacen: PuntoDto[];
}

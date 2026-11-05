import { HttpClient } from '@/api/http-client';
import type { MargenProductoDto } from '@/dtos/margen-producto.dto';
import type { ReporteComprasDto } from '@/dtos/reporte-compras.dto';
import type { ReporteFinancieroDto } from '@/dtos/reporte-financiero.dto';
import type { ReporteInventarioDto } from '@/dtos/reporte-inventario.dto';
import type { ReporteVentasDto } from '@/dtos/reporte-ventas.dto';
import { ReporteMapper } from '@/mappers/reporte.mapper';
import type { MargenProducto } from '@/models/margen-producto.model';
import type { ReporteCompras } from '@/models/reporte-compras.model';
import type { ReporteFinanciero } from '@/models/reporte-financiero.model';
import type { ReporteInventario } from '@/models/reporte-inventario.model';
import type { ReporteVentas } from '@/models/reporte-ventas.model';

/** Periodo en formato yyyy-MM-dd. */
export interface Periodo {
  desde: string;
  hasta: string;
}

export class ReportesService {
  constructor(private readonly http: HttpClient) {}

  async ventas(p: Periodo): Promise<ReporteVentas> {
    return ReporteMapper.ventas(await this.http.get<ReporteVentasDto>('/api/reportes/ventas', { ...p }));
  }

  async compras(p: Periodo): Promise<ReporteCompras> {
    return ReporteMapper.compras(await this.http.get<ReporteComprasDto>('/api/reportes/compras', { ...p }));
  }

  async financiero(p: Periodo): Promise<ReporteFinanciero> {
    return ReporteMapper.financiero(await this.http.get<ReporteFinancieroDto>('/api/reportes/financiero', { ...p }));
  }

  /** Foto de hoy: no depende del periodo. */
  async inventario(): Promise<ReporteInventario> {
    return ReporteMapper.inventario(await this.http.get<ReporteInventarioDto>('/api/reportes/inventario'));
  }

  async margen(p: Periodo): Promise<MargenProducto[]> {
    return (await this.http.get<MargenProductoDto[]>('/api/reportes/margen', { ...p })).map(ReporteMapper.margen);
  }
}

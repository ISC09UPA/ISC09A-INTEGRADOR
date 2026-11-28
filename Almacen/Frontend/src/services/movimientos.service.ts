import { HttpClient, type Parametros } from '@/api/http-client';
import type { ReporteMovimientosDto } from '@/dtos/reporte-movimientos.dto';
import { MovimientoMapper } from '@/mappers/movimiento.mapper';
import type { ReporteMovimientos } from '@/models/reporte-movimientos.model';
import type { TipoMovimientoInventario } from '@/types/enums';

export interface FiltroMovimientos {
  desde: string;
  hasta: string;
  almacenId?: string;
  productoId?: string;
  tipo?: TipoMovimientoInventario;
  busqueda?: string;
}

/** Reporte de movimientos de inventario (JSON); el PDF y el CSV los abre ArchivosService. */
export class MovimientosService {
  constructor(private readonly http: HttpClient) {}

  async reporte(filtro: FiltroMovimientos, pagina = 1, tamano = 50): Promise<ReporteMovimientos> {
    const dto = await this.http.get<ReporteMovimientosDto>('/api/movimientos', { ...MovimientosService.parametros(filtro), pagina, tamano });
    return MovimientoMapper.reporte(dto);
  }

  /** Nombres que espera la API (almacen, producto) a partir del filtro de la app. */
  static parametros(filtro: FiltroMovimientos): Parametros {
    return { desde: filtro.desde, hasta: filtro.hasta, almacen: filtro.almacenId, producto: filtro.productoId, tipo: filtro.tipo, busqueda: filtro.busqueda };
  }
}

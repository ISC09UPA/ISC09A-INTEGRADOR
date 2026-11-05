import { HttpClient } from '@/api/http-client';
import type { ReporteMensualDto } from '@/dtos/reporte-mensual.dto';
import type { ResumenFinancieroDto } from '@/dtos/resumen-financiero.dto';
import { FinanzasMapper } from '@/mappers/finanzas.mapper';
import type { ReporteMensual } from '@/models/reporte-mensual.model';
import type { ResumenFinanciero } from '@/models/resumen-financiero.model';
import type { NaturalezaFinanciera } from '@/types/enums';

export interface FiltroFinanzas {
  desde?: string;
  hasta?: string;
  naturaleza?: NaturalezaFinanciera;
  pagina?: number;
  tamano?: number;
}

export class FinanzasService {
  constructor(private readonly http: HttpClient) {}

  async movimientos(filtro: FiltroFinanzas): Promise<ResumenFinanciero> {
    return FinanzasMapper.resumen(await this.http.get<ResumenFinancieroDto>('/api/finanzas/movimientos', { ...filtro }));
  }

  /** Debe / Haber por mes del año dado y los dos anteriores. */
  async reporteMensual(anio: number): Promise<ReporteMensual> {
    return FinanzasMapper.reporteMensual(await this.http.get<ReporteMensualDto>('/api/finanzas/mensual', { anio }));
  }
}

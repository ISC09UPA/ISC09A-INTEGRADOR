import { HttpClient } from '@/api/http-client';
import type { ConsultaStockIotDto } from '@/dtos/consulta-stock-iot.dto';
import type { PaginaDto } from '@/dtos/pagina.dto';
import type { SemaforoStockDto } from '@/dtos/semaforo-stock.dto';
import { ComunMapper } from '@/mappers/comun.mapper';
import { StockMapper } from '@/mappers/stock.mapper';
import type { ConsultaStockIot } from '@/models/consulta-stock-iot.model';
import type { Pagina } from '@/models/pagina.model';
import type { SemaforoStock } from '@/models/semaforo-stock.model';
import type { NivelStock } from '@/types/enums';

export interface FiltroSemaforo {
  busqueda?: string;
  nivel?: NivelStock;
  pagina?: number;
  tamano?: number;
}

/** Comprobar stock: semáforo por producto y bitácora de consultas hechas desde el lector del embebido. */
export class StockService {
  constructor(private readonly http: HttpClient) {}

  async semaforo(filtro: FiltroSemaforo): Promise<SemaforoStock> {
    return StockMapper.semaforo(
      await this.http.get<SemaforoStockDto>('/api/stock/semaforo', { busqueda: filtro.busqueda, nivel: filtro.nivel, pagina: filtro.pagina, tamano: filtro.tamano ?? 30 }),
    );
  }

  async consultasLector(pagina = 1, tamano = 30): Promise<Pagina<ConsultaStockIot>> {
    return ComunMapper.pagina(await this.http.get<PaginaDto<ConsultaStockIotDto>>('/api/iot/consultas-stock', { pagina, tamano }), StockMapper.consulta);
  }
}

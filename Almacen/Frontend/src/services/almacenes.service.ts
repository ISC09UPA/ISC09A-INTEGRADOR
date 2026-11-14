import { HttpClient } from '@/api/http-client';
import type { AlmacenDto } from '@/dtos/almacen.dto';
import type { ExistenciaDto } from '@/dtos/existencia.dto';
import type { KardexDto } from '@/dtos/kardex.dto';
import type { PaginaDto } from '@/dtos/pagina.dto';
import { AlmacenMapper } from '@/mappers/almacen.mapper';
import { ComunMapper } from '@/mappers/comun.mapper';
import type { Almacen } from '@/models/almacen.model';
import type { DatosAlmacen } from '@/models/datos-almacen.model';
import type { Existencia } from '@/models/existencia.model';
import type { MovimientoKardex } from '@/models/movimiento-kardex.model';
import type { Pagina } from '@/models/pagina.model';
import type { StockInicial } from '@/models/stock-inicial.model';

export interface FiltroExistencias {
  almacenId?: string;
  busqueda?: string;
  soloBajoMinimo?: boolean;
  pagina?: number;
  tamano?: number;
}

/** Almacenes, existencias y kardex. */
export class AlmacenesService {
  constructor(private readonly http: HttpClient) {}

  async listar(inactivos = false): Promise<Almacen[]> {
    return (await this.http.get<AlmacenDto[]>('/api/almacenes', { inactivos })).map(AlmacenMapper.aModel);
  }

  async crear(datos: DatosAlmacen): Promise<Almacen> {
    return AlmacenMapper.aModel(await this.http.post<AlmacenDto>('/api/almacenes', AlmacenMapper.datos(datos)));
  }

  async editar(id: string, datos: DatosAlmacen): Promise<Almacen> {
    return AlmacenMapper.aModel(await this.http.put<AlmacenDto>(`/api/almacenes/${id}`, AlmacenMapper.datos(datos)));
  }

  async cambiarEstado(id: string, activo: boolean): Promise<Almacen> {
    return AlmacenMapper.aModel(await this.http.patch<AlmacenDto>(`/api/almacenes/${id}/estado`, { activo }));
  }

  async existencias(filtro: FiltroExistencias): Promise<Pagina<Existencia>> {
    const dto = await this.http.get<PaginaDto<ExistenciaDto>>('/api/almacenes/existencias', {
      almacen: filtro.almacenId,
      busqueda: filtro.busqueda,
      bajoMinimo: filtro.soloBajoMinimo,
      pagina: filtro.pagina,
      tamano: filtro.tamano ?? 50,
    });
    return ComunMapper.pagina(dto, AlmacenMapper.existencia);
  }

  async kardex(almacenId: string, productoId: string, desde?: string, hasta?: string): Promise<MovimientoKardex[]> {
    const dtos = await this.http.get<KardexDto[]>(`/api/almacenes/${almacenId}/kardex/${productoId}`, { desde, hasta });
    return dtos.map(AlmacenMapper.kardex);
  }

  /** 0 para no avisar. */
  definirMinimo(almacenId: string, productoId: string, minimo: number) {
    return this.http.put(`/api/almacenes/${almacenId}/existencias/${productoId}/minimo`, { minimo });
  }

  registrarStockInicial(almacenId: string, productoId: string, stock: StockInicial) {
    return this.http.post(`/api/almacenes/${almacenId}/existencias/${productoId}/inicial`, AlmacenMapper.stockInicial(stock));
  }
}

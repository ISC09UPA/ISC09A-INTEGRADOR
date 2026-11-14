import { HttpClient } from '@/api/http-client';
import type { OpcionDto } from '@/dtos/opcion.dto';
import type { PaginaDto } from '@/dtos/pagina.dto';
import type { TerceroDto } from '@/dtos/tercero.dto';
import { ComunMapper } from '@/mappers/comun.mapper';
import { TerceroMapper } from '@/mappers/tercero.mapper';
import type { DatosTercero } from '@/models/datos-tercero.model';
import type { Opcion } from '@/models/opcion.model';
import type { Pagina } from '@/models/pagina.model';
import type { Tercero } from '@/models/tercero.model';

export interface FiltroTerceros {
  busqueda?: string;
  inactivos?: boolean;
  pagina?: number;
  tamano?: number;
}

/** Clientes y proveedores tienen los mismos endpoints; cambia solo la ruta base. */
export abstract class TercerosService {
  protected constructor(
    protected readonly http: HttpClient,
    private readonly ruta: string,
  ) {}

  async listar(filtro: FiltroTerceros): Promise<Pagina<Tercero>> {
    const dto = await this.http.get<PaginaDto<TerceroDto>>(this.ruta, { ...filtro, tamano: filtro.tamano ?? 20 });
    return ComunMapper.pagina(dto, TerceroMapper.aModel);
  }

  /** Activos, para elegir en un documento. */
  async activos(texto?: string): Promise<Opcion[]> {
    const dtos = await this.http.get<OpcionDto[]>(`${this.ruta}/activos`, { texto });
    return dtos.map(ComunMapper.opcion);
  }

  async obtener(id: string): Promise<Tercero> {
    return TerceroMapper.aModel(await this.http.get<TerceroDto>(`${this.ruta}/${id}`));
  }

  async crear(datos: DatosTercero): Promise<Tercero> {
    return TerceroMapper.aModel(await this.http.post<TerceroDto>(this.ruta, TerceroMapper.datos(datos)));
  }

  async editar(id: string, datos: DatosTercero): Promise<Tercero> {
    return TerceroMapper.aModel(await this.http.put<TerceroDto>(`${this.ruta}/${id}`, TerceroMapper.datos(datos)));
  }

  async cambiarEstado(id: string, activo: boolean): Promise<Tercero> {
    return TerceroMapper.aModel(await this.http.patch<TerceroDto>(`${this.ruta}/${id}/estado`, { activo }));
  }
}

export class ClientesService extends TercerosService {
  constructor(http: HttpClient) {
    super(http, '/api/clientes');
  }
}

export class ProveedoresService extends TercerosService {
  constructor(http: HttpClient) {
    super(http, '/api/proveedores');
  }
}

import { HttpClient } from '@/api/http-client';
import type { DocumentoDetalleDto } from '@/dtos/documento-detalle.dto';
import type { DocumentoRegistradoDto } from '@/dtos/documento-registrado.dto';
import type { DocumentoResumenDto } from '@/dtos/documento-resumen.dto';
import type { PaginaDto } from '@/dtos/pagina.dto';
import { ComunMapper } from '@/mappers/comun.mapper';
import { DocumentoMapper } from '@/mappers/documento.mapper';
import type { DocumentoDetalle } from '@/models/documento-detalle.model';
import type { DocumentoRegistrado } from '@/models/documento-registrado.model';
import type { DocumentoResumen } from '@/models/documento-resumen.model';
import type { NuevaRecepcion } from '@/models/nueva-recepcion.model';
import type { NuevaVenta } from '@/models/nueva-venta.model';
import type { NuevoTraspaso } from '@/models/nuevo-traspaso.model';
import type { Pagina } from '@/models/pagina.model';
import type { EstadoDocumento } from '@/types/enums';

export interface FiltroDocumentos {
  busqueda?: string;
  estado?: EstadoDocumento;
  desde?: string;
  hasta?: string;
  pagina?: number;
  tamano?: number;
}

/** Lista, detalle y cancelación: iguales para ventas, recepciones y traspasos. */
export abstract class DocumentosService {
  protected constructor(
    protected readonly http: HttpClient,
    protected readonly ruta: string,
  ) {}

  async listar(filtro: FiltroDocumentos): Promise<Pagina<DocumentoResumen>> {
    const dto = await this.http.get<PaginaDto<DocumentoResumenDto>>(this.ruta, { ...filtro, tamano: filtro.tamano ?? 20 });
    return ComunMapper.pagina(dto, DocumentoMapper.resumen);
  }

  async obtener(id: string): Promise<DocumentoDetalle> {
    return DocumentoMapper.detalle(await this.http.get<DocumentoDetalleDto>(`${this.ruta}/${id}`));
  }

  cancelar(id: string, motivo: string) {
    return this.http.post(`${this.ruta}/${id}/cancelar`, { texto: motivo });
  }

  protected async registrarDto(cuerpo: unknown): Promise<DocumentoRegistrado> {
    return DocumentoMapper.registrado(await this.http.post<DocumentoRegistradoDto>(this.ruta, cuerpo));
  }
}

export class VentasService extends DocumentosService {
  constructor(http: HttpClient) {
    super(http, '/api/ventas');
  }

  registrar(venta: NuevaVenta) {
    return this.registrarDto(DocumentoMapper.nuevaVenta(venta));
  }
}

export class RecepcionesService extends DocumentosService {
  constructor(http: HttpClient) {
    super(http, '/api/recepciones');
  }

  registrar(recepcion: NuevaRecepcion) {
    return this.registrarDto(DocumentoMapper.nuevaRecepcion(recepcion));
  }
}

export class TraspasosService extends DocumentosService {
  constructor(http: HttpClient) {
    super(http, '/api/traspasos');
  }

  registrar(traspaso: NuevoTraspaso) {
    return this.registrarDto(DocumentoMapper.nuevoTraspaso(traspaso));
  }
}

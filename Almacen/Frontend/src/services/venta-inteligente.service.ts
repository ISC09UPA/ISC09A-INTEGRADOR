import { HttpClient } from '@/api/http-client';
import type { EscaneoDto, SesionVentaDto } from '@/dtos/venta-inteligente.dto';
import { ProductoMapper } from '@/mappers/producto.mapper';
import type { Escaneo, SesionVenta } from '@/models/venta-inteligente.model';

const sesion = (dto: SesionVentaDto): SesionVenta => ({ usuario: dto.usuario, almacenId: dto.almacenId, almacen: dto.almacen, esMia: dto.esMia });

/**
 * Modo inteligente de venta: mientras el usuario tiene el lector NFC, cada producto que pasa por él llega como escaneo.
 * Hay un solo lector: tomar falla con 409 ("lo está usando X") si otro lo tiene vigente.
 */
export class VentaInteligenteService {
  constructor(private readonly http: HttpClient) {}

  /** Quién tiene el lector; null si está libre. */
  async obtener(): Promise<SesionVenta | null> {
    const dto = await this.http.get<SesionVentaDto | undefined>('/api/iot/venta');
    return dto ? sesion(dto) : null;
  }

  async tomar(almacenId: string): Promise<SesionVenta> {
    return sesion(await this.http.post<SesionVentaDto>('/api/iot/venta/tomar', { almacenId }));
  }

  /** Cada ~15 s; falla si el candado venció y lo tomó otro usuario. */
  async latido(): Promise<SesionVenta> {
    return sesion(await this.http.post<SesionVentaDto>('/api/iot/venta/latido'));
  }

  liberar() {
    return this.http.post('/api/iot/venta/liberar');
  }

  /** Escaneos nuevos; cada uno se entrega una sola vez. */
  async escaneos(): Promise<Escaneo[]> {
    return (await this.http.post<EscaneoDto[]>('/api/iot/venta/escaneos')).map((e) => ({ id: e.id, producto: ProductoMapper.opcion(e.producto) }));
  }
}

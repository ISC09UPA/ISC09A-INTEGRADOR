import { HttpClient } from '@/api/http-client';
import type { EscaneoDto } from '@/dtos/escaneo.dto';
import type { SesionVentaDto } from '@/dtos/sesion-venta.dto';
import { IotMapper } from '@/mappers/iot.mapper';
import type { Escaneo } from '@/models/escaneo.model';
import type { SesionVenta } from '@/models/sesion-venta.model';

/**
 * Modo inteligente de venta: el lector NFC del embebido (Raspberry) manda cada pieza por Service Bus y la API la
 * guarda para quien tenga el candado. Solo un usuario a la vez; el candado vence si deja de mandar latidos.
 */
export class IotService {
  constructor(private readonly http: HttpClient) {}

  /** Quién lo tiene ahora; undefined si está libre (204). */
  async sesionVenta(): Promise<SesionVenta | undefined> {
    const dto = await this.http.get<SesionVentaDto | undefined>('/api/iot/venta');
    return dto ? IotMapper.sesion(dto) : undefined;
  }

  /** Toma el lector con un almacén por defecto. Falla (409) si otro usuario lo tiene vigente. */
  async tomarModo(almacenId: string): Promise<SesionVenta> {
    return IotMapper.sesion(await this.http.post<SesionVentaDto>('/api/iot/venta/tomar', { almacenId }));
  }

  /** Cada ~15 s. Falla si el candado venció o lo tomó otro usuario. */
  latido() {
    return this.http.post<SesionVentaDto>('/api/iot/venta/latido');
  }

  liberarModo() {
    return this.http.post('/api/iot/venta/liberar');
  }

  /** Piezas nuevas desde la última consulta; cada una se entrega una sola vez. */
  async escaneos(): Promise<Escaneo[]> {
    return (await this.http.post<EscaneoDto[]>('/api/iot/venta/escaneos')).map(IotMapper.escaneo);
  }
}

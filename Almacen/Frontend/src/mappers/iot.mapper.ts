import type { EscaneoDto } from '@/dtos/escaneo.dto';
import type { SesionVentaDto } from '@/dtos/sesion-venta.dto';
import type { Escaneo } from '@/models/escaneo.model';
import type { SesionVenta } from '@/models/sesion-venta.model';

import { ComunMapper } from './comun.mapper';
import { ProductoMapper } from './producto.mapper';

const { opcional } = ComunMapper;

export class IotMapper {
  static sesion(dto: SesionVentaDto): SesionVenta {
    return { usuario: dto.usuario, almacenId: dto.almacenId, almacen: dto.almacen, esMia: dto.esMia };
  }

  static escaneo(dto: EscaneoDto): Escaneo {
    return {
      id: dto.id,
      uid: dto.uid,
      familia: dto.familia,
      producto: dto.producto ? ProductoMapper.opcion(dto.producto) : undefined,
      rechazo: opcional(dto.rechazo),
      duplicado: dto.duplicado ?? false,
    };
  }
}

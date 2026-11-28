import type { ConsultaStockIotDto } from '@/dtos/consulta-stock-iot.dto';
import type { SemaforoProductoDto, SemaforoStockDto } from '@/dtos/semaforo-stock.dto';
import type { ConsultaStockIot } from '@/models/consulta-stock-iot.model';
import type { SemaforoProducto, SemaforoStock } from '@/models/semaforo-stock.model';

import { ComunMapper } from './comun.mapper';

const { opcional } = ComunMapper;

export class StockMapper {
  static producto(dto: SemaforoProductoDto): SemaforoProducto {
    return {
      productoId: dto.productoId,
      referencia: dto.referencia,
      descripcion: dto.descripcion,
      unidad: dto.unidad,
      familiaNfc: opcional(dto.familiaNfc),
      existencia: dto.existencia,
      minimo: dto.minimo,
      nivel: dto.nivel,
      almacenes: dto.almacenes.map((a) => ({ almacenId: a.almacenId, almacen: a.almacen, cantidad: a.cantidad, minimo: a.minimo, nivel: a.nivel })),
    };
  }

  static semaforo(dto: SemaforoStockDto): SemaforoStock {
    return { productos: ComunMapper.pagina(dto.productos, StockMapper.producto), verdes: dto.verdes, amarillos: dto.amarillos, rojos: dto.rojos };
  }

  static consulta(dto: ConsultaStockIotDto): ConsultaStockIot {
    return {
      id: dto.id,
      fecha: dto.fecha,
      uid: dto.uid,
      familia: dto.familia,
      producto: opcional(dto.producto),
      existencia: opcional(dto.existencia),
      minimo: opcional(dto.minimo),
      nivel: opcional(dto.nivel),
      motivo: dto.motivo,
    };
  }
}

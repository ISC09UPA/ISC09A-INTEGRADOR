import type { MovimientoDto } from '@/dtos/movimiento.dto';
import type { ReporteMovimientosDto } from '@/dtos/reporte-movimientos.dto';
import type { ResumenProductoDto } from '@/dtos/resumen-producto.dto';
import type { MovimientoInventario } from '@/models/movimiento-inventario.model';
import type { ReporteMovimientos } from '@/models/reporte-movimientos.model';
import type { ResumenProducto } from '@/models/resumen-producto.model';

import { ComunMapper } from './comun.mapper';
import { DocumentoMapper } from './documento.mapper';

export class MovimientoMapper {
  static movimiento(dto: MovimientoDto): MovimientoInventario {
    return {
      id: dto.id,
      fecha: dto.fecha,
      almacen: dto.almacen,
      productoId: dto.productoId,
      referencia: dto.referencia,
      descripcion: dto.descripcion,
      unidad: dto.unidad,
      tipo: dto.tipo,
      cantidad: dto.cantidad,
      costoUnitario: dto.costoUnitario,
      saldo: dto.saldo,
      documento: DocumentoMapper.referencia(dto.documentoTipo, dto.documentoId, dto.folio),
      usuario: dto.usuario,
      valor: dto.valor,
    };
  }

  static resumenProducto(dto: ResumenProductoDto): ResumenProducto {
    return {
      almacen: dto.almacen,
      referencia: dto.referencia,
      descripcion: dto.descripcion,
      unidad: dto.unidad,
      inicial: dto.inicial,
      entradas: dto.entradas,
      salidas: dto.salidas,
      final: dto.final,
      valorEntradas: dto.valorEntradas,
      valorSalidas: dto.valorSalidas,
    };
  }

  static reporte(dto: ReporteMovimientosDto): ReporteMovimientos {
    return {
      movimientos: ComunMapper.pagina(dto.movimientos, MovimientoMapper.movimiento),
      numeroMovimientos: dto.numeroMovimientos,
      entradas: dto.entradas,
      salidas: dto.salidas,
      valorEntradas: dto.valorEntradas,
      valorSalidas: dto.valorSalidas,
      porProducto: dto.porProducto.map(MovimientoMapper.resumenProducto),
    };
  }
}

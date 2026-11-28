import type { AlmacenDto } from '@/dtos/almacen.dto';
import type { DatosAlmacenDto } from '@/dtos/datos-almacen.dto';
import type { ExistenciaDto } from '@/dtos/existencia.dto';
import type { KardexDto } from '@/dtos/kardex.dto';
import type { StockInicialDto } from '@/dtos/stock-inicial.dto';
import type { Almacen } from '@/models/almacen.model';
import type { DatosAlmacen } from '@/models/datos-almacen.model';
import type { Existencia } from '@/models/existencia.model';
import type { MovimientoKardex } from '@/models/movimiento-kardex.model';
import type { StockInicial } from '@/models/stock-inicial.model';

import { DocumentoMapper } from './documento.mapper';

export class AlmacenMapper {
  static aModel(dto: AlmacenDto): Almacen {
    return { id: dto.id, nombre: dto.nombre, ubicacion: dto.ubicacion, activo: dto.activo, productos: dto.productos, valorInventario: dto.valorInventario };
  }

  static datos(model: DatosAlmacen): DatosAlmacenDto {
    return { nombre: model.nombre.trim(), ubicacion: model.ubicacion.trim() };
  }

  static existencia(dto: ExistenciaDto): Existencia {
    return {
      almacenId: dto.almacenId,
      almacen: dto.almacen,
      productoId: dto.productoId,
      referencia: dto.referencia,
      descripcion: dto.descripcion,
      unidad: dto.unidad,
      permiteFracciones: dto.permiteFracciones,
      cantidad: dto.cantidad,
      stockMinimo: dto.stockMinimo,
      costoPromedio: dto.costoPromedio,
      valor: dto.valor,
      bajoMinimo: dto.bajoMinimo,
    };
  }

  static stockInicial(model: StockInicial): StockInicialDto {
    return { cantidad: model.cantidad, costoUnitario: model.costoUnitario };
  }

  static kardex(dto: KardexDto): MovimientoKardex {
    return {
      fecha: dto.fecha,
      tipo: dto.tipo,
      cantidad: dto.cantidad,
      costoUnitario: dto.costoUnitario,
      saldo: dto.saldo,
      costoPromedio: dto.costoPromedio,
      documento: DocumentoMapper.referencia(dto.documentoTipo, dto.documentoId, dto.folio),
      usuario: dto.usuario,
    };
  }
}

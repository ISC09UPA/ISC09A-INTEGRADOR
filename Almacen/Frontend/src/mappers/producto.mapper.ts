import type { CrearProductoDto } from '@/dtos/crear-producto.dto';
import type { DatosProductoDto } from '@/dtos/datos-producto.dto';
import type { ExistenciaOpcionDto } from '@/dtos/existencia-opcion.dto';
import type { ProductoOpcionDto } from '@/dtos/producto-opcion.dto';
import type { ProductoDto } from '@/dtos/producto.dto';
import type { UnidadDto } from '@/dtos/unidad.dto';
import type { CambiosProducto } from '@/models/cambios-producto.model';
import type { ExistenciaOpcion } from '@/models/existencia-opcion.model';
import type { NuevoProducto } from '@/models/nuevo-producto.model';
import type { ProductoOpcion } from '@/models/producto-opcion.model';
import type { Producto } from '@/models/producto.model';
import type { Unidad } from '@/models/unidad.model';

import { ComunMapper } from './comun.mapper';

const { opcional } = ComunMapper;

export class ProductoMapper {
  static unidad(dto: UnidadDto): Unidad {
    return { id: dto.id, clave: dto.clave, nombre: dto.nombre, permiteFracciones: dto.permiteFracciones };
  }

  static aModel(dto: ProductoDto): Producto {
    return {
      id: dto.id,
      referencia: dto.referencia,
      descripcion: dto.descripcion,
      unidadId: dto.unidadId,
      unidad: dto.unidad,
      permiteFracciones: dto.permiteFracciones,
      imagenRuta: opcional(dto.imagenRuta),
      activo: dto.activo,
      estado: dto.estado,
      gerenteAprobadorId: opcional(dto.gerenteAprobadorId),
      gerenteAprobador: opcional(dto.gerenteAprobador),
      creadoPor: dto.creadoPor,
      creadoEn: dto.creadoEn,
      motivoRechazo: opcional(dto.motivoRechazo),
      existencia: dto.existencia,
      texto: `${dto.referencia} · ${dto.descripcion}`,
    };
  }

  static existenciaOpcion(dto: ExistenciaOpcionDto): ExistenciaOpcion {
    return { almacenId: dto.almacenId, almacen: dto.almacen, cantidad: dto.cantidad, costoPromedio: dto.costoPromedio };
  }

  static opcion(dto: ProductoOpcionDto): ProductoOpcion {
    return {
      id: dto.id,
      referencia: dto.referencia,
      descripcion: dto.descripcion,
      unidad: dto.unidad,
      permiteFracciones: dto.permiteFracciones,
      existencias: dto.existencias.map(ProductoMapper.existenciaOpcion),
      texto: `${dto.referencia} · ${dto.descripcion}`,
    };
  }

  /** Un producto del catálogo como opción de captura (sin existencias), p. ej. para precargar el kardex. */
  static comoOpcion(p: Producto): ProductoOpcion {
    return { id: p.id, referencia: p.referencia, descripcion: p.descripcion, unidad: p.unidad, permiteFracciones: p.permiteFracciones, existencias: [], texto: p.texto };
  }

  static nuevo(model: NuevoProducto): CrearProductoDto {
    return {
      referencia: model.referencia.trim(),
      descripcion: model.descripcion.trim(),
      unidadId: model.unidadId,
      gerenteAprobadorId: model.gerenteAprobadorId ?? null,
    };
  }

  static cambios(model: CambiosProducto): DatosProductoDto {
    return { descripcion: model.descripcion.trim(), unidadId: model.unidadId };
  }
}

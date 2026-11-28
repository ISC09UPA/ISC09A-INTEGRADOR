import type { MargenProductoDto } from '@/dtos/margen-producto.dto';
import type { ReporteComprasDto } from '@/dtos/reporte-compras.dto';
import type { ReporteFinancieroDto } from '@/dtos/reporte-financiero.dto';
import type { ReporteInventarioDto } from '@/dtos/reporte-inventario.dto';
import type { ReporteVentasDto } from '@/dtos/reporte-ventas.dto';
import type { MargenProducto } from '@/models/margen-producto.model';
import type { ReporteCompras } from '@/models/reporte-compras.model';
import type { ReporteFinanciero } from '@/models/reporte-financiero.model';
import type { ReporteInventario } from '@/models/reporte-inventario.model';
import type { ReporteVentas } from '@/models/reporte-ventas.model';

import { AlmacenMapper } from './almacen.mapper';
import { ComunMapper } from './comun.mapper';

const { puntos } = ComunMapper;

export class ReporteMapper {
  static ventas(dto: ReporteVentasDto): ReporteVentas {
    return {
      numeroVentas: dto.numeroVentas,
      subtotal: dto.subtotal,
      iva: dto.iva,
      total: dto.total,
      ticketPromedio: dto.ticketPromedio,
      porPeriodo: puntos(dto.porPeriodo),
      topClientes: puntos(dto.topClientes),
      topProductos: puntos(dto.topProductos),
      porAlmacen: puntos(dto.porAlmacen),
    };
  }

  static compras(dto: ReporteComprasDto): ReporteCompras {
    return {
      numeroRecepciones: dto.numeroRecepciones,
      subtotal: dto.subtotal,
      iva: dto.iva,
      total: dto.total,
      porPeriodo: puntos(dto.porPeriodo),
      porProveedor: puntos(dto.porProveedor),
    };
  }

  static financiero(dto: ReporteFinancieroDto): ReporteFinanciero {
    return {
      periodos: dto.periodos,
      ingresos: dto.ingresos,
      egresos: dto.egresos,
      totalIngresos: dto.totalIngresos,
      totalEgresos: dto.totalEgresos,
      ivaTrasladado: dto.ivaTrasladado,
      ivaAcreditable: dto.ivaAcreditable,
      saldo: dto.saldo,
      ivaPorPagar: dto.ivaTrasladado - dto.ivaAcreditable,
    };
  }

  static inventario(dto: ReporteInventarioDto): ReporteInventario {
    return { valorTotal: dto.valorTotal, valorPorAlmacen: puntos(dto.valorPorAlmacen), bajoMinimo: dto.bajoMinimo.map(AlmacenMapper.existencia) };
  }

  static margen(dto: MargenProductoDto): MargenProducto {
    return {
      referencia: dto.referencia,
      descripcion: dto.descripcion,
      cantidad: dto.cantidad,
      ventas: dto.ventas,
      costo: dto.costo,
      margen: dto.margen,
      margenPorcentaje: dto.margenPorcentaje,
    };
  }
}

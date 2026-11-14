import type { DocumentoDetalleDto } from '@/dtos/documento-detalle.dto';
import type { DocumentoRegistradoDto } from '@/dtos/documento-registrado.dto';
import type { DocumentoResumenDto } from '@/dtos/documento-resumen.dto';
import type { LineaDocumentoDto } from '@/dtos/linea-documento.dto';
import type { RegistrarRecepcionDto } from '@/dtos/registrar-recepcion.dto';
import type { RegistrarTraspasoDto } from '@/dtos/registrar-traspaso.dto';
import type { RegistrarVentaDto } from '@/dtos/registrar-venta.dto';
import type { DocumentoDetalle } from '@/models/documento-detalle.model';
import type { DocumentoRef } from '@/models/documento-ref.model';
import type { DocumentoRegistrado } from '@/models/documento-registrado.model';
import type { DocumentoResumen } from '@/models/documento-resumen.model';
import type { LineaDocumento } from '@/models/linea-documento.model';
import type { NuevaRecepcion } from '@/models/nueva-recepcion.model';
import type { NuevaVenta } from '@/models/nueva-venta.model';
import type { NuevoTraspaso } from '@/models/nuevo-traspaso.model';
import type { TipoDocumento } from '@/types/enums';

import { ComunMapper } from './comun.mapper';

const { opcional } = ComunMapper;

/** Ventas, recepciones y traspasos comparten la forma de lista y de detalle. */
export class DocumentoMapper {
  static resumen(dto: DocumentoResumenDto): DocumentoResumen {
    return {
      id: dto.id,
      folio: dto.folio,
      fecha: dto.fecha,
      tercero: dto.tercero,
      almacen: dto.almacen,
      estado: dto.estado,
      lineas: dto.lineas,
      subtotal: dto.subtotal,
      iva: dto.iva,
      total: dto.total,
    };
  }

  static linea(dto: LineaDocumentoDto): LineaDocumento {
    return {
      productoId: dto.productoId,
      referencia: dto.referencia,
      descripcion: dto.descripcion,
      unidad: dto.unidad,
      almacen: opcional(dto.almacen),
      cantidad: dto.cantidad,
      precioUnitario: dto.precioUnitario,
      tasaIva: dto.tasaIva,
      subtotal: dto.subtotal,
      iva: dto.iva,
      costoUnitario: dto.costoUnitario,
      total: dto.total,
    };
  }

  static detalle(dto: DocumentoDetalleDto): DocumentoDetalle {
    return {
      id: dto.id,
      folio: dto.folio,
      fecha: dto.fecha,
      estado: dto.estado,
      tercero: opcional(dto.tercero),
      terceroRfc: opcional(dto.terceroRfc),
      terceroCorreo: opcional(dto.terceroCorreo),
      almacen: dto.almacen,
      almacenDestino: opcional(dto.almacenDestino),
      lineas: dto.lineas.map(DocumentoMapper.linea),
      creadoPor: dto.creadoPor,
      creadoEn: dto.creadoEn,
      cancelacion:
        dto.estado === 'Cancelado'
          ? { por: dto.canceladoPor ?? '—', en: dto.canceladoEn ?? dto.creadoEn, motivo: dto.motivoCancelacion ?? '—' }
          : undefined,
      subtotal: dto.subtotal,
      iva: dto.iva,
      total: dto.total,
    };
  }

  static registrado(dto: DocumentoRegistradoDto): DocumentoRegistrado {
    return { id: dto.id, folio: dto.folio };
  }

  /** Arma la referencia solo cuando la API manda tipo, id y folio. */
  static referencia(tipo?: TipoDocumento | null, id?: string | null, folio?: number | null): DocumentoRef | undefined {
    return tipo && id && folio ? { tipo, id, folio } : undefined;
  }

  static nuevaVenta(model: NuevaVenta): RegistrarVentaDto {
    return {
      clienteId: model.clienteId,
      fecha: model.fecha,
      lineas: model.lineas.map((l) => ({
        productoId: l.productoId,
        almacenId: l.almacenId,
        cantidad: l.cantidad,
        precioUnitario: l.precioUnitario,
        tasaIva: l.tasaIva,
      })),
    };
  }

  static nuevaRecepcion(model: NuevaRecepcion): RegistrarRecepcionDto {
    return {
      proveedorId: model.proveedorId,
      almacenId: model.almacenId,
      fecha: model.fecha,
      lineas: model.lineas.map((l) => ({ productoId: l.productoId, cantidad: l.cantidad, costoUnitario: l.costoUnitario, tasaIva: l.tasaIva })),
    };
  }

  static nuevoTraspaso(model: NuevoTraspaso): RegistrarTraspasoDto {
    return {
      almacenOrigenId: model.almacenOrigenId,
      almacenDestinoId: model.almacenDestinoId,
      fecha: model.fecha,
      lineas: model.lineas.map((l) => ({ productoId: l.productoId, cantidad: l.cantidad })),
    };
  }
}

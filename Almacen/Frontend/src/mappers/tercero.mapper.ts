import type { DatosTerceroDto } from '@/dtos/datos-tercero.dto';
import type { DireccionDto } from '@/dtos/direccion.dto';
import type { TerceroDto } from '@/dtos/tercero.dto';
import type { DatosTercero } from '@/models/datos-tercero.model';
import type { Direccion } from '@/models/direccion.model';
import type { Tercero } from '@/models/tercero.model';

const MEXICO = 'México';

/** Clientes y proveedores comparten forma. */
export class TerceroMapper {
  static direccion(dto: DireccionDto): Direccion {
    return {
      calle: dto.calle,
      numeroExterior: dto.numeroExterior,
      numeroInterior: dto.numeroInterior ?? '',
      colonia: dto.colonia,
      codigoPostal: dto.codigoPostal,
      municipio: dto.municipio,
      estado: dto.estado,
      pais: dto.pais || MEXICO,
    };
  }

  static aModel(dto: TerceroDto): Tercero {
    return {
      id: dto.id,
      codigo: dto.codigo,
      nombre: dto.nombre,
      correo: dto.correo,
      rfc: dto.rfc,
      direccion: TerceroMapper.direccion(dto.direccion),
      activo: dto.activo,
      texto: `${dto.codigo} · ${dto.nombre}`,
    };
  }

  /** Limpia espacios, pasa el RFC a mayúsculas y manda null en lo opcional vacío. */
  static datos(model: DatosTercero): DatosTerceroDto {
    const d = model.direccion;
    return {
      codigo: model.codigo.trim(),
      nombre: model.nombre.trim(),
      correo: model.correo.trim(),
      rfc: model.rfc.trim().toUpperCase(),
      direccion: {
        calle: d.calle.trim(),
        numeroExterior: d.numeroExterior.trim(),
        numeroInterior: d.numeroInterior.trim() || null,
        colonia: d.colonia.trim(),
        codigoPostal: d.codigoPostal.trim(),
        municipio: d.municipio.trim(),
        estado: d.estado.trim(),
        pais: d.pais.trim() || MEXICO,
      },
    };
  }
}

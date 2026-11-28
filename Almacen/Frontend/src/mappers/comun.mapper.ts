import type { OpcionDto } from '@/dtos/opcion.dto';
import type { PaginaDto } from '@/dtos/pagina.dto';
import type { PuntoDto } from '@/dtos/punto.dto';
import type { Opcion } from '@/models/opcion.model';
import type { Pagina } from '@/models/pagina.model';
import type { Punto } from '@/models/punto.model';

/** Piezas que reutilizan los demás mappers. */
export class ComunMapper {
  static pagina<TDto, TModel>(dto: PaginaDto<TDto>, elemento: (d: TDto) => TModel): Pagina<TModel> {
    return { elementos: dto.elementos.map(elemento), total: dto.total, numeroPagina: dto.numeroPagina, tamano: dto.tamano };
  }

  static punto(dto: PuntoDto): Punto {
    return { etiqueta: dto.etiqueta, valor: dto.valor };
  }

  static puntos(dtos: PuntoDto[] | null | undefined): Punto[] {
    return (dtos ?? []).map(ComunMapper.punto);
  }

  static opcion(dto: OpcionDto): Opcion {
    return { id: dto.id, texto: dto.texto };
  }

  /** null de la API → undefined en los models (campos opcionales). */
  static opcional<T>(valor: T | null | undefined): T | undefined {
    return valor ?? undefined;
  }
}

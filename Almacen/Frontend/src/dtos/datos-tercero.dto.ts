import type { DireccionDto } from '@/dtos/direccion.dto';

/** Lo que se captura al dar de alta o editar un cliente o proveedor. Contrato de la API. */
export interface DatosTerceroDto {
  codigo: string;
  nombre: string;
  correo: string;
  rfc: string;
  direccion: DireccionDto;
}

import type { Direccion } from '@/models/direccion.model';

/** Lo que se captura al dar de alta o editar un cliente o proveedor. */
export interface DatosTercero {
  codigo: string;
  nombre: string;
  correo: string;
  rfc: string;
  direccion: Direccion;
}

import type { Direccion } from '@/models/direccion.model';

/** Cliente o proveedor. */
export interface Tercero {
  id: string;
  codigo: string;
  nombre: string;
  correo: string;
  rfc: string;
  direccion: Direccion;
  activo: boolean;
  /** "CÓDIGO · Nombre", como en los selectores. */
  texto: string;
}

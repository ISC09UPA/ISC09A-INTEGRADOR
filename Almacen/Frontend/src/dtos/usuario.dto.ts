/** Cuenta del sistema. Contrato de la API. */
export interface UsuarioDto {
  id: string;
  nombre: string;
  correo: string;
  rol: string;
  activo: boolean;
  /** Almacén donde trabaja; null en el superadmin. */
  almacenId?: string | null;
  almacen?: string | null;
}

/** Cuenta del sistema. */
export interface Usuario {
  id: string;
  nombre: string;
  correo: string;
  rol: string;
  activo: boolean;
  /** Almacén donde trabaja; vacío en el superadmin (ve todos). */
  almacenId?: string;
  almacen?: string;
}

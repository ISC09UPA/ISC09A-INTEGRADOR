/** Almacén asignado al usuario de la sesión: solo opera y ve el stock de ese. */
export interface MiAlmacen {
  almacenId?: string;
  almacen?: string;
  veTodos: boolean;
}

/** Sesión del usuario: el token y lo que trae (nombre, rol, permisos), con la expiración ya como fecha. */
export interface Sesion {
  /** JWT para Authorization: Bearer. */
  token: string;
  expira: Date;
  nombre: string;
  usuarioId?: string;
  /** Rol principal (el primero). */
  rol: string;
  roles: string[];
  permisos: string[];
}

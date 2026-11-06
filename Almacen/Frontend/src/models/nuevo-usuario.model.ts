/** Lo que se envía en POST /api/usuarios. */
export interface NuevoUsuario {
  nombre: string;
  correo: string;
  rol: string;
  contrasena: string;
}

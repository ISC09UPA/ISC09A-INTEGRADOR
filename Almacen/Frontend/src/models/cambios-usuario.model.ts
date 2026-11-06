/** Lo que se envía en PUT /api/usuarios/{id}. */
export interface CambiosUsuario {
  nombre: string;
  rol: string;
  activo: boolean;
}

/** Cuerpo de POST /api/usuarios. Contrato de la API. */
export interface CrearUsuarioDto {
  nombre: string;
  correo: string;
  rol: string;
  contrasena: string;
}

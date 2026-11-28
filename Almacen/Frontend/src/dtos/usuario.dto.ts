/** Cuenta del sistema. Contrato de la API. */
export interface UsuarioDto {
  id: string;
  nombre: string;
  correo: string;
  rol: string;
  activo: boolean;
}

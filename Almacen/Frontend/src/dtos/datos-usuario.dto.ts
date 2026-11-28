/** Cuerpo de PUT /api/usuarios/{id}. Contrato de la API. */
export interface DatosUsuarioDto {
  nombre: string;
  rol: string;
  activo: boolean;
  /** null conserva el almacén actual. */
  almacenId?: string | null;
}

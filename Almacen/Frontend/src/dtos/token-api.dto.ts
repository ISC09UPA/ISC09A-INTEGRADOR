/** Respuesta de POST /api/cuenta/token: JWT con roles y permisos. Contrato de la API. */
export interface TokenApiDto {
  token: string;
  expira: string;
  nombre: string;
  roles: string[];
  permisos: string[];
}

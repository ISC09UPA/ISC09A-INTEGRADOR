/** Quién tiene el modo inteligente de venta (el lector NFC). Contrato de la API. */
export interface SesionVentaDto {
  sesionId: string;
  usuarioId: string;
  usuario: string;
  almacenId: string;
  almacen: string;
  inicio: string;
  ultimoLatido: string;
  esMia: boolean;
}

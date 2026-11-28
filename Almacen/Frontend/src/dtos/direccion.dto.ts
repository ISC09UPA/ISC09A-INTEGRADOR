/** Dirección de cliente o proveedor. Contrato de la API. */
export interface DireccionDto {
  calle: string;
  numeroExterior: string;
  numeroInterior?: string | null;
  colonia: string;
  codigoPostal: string;
  municipio: string;
  estado: string;
  pais?: string | null;
}

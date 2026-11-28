import { almacenesService } from '@/services';

import { useDatos } from './use-datos';

/**
 * Id del almacén asignado al usuario, o undefined si es superadmin (ve todos) o aún no carga.
 * Las pantallas lo fijan en sus selectores: el usuario solo opera y ve el stock de su almacén.
 */
export function useMiAlmacen(): string | undefined {
  const { datos } = useDatos(() => almacenesService.mio());
  return datos && !datos.veTodos ? datos.almacenId : undefined;
}

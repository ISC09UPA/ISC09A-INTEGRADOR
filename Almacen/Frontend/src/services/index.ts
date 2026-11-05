import { httpClient } from '@/api/http-client';

import { AlmacenesService } from './almacenes.service';
import { ArchivosService } from './archivos.service';
import { CuentaService } from './cuenta.service';
import { RecepcionesService, TraspasosService, VentasService } from './documentos.service';
import { FinanzasService } from './finanzas.service';
import { MovimientosService } from './movimientos.service';
import { ProductosService } from './productos.service';
import { ReportesService } from './reportes.service';
import { ClientesService, ProveedoresService } from './terceros.service';
import { UsuariosService } from './usuarios.service';

// Una instancia de cada service, todas sobre el mismo HttpClient (y por lo tanto el mismo token).
export const cuentaService = new CuentaService(httpClient);
export const clientesService = new ClientesService(httpClient);
export const proveedoresService = new ProveedoresService(httpClient);
export const productosService = new ProductosService(httpClient);
export const almacenesService = new AlmacenesService(httpClient);
export const ventasService = new VentasService(httpClient);
export const recepcionesService = new RecepcionesService(httpClient);
export const traspasosService = new TraspasosService(httpClient);
export const movimientosService = new MovimientosService(httpClient);
export const finanzasService = new FinanzasService(httpClient);
export const reportesService = new ReportesService(httpClient);
export const usuariosService = new UsuariosService(httpClient);
export const archivosService = new ArchivosService(httpClient);

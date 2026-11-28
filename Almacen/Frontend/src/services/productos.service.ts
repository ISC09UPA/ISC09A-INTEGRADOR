import { HttpClient } from '@/api/http-client';
import type { PaginaDto } from '@/dtos/pagina.dto';
import type { ProductoOpcionDto } from '@/dtos/producto-opcion.dto';
import type { ProductoDto } from '@/dtos/producto.dto';
import type { UnidadDto } from '@/dtos/unidad.dto';
import type { UsuarioDto } from '@/dtos/usuario.dto';
import { ComunMapper } from '@/mappers/comun.mapper';
import { ProductoMapper } from '@/mappers/producto.mapper';
import { UsuarioMapper } from '@/mappers/usuario.mapper';
import type { CambiosProducto } from '@/models/cambios-producto.model';
import type { NuevoProducto } from '@/models/nuevo-producto.model';
import type { Pagina } from '@/models/pagina.model';
import type { ProductoOpcion } from '@/models/producto-opcion.model';
import type { Producto } from '@/models/producto.model';
import type { Unidad } from '@/models/unidad.model';
import type { Usuario } from '@/models/usuario.model';
import type { EstadoAprobacion } from '@/types/enums';

export interface FiltroProductos {
  busqueda?: string;
  estado?: EstadoAprobacion;
  inactivos?: boolean;
  /** Solo los que el usuario (gerente) tiene que aprobar. */
  misAprobaciones?: boolean;
  pagina?: number;
  tamano?: number;
}

/** Imagen elegida en el teléfono (uri) o en el navegador (Blob). */
export interface ArchivoImagen {
  nombre: string;
  tipo: string;
  uri?: string;
  blob?: Blob;
}

export class ProductosService {
  constructor(private readonly http: HttpClient) {}

  async listar(filtro: FiltroProductos): Promise<Pagina<Producto>> {
    const dto = await this.http.get<PaginaDto<ProductoDto>>('/api/productos', { ...filtro, tamano: filtro.tamano ?? 20 });
    return ComunMapper.pagina(dto, ProductoMapper.aModel);
  }

  async obtener(id: string): Promise<Producto> {
    return ProductoMapper.aModel(await this.http.get<ProductoDto>(`/api/productos/${id}`));
  }

  /** Aprobados y activos con su existencia por almacén, para capturar documentos. */
  async disponibles(texto?: string, maximo = 30): Promise<ProductoOpcion[]> {
    const dtos = await this.http.get<ProductoOpcionDto[]>('/api/productos/disponibles', { texto, maximo });
    return dtos.map(ProductoMapper.opcion);
  }

  async unidades(): Promise<Unidad[]> {
    return (await this.http.get<UnidadDto[]>('/api/productos/unidades')).map(ProductoMapper.unidad);
  }

  /** Gerentes activos a los que se puede enviar la aprobación. */
  async gerentes(): Promise<Usuario[]> {
    return (await this.http.get<UsuarioDto[]>('/api/productos/gerentes')).map(UsuarioMapper.aModel);
  }

  async crear(producto: NuevoProducto): Promise<Producto> {
    return ProductoMapper.aModel(await this.http.post<ProductoDto>('/api/productos', ProductoMapper.nuevo(producto)));
  }

  async editar(id: string, cambios: CambiosProducto): Promise<Producto> {
    return ProductoMapper.aModel(await this.http.put<ProductoDto>(`/api/productos/${id}`, ProductoMapper.cambios(cambios)));
  }

  async cambiarEstado(id: string, activo: boolean): Promise<Producto> {
    return ProductoMapper.aModel(await this.http.patch<ProductoDto>(`/api/productos/${id}/estado`, { activo }));
  }

  async aprobar(id: string): Promise<Producto> {
    return ProductoMapper.aModel(await this.http.post<ProductoDto>(`/api/productos/${id}/aprobar`));
  }

  async rechazar(id: string, motivo: string): Promise<Producto> {
    return ProductoMapper.aModel(await this.http.post<ProductoDto>(`/api/productos/${id}/rechazar`, { texto: motivo }));
  }

  /** Fuente para <Image>: /api/archivos pide el token. */
  fuenteImagen(ruta: string) {
    return { uri: this.http.url(`/api/archivos/${ruta}`), headers: this.http.encabezadosAutorizacion };
  }

  /** multipart/form-data con el campo "imagen" (JPG, PNG o WebP, máx. 2 MB). */
  async subirImagen(id: string, imagen: ArchivoImagen): Promise<Producto> {
    const formulario = new FormData();
    if (imagen.blob) formulario.append('imagen', imagen.blob, imagen.nombre);
    // React Native sube el archivo a partir de su uri.
    else formulario.append('imagen', { uri: imagen.uri, name: imagen.nombre, type: imagen.tipo } as unknown as Blob);
    return ProductoMapper.aModel(await this.http.subir<ProductoDto>(`/api/productos/${id}/imagen`, formulario));
  }
}

import type { CrearUsuarioDto } from '@/dtos/crear-usuario.dto';
import type { DatosUsuarioDto } from '@/dtos/datos-usuario.dto';
import type { UsuarioDto } from '@/dtos/usuario.dto';
import type { CambiosUsuario } from '@/models/cambios-usuario.model';
import type { NuevoUsuario } from '@/models/nuevo-usuario.model';
import type { Usuario } from '@/models/usuario.model';

export class UsuarioMapper {
  static aModel(dto: UsuarioDto): Usuario {
    return { id: dto.id, nombre: dto.nombre, correo: dto.correo, rol: dto.rol, activo: dto.activo };
  }

  static nuevo(model: NuevoUsuario): CrearUsuarioDto {
    return { nombre: model.nombre.trim(), correo: model.correo.trim(), rol: model.rol, contrasena: model.contrasena };
  }

  static cambios(model: CambiosUsuario): DatosUsuarioDto {
    return { nombre: model.nombre.trim(), rol: model.rol, activo: model.activo };
  }
}

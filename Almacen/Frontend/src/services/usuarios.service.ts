import { HttpClient } from '@/api/http-client';
import type { UsuarioDto } from '@/dtos/usuario.dto';
import { UsuarioMapper } from '@/mappers/usuario.mapper';
import type { CambiosUsuario } from '@/models/cambios-usuario.model';
import type { NuevoUsuario } from '@/models/nuevo-usuario.model';
import type { Usuario } from '@/models/usuario.model';

export class UsuariosService {
  constructor(private readonly http: HttpClient) {}

  async listar(rol?: string): Promise<Usuario[]> {
    return (await this.http.get<UsuarioDto[]>('/api/usuarios', { rol })).map(UsuarioMapper.aModel);
  }

  async crear(usuario: NuevoUsuario): Promise<Usuario> {
    return UsuarioMapper.aModel(await this.http.post<UsuarioDto>('/api/usuarios', UsuarioMapper.nuevo(usuario)));
  }

  async actualizar(id: string, cambios: CambiosUsuario): Promise<Usuario> {
    return UsuarioMapper.aModel(await this.http.put<UsuarioDto>(`/api/usuarios/${id}`, UsuarioMapper.cambios(cambios)));
  }

  restablecerContrasena(id: string, contrasena: string) {
    return this.http.post(`/api/usuarios/${id}/contrasena`, { contrasena });
  }
}

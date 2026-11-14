import type { CredencialesDto } from '@/dtos/credenciales.dto';
import type { TokenApiDto } from '@/dtos/token-api.dto';
import type { Credenciales } from '@/models/credenciales.model';
import type { Sesion } from '@/models/sesion.model';

const CLAIM_ID = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier';

export class SesionMapper {
  static aModel(dto: TokenApiDto): Sesion {
    return {
      token: dto.token,
      expira: new Date(dto.expira),
      nombre: dto.nombre,
      usuarioId: SesionMapper.idDelToken(dto.token),
      rol: dto.roles[0] ?? '',
      roles: dto.roles,
      permisos: dto.permisos,
    };
  }

  static credenciales(model: Credenciales): CredencialesDto {
    return { correo: model.correo.trim(), contrasena: model.contrasena };
  }

  /** Id del usuario dentro del JWT (claim nameidentifier); sirve para saber qué aprobaciones le tocan. */
  private static idDelToken(token: string): string | undefined {
    try {
      const base = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
      // Solo se lee el id (ASCII), así que no hace falta decodificar UTF-8.
      const datos = JSON.parse(atob(base.padEnd(base.length + ((4 - (base.length % 4)) % 4), '=')));
      return datos[CLAIM_ID] ?? datos.sub ?? undefined;
    } catch {
      return undefined;
    }
  }
}

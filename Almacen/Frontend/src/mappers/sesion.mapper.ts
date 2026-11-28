import type { EstadoInicioSesionIotDto, SolicitudInicioSesionIotDto } from '@/dtos/inicio-sesion-iot.dto';
import type { TokenApiDto } from '@/dtos/token-api.dto';
import type { EstadoInicioSesionIot, SolicitudInicioSesionIot } from '@/models/inicio-sesion-iot.model';
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

  static solicitudIot(dto: SolicitudInicioSesionIotDto): SolicitudInicioSesionIot {
    return { id: dto.id, expira: new Date(dto.expira), vigenciaSegundos: dto.vigenciaSegundos };
  }

  static estadoIot(dto: EstadoInicioSesionIotDto): EstadoInicioSesionIot {
    switch (dto.estado) {
      case 'pendiente':
        return { estado: 'pendiente' };
      case 'completada':
        return dto.token
          ? { estado: 'completada', sesion: SesionMapper.aModel(dto.token) }
          : { estado: 'consumida', motivo: 'El inicio de sesión ya se usó.' };
      default:
        return { estado: dto.estado, motivo: dto.motivo ?? 'No se pudo iniciar sesión.' };
    }
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

import * as SecureStore from 'expo-secure-store';
import { Platform } from 'react-native';

import { HttpClient } from '@/api/http-client';
import type { TokenApiDto } from '@/dtos/token-api.dto';
import { SesionMapper } from '@/mappers/sesion.mapper';
import type { Credenciales } from '@/models/credenciales.model';
import type { Sesion } from '@/models/sesion.model';

const CLAVE = 'almacen.sesion';

/** Inicio de sesión y sesión guardada. Se guarda el DTO tal cual y se vuelve a mapear al abrir la app. */
export class CuentaService {
  constructor(private readonly http: HttpClient) {}

  async ingresar(credenciales: Credenciales): Promise<Sesion> {
    const dto = await this.http.post<TokenApiDto>('/api/cuenta/token', SesionMapper.credenciales(credenciales));
    await this.guardar(JSON.stringify(dto));
    return SesionMapper.aModel(dto);
  }

  /** La sesión guardada si todavía le queda al menos un minuto; si venció, la borra. */
  async restaurar(): Promise<Sesion | null> {
    const texto = await this.leer();
    if (!texto) return null;
    try {
      const sesion = SesionMapper.aModel(JSON.parse(texto) as TokenApiDto);
      if (sesion.expira.getTime() > Date.now() + 60_000) return sesion;
    } catch {
      // Guardado corrupto: se descarta.
    }
    await this.guardar(null);
    return null;
  }

  salir() {
    return this.guardar(null);
  }

  // En el teléfono el token se guarda cifrado (Keychain / Keystore); en el navegador, en localStorage.
  private async leer(): Promise<string | null> {
    if (Platform.OS !== 'web') return SecureStore.getItemAsync(CLAVE);
    try {
      return globalThis.localStorage?.getItem(CLAVE) ?? null;
    } catch {
      return null;
    }
  }

  private async guardar(valor: string | null) {
    if (Platform.OS !== 'web') {
      if (valor) await SecureStore.setItemAsync(CLAVE, valor);
      else await SecureStore.deleteItemAsync(CLAVE);
      return;
    }
    try {
      if (valor) globalThis.localStorage?.setItem(CLAVE, valor);
      else globalThis.localStorage?.removeItem(CLAVE);
    } catch {
      // Navegación privada: la sesión dura lo que la pestaña.
    }
  }
}

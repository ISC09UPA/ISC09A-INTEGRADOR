export type Rol = 'Usuario' | 'Administrador';
export type LockerEstado = 'Disponible' | 'Ocupado' | 'Mantenimiento';

export interface Usuario {
  id: number;
  nombre: string;
  email: string;
  rol: Rol;
  tieneRostro: boolean;
}

export interface AuthResponse {
  token: string;
  usuario: Usuario;
}

export interface Locker {
  id: number;
  codigo: string;
  estado: LockerEstado;
  pesoMaximoKg: number;
  pesoActualKg: number;
  puertaCerrada: boolean;
}

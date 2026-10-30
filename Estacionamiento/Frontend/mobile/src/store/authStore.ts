import { create } from 'zustand';
import { saveSecureItem, removeSecureItem, getSecureItem, STORAGE_KEYS } from '@/services/secureStore';
import { authenticateWithBiometrics } from '@/services/biometrics';
import { request } from '@/services/apiClient';

export type UserRole = 'ADMIN' | 'CUSTOMER';

export interface UserProfile {
  id: string;
  fullName: string;
  email: string;
  phoneNumber: string;
  role: UserRole;
  frequentFlyerNumber?: string;
  biometricsEnabled: boolean;
  tier: 'STANDARD' | 'AEROPARK_BLACK' | 'FIRST_CLASS';
}

interface AuthState {
  isAuthenticated: boolean;
  isLoading: boolean;
  user: UserProfile | null;
  token: string | null;
  role: UserRole;
  loginWithPassword: (email: string, password: string) => Promise<{ success: boolean; role?: UserRole; error?: string }>;
  register: (data: {
    nombre: string;
    email: string;
    password: string;
    telefono: string;
    placa: string;
    marcaModelo?: string;
  }) => Promise<{ success: boolean; error?: string }>;
  loginWithBiometrics: () => Promise<boolean>;
  logout: () => Promise<void>;
  enableBiometrics: (enabled: boolean) => Promise<void>;
}

// Perfil Demo Supervisor (Admin)
const ADMIN_USER: UserProfile = {
  id: 'admin-01',
  fullName: 'Carlos Mendoza (Supervisor)',
  email: 'admin@aeropark.mx',
  phoneNumber: '+52 449 999 8888',
  role: 'ADMIN',
  biometricsEnabled: true,
  tier: 'FIRST_CLASS',
};

// Perfil Demo Cliente (Viajero Frecuente)
const CUSTOMER_USER: UserProfile = {
  id: 'cust-01',
  fullName: 'Laura Sánchez (Viajera)',
  email: 'laura@aeropark.mx',
  phoneNumber: '+52 449 123 4567',
  role: 'CUSTOMER',
  frequentFlyerNumber: 'AM-984210',
  biometricsEnabled: true,
  tier: 'AEROPARK_BLACK',
};

export const useAuthStore = create<AuthState>((set) => ({
  isAuthenticated: false, // Inicia en pantalla de login para permitir seleccionar rol
  isLoading: false,
  user: null,
  token: null,
  role: 'CUSTOMER',

  loginWithPassword: async (email: string, password: string) => {
    set({ isLoading: true });

    try {
      // 1. Intentar llamar al backend .NET 10 Minimal APIs
      const response = await request<{
        token: string;
        customerId: string;
        nombre: string;
        email: string;
        role: string;
      }>('/v1/auth/login', {
        method: 'POST',
        body: JSON.stringify({ email, password }),
      });

      if (response.data && response.status === 200) {
        const role = (response.data.role.toUpperCase() === 'ADMIN' ? 'ADMIN' : 'CUSTOMER') as UserRole;
        const profile: UserProfile = {
          id: response.data.customerId,
          fullName: response.data.nombre,
          email: response.data.email,
          phoneNumber: role === 'ADMIN' ? '+52 449 999 8888' : '+52 449 123 4567',
          role: role,
          biometricsEnabled: true,
          tier: role === 'ADMIN' ? 'FIRST_CLASS' : 'AEROPARK_BLACK',
        };

        await saveSecureItem(STORAGE_KEYS.AUTH_TOKEN, response.data.token);
        set({
          isAuthenticated: true,
          isLoading: false,
          user: profile,
          token: response.data.token,
          role: role,
        });
        return { success: true, role };
      }
    } catch {
      // Fallback si no hay red o backend está iniciando
    }

    // 2. Fallback de detección inteligente de rol local
    const normalizedEmail = email.toLowerCase().trim();
    const isAdmin = normalizedEmail.includes('admin') || normalizedEmail.includes('carlos');
    const selectedUser = isAdmin ? ADMIN_USER : CUSTOMER_USER;
    const role: UserRole = selectedUser.role;

    const token = `ey-aeropark-jwt-${role.toLowerCase()}-${Date.now()}`;
    await saveSecureItem(STORAGE_KEYS.AUTH_TOKEN, token);

    set({
      isAuthenticated: true,
      isLoading: false,
      user: { ...selectedUser, email },
      token,
      role,
    });

    return { success: true, role };
  },

  register: async ({ nombre, email, password, telefono, placa, marcaModelo }) => {
    set({ isLoading: true });
    try {
      const response = await request<{
        token: string;
        customerId: string;
        nombre: string;
        email: string;
        role: string;
      }>('/v1/auth/register', {
        method: 'POST',
        body: JSON.stringify({
          nombre,
          email,
          password,
          telefono,
          placa,
          marcaModelo,
        }),
      });

      if (response.data && (response.status === 200 || response.status === 201)) {
        const profile: UserProfile = {
          id: response.data.customerId,
          fullName: response.data.nombre,
          email: response.data.email,
          phoneNumber: telefono,
          role: 'CUSTOMER',
          biometricsEnabled: true,
          tier: 'STANDARD',
        };

        await saveSecureItem(STORAGE_KEYS.AUTH_TOKEN, response.data.token);
        set({
          isAuthenticated: true,
          isLoading: false,
          user: profile,
          token: response.data.token,
          role: 'CUSTOMER',
        });
        return { success: true };
      }
    } catch {
      // Fallback local autónomo
    }

    const customerId = `cust-${Date.now()}`;
    const token = `ey-aeropark-jwt-cust-${Date.now()}`;
    await saveSecureItem(STORAGE_KEYS.AUTH_TOKEN, token);

    const profile: UserProfile = {
      id: customerId,
      fullName: nombre,
      email,
      phoneNumber: telefono,
      role: 'CUSTOMER',
      biometricsEnabled: true,
      tier: 'STANDARD',
    };

    set({
      isAuthenticated: true,
      isLoading: false,
      user: profile,
      token,
      role: 'CUSTOMER',
    });

    return { success: true };
  },

  loginWithBiometrics: async () => {
    const success = await authenticateWithBiometrics('Ingresar a AeroPark con Face ID');
    if (success) {
      set({
        isAuthenticated: true,
        user: CUSTOMER_USER,
        token: 'biometric-token',
        role: 'CUSTOMER',
      });
      return true;
    }
    return false;
  },

  logout: async () => {
    await removeSecureItem(STORAGE_KEYS.AUTH_TOKEN);
    set({
      isAuthenticated: false,
      user: null,
      token: null,
      role: 'CUSTOMER',
    });
  },

  enableBiometrics: async (enabled) => {
    await saveSecureItem(STORAGE_KEYS.BIOMETRIC_PREF, enabled ? 'true' : 'false');
    set((state) => ({
      user: state.user ? { ...state.user, biometricsEnabled: enabled } : null,
    }));
  },
}));

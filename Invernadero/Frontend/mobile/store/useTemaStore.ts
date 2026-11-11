import { create } from 'zustand';
import { persist, createJSONStorage } from 'zustand/middleware';
import AsyncStorage from '@react-native-async-storage/async-storage';
import type { NombreTema } from '@/theme/tokens';

interface TemaState {
  tema: NombreTema;
  cambiarTema: (tema: NombreTema) => void;
}

export const useTemaStore = create<TemaState>()(
  persist(
    (set) => ({
      tema: 'claro',
      cambiarTema: (tema) => set({ tema }),
    }),
    {
      name: 'riego-tema',
      storage: createJSONStorage(() => AsyncStorage),
    }
  )
);
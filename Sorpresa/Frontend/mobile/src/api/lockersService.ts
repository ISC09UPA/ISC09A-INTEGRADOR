import client from './client';
import type { Locker } from '@/types';

export const listar = (soloDisponibles = false) =>
  client
    .get<Locker[]>('/api/lockers', { params: { disponibles: soloDisponibles } })
    .then((r) => r.data);

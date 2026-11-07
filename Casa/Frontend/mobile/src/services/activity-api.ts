// Servicio mock de actividad. Reemplazar por la llamada a la API real manteniendo la misma
// forma de los datos; la UI y el hook no necesitan cambios.

export type ActivityCategory = 'luz' | 'seguridad' | 'accesos';

export type ActivityStatus = 'normal' | 'success' | 'warning';

export type ActivityEvent = {
  id: string;
  title: string;
  detail: string;
  time: Date;
  category: ActivityCategory;
  status: ActivityStatus;
  /** El evento llegó a la app después de ocurrir (p. ej. la casa estuvo sin conexión). */
  delayed?: boolean;
};

function mockTime(hours: number, minutes: number) {
  const date = new Date();
  date.setHours(hours, minutes, 0, 0);
  return date;
}

/** Eventos ordenados del más reciente al más antiguo. */
export async function fetchActivityEvents(): Promise<ActivityEvent[]> {
  return [
    {
      id: 'evt-1',
      title: 'Movimiento en Sala',
      detail: 'Incidente · seguridad armada',
      time: mockTime(18, 42),
      category: 'seguridad',
      status: 'normal',
    },
    {
      id: 'evt-2',
      title: 'Acceso facial · Puerta principal',
      detail: 'Alex Rivera · autorizado',
      time: mockTime(18, 31),
      category: 'accesos',
      status: 'success',
    },
    {
      id: 'evt-3',
      title: 'Luz de Sala encendida',
      detail: 'Confirmado por la Pi',
      time: mockTime(18, 12),
      category: 'luz',
      status: 'normal',
    },
    {
      id: 'evt-4',
      title: 'Intento por huella · denegado',
      detail: 'Puerta principal',
      time: mockTime(18, 5),
      category: 'accesos',
      status: 'warning',
      delayed: true,
    },
  ];
}

import { useState } from 'react';
import { StyleSheet } from 'react-native';

import {
  AppHeader,
  AppText,
  EventItem,
  Icons,
  Screen,
  Section,
  Segmented,
  Surface,
} from '@/components';
import { Spacing } from '@/theme';

type Filter = 'todo' | 'seguridad' | 'dispositivos';

export default function ActividadScreen() {
  const [filter, setFilter] = useState<Filter>('todo');

  const events = [
    { id: 'e1', group: 'Hoy', kind: 'seguridad', icon: Icons.unlock, tone: 'warning' as const, title: 'Alarma desarmada', description: 'Ana desde el móvil', time: '12:03' },
    { id: 'e2', group: 'Hoy', kind: 'dispositivos', icon: Icons.bulb, tone: 'accent' as const, title: 'Lámpara del salón encendida', description: 'Escena «Llegar a casa»', time: '11:58' },
    { id: 'e3', group: 'Hoy', kind: 'seguridad', icon: Icons.camera, tone: 'neutral' as const, title: 'Movimiento detectado', description: 'Cámara entrada principal', time: '09:31' },
    { id: 'e4', group: 'Hoy', kind: 'seguridad', icon: Icons.door, tone: 'success' as const, title: 'Puerta principal cerrada', description: 'Cerradura automática', time: '08:12' },
    { id: 'e5', group: 'Ayer', kind: 'dispositivos', icon: Icons.thermometer, tone: 'accent' as const, title: 'Termostato ajustado', description: '21 °C · Modo eco', time: '22:40' },
    { id: 'e6', group: 'Ayer', kind: 'seguridad', icon: Icons.lock, tone: 'success' as const, title: 'Alarma armada', description: 'Modo noche', time: '23:15' },
  ] as const;

  const visible = events.filter((event) => filter === 'todo' || event.kind === filter);
  const groups = ['Hoy', 'Ayer'].filter((group) => visible.some((event) => event.group === group));

  return (
    <Screen background="transparent" tabBarInset>
      <AppHeader eyebrow="Historial" title="Actividad" subtitle="Lo que ha pasado en casa" />

      <Segmented
        label="Filtrar actividad"
        value={filter}
        onChange={setFilter}
        options={[
          { value: 'todo', label: 'Todo' },
          { value: 'seguridad', label: 'Seguridad' },
          { value: 'dispositivos', label: 'Dispositivos' },
        ]}
      />

      {groups.map((group) => (
        <Section key={group} title={group}>
          <Surface padding={Spacing.two}>
            {visible
              .filter((event) => event.group === group)
              .map((event) => (
                <EventItem
                  key={event.id}
                  icon={event.icon}
                  tone={event.tone}
                  title={event.title}
                  description={event.description}
                  time={event.time}
                />
              ))}
          </Surface>
        </Section>
      ))}

      {visible.length === 0 && (
        <Section>
          <Surface padding={Spacing.four}>
            <AppText variant="body" color="inkMuted" style={styles.emptyText}>
              No hay eventos con este filtro.
            </AppText>
          </Surface>
        </Section>
      )}
    </Screen>
  );
}

const styles = StyleSheet.create({
  emptyText: {
    textAlign: 'center',
  },
});

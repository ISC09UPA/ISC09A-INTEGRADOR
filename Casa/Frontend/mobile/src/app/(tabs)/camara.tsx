import { useState } from 'react';
import { StyleSheet, useWindowDimensions, View } from 'react-native';

import {
  AppHeader,
  Button,
  CameraCard,
  Icons,
  Screen,
  Section,
  Segmented,
  StatusBanner,
} from '@/components';
import { MaxContentWidth, Spacing } from '@/theme';

type Zone = 'todas' | 'exterior' | 'interior';

const CAMERAS = [
  { id: 'c1', name: 'Entrada principal', zone: 'exterior' as Zone, live: true, time: 'Ahora' },
  { id: 'c2', name: 'Jardín trasero', zone: 'exterior' as Zone, live: true, time: 'Ahora' },
  { id: 'c3', name: 'Salón', zone: 'interior' as Zone, live: true, time: 'Ahora' },
  { id: 'c4', name: 'Pasillo', zone: 'interior' as Zone, live: false, time: 'Hace 20 min' },
];

export default function CamaraScreen() {
  const [zone, setZone] = useState<Zone>('todas');
  const { width } = useWindowDimensions();

  const columns = width >= 700 ? 3 : 2;
  const gap = Spacing.two;
  const contentWidth = Math.min(width, MaxContentWidth) - Spacing.three * 2;
  const itemWidth = (contentWidth - gap * (columns - 1)) / columns;
  const visible = CAMERAS.filter((camera) => zone === 'todas' || camera.zone === zone);

  return (
    <Screen background="transparent" tabBarInset>
      <AppHeader eyebrow="Vigilancia" title="Cámaras" subtitle={`${visible.length} cámaras · En vivo`} />

      <StatusBanner
        tone="neutral"
        icon={Icons.camera}
        title="Transmisión en vivo"
        description="Última grabación hace 20 min · 12 GB disponibles"
        action={<Button label="Grabaciones" size="sm" variant="secondary" disabled />}
      />

      <Segmented
        label="Filtrar por zona"
        value={zone}
        onChange={setZone}
        options={[
          { value: 'todas', label: 'Todas' },
          { value: 'exterior', label: 'Exterior' },
          { value: 'interior', label: 'Interior' },
        ]}
      />

      <Section title="Transmisiones">
        <View style={[styles.grid, { gap }]}>
          {visible.map((camera) => (
            <CameraCard
              key={camera.id}
              name={camera.name}
              live={camera.live}
              status={camera.live ? 'En vivo' : 'Sin señal'}
              time={camera.time}
              style={{ width: itemWidth }}
            />
          ))}
        </View>
      </Section>
    </Screen>
  );
}

const styles = StyleSheet.create({
  grid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
  },
});

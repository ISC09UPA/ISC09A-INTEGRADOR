import { useState } from 'react';
import { StyleSheet, View } from 'react-native';

import {
  AppHeader,
  Button,
  DeviceCard,
  Icons,
  Screen,
  Section,
  Segmented,
  StatusBanner,
} from '@/components';
import { Spacing } from '@/theme';

type Room = 'todas' | 'salon' | 'cocina' | 'dormitorio';

type LightDevice = {
  id: string;
  name: string;
  room: Room;
  on: boolean;
  level: number;
  detail: string;
};

const INITIAL_DEVICES: LightDevice[] = [
  { id: 'l1', name: 'Lámpara de pie', room: 'salon', on: true, level: 62, detail: '12 W' },
  { id: 'l2', name: 'Tira LED', room: 'salon', on: true, level: 40, detail: 'Cálido' },
  { id: 'l3', name: 'Luz de techo', room: 'cocina', on: false, level: 0, detail: '18 W' },
  { id: 'l4', name: 'Lámpara de mesilla', room: 'dormitorio', on: true, level: 25, detail: '8 W' },
];

export default function LucesScreen() {
  const [room, setRoom] = useState<Room>('todas');
  const [devices, setDevices] = useState(INITIAL_DEVICES);

  const visible = devices.filter((device) => room === 'todas' || device.room === room);
  const activeCount = devices.filter((device) => device.on).length;

  const toggle = (id: string, value: boolean) => {
    setDevices((current) =>
      current.map((device) =>
        device.id === id ? { ...device, on: value, level: value && device.level === 0 ? 50 : device.level } : device,
      ),
    );
  };

  return (
    <Screen background="transparent" tabBarInset>
      <AppHeader eyebrow="Iluminación" title="Luces" subtitle={`${devices.length} luces · ${activeCount} encendidas`} />

      <Segmented
        label="Filtrar por habitación"
        value={room}
        onChange={setRoom}
        options={[
          { value: 'todas', label: 'Todas' },
          { value: 'salon', label: 'Salón' },
          { value: 'cocina', label: 'Cocina' },
          { value: 'dormitorio', label: 'Dorm.' },
        ]}
      />

      <StatusBanner
        tone={activeCount > 0 ? 'accent' : 'neutral'}
        icon={Icons.bulb}
        title={activeCount > 0 ? `${activeCount} luces encendidas` : 'Todas las luces apagadas'}
        description={activeCount > 0 ? 'Ajusta el brillo o apaga todo de un golpe' : 'Consumo mínimo en este momento'}
        action={<Button label={activeCount > 0 ? 'Apagar todo' : 'Encender todas'} size="sm" variant={activeCount > 0 ? 'primary' : 'secondary'} onPress={() => setDevices((current) => current.map((device) => ({ ...device, on: activeCount === 0, level: activeCount === 0 ? (device.level || 50) : 0 })))} />}
      />

      <Section title="Dispositivos">
        {visible.map((device) => (
          <DeviceCard
            key={device.id}
            name={device.name}
            room={{ salon: 'Salón', cocina: 'Cocina', dormitorio: 'Dormitorio', todas: '' }[device.room]}
            on={device.on}
            level={device.level}
            detail={device.on ? `${device.level}% · ${device.detail}` : device.detail}
            onToggle={(value) => toggle(device.id, value)}
          />
        ))}
        {visible.length === 0 && (
          <StatusBanner tone="neutral" icon={Icons.info} title="No hay luces aquí" description="Prueba con otro filtro" />
        )}
      </Section>

      <Section title="Escenas">
        <View style={styles.scenesRow}>
          <Button label="Lectura" variant="secondary" icon={Icons.sun} disabled />
          <Button label="Cena" variant="secondary" icon={Icons.moon} disabled />
          <Button label="Fiesta" variant="secondary" icon={Icons.bolt} disabled />
        </View>
      </Section>
    </Screen>
  );
}

const styles = StyleSheet.create({
  scenesRow: {
    flexDirection: 'row',
    gap: Spacing.two,
  },
});

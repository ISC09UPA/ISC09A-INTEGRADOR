import { useState } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { Feather } from '@expo/vector-icons';
import { glass, text, type IconName } from '@/constants/casa-theme';
import { useCasaTheme } from '@/hooks/use-casa-theme';

const tabs = [
  { key: 'inicio', icon: 'home', label: 'Inicio' },
  { key: 'actividad', icon: 'activity', label: 'Actividad' },
  { key: 'camara', icon: 'video', label: 'Cámara' },
  { key: 'ajustes', icon: 'settings', label: 'Ajustes' },
] as const satisfies readonly { key: string; icon: IconName; label: string }[];

type TabKey = (typeof tabs)[number]['key'];

export function TabBar({ bottom }: { bottom: number }) {
  const colors = useCasaTheme();
  const [activa, setActiva] = useState<TabKey>('inicio');

  return (
    <View style={[styles.tabBar, { bottom: bottom + 12 }]}>
      {tabs.map(({ key, icon, label }) => {
        const color = key === activa ? colors.teal : colors.faint;
        return (
          <Pressable key={key} style={styles.tab} onPress={() => setActiva(key)}>
            <Feather name={icon} size={20} color={color} />
            <Text style={[styles.tabLabel, { color }]}>{label}</Text>
          </Pressable>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  tabBar: {
    ...glass('rgba(255, 255, 255, 0.82)', 0.16),
    position: 'absolute',
    left: 22,
    right: 22,
    flexDirection: 'row',
    justifyContent: 'space-around',
    paddingVertical: 10,
    borderRadius: 30,
  },
  tab: { alignItems: 'center', gap: 3, paddingHorizontal: 8 },
  tabLabel: text('medium', 11, 'faint'),
});

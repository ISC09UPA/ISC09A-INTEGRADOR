import type { ComponentProps } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { router, usePathname, type Href } from 'expo-router';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { glass, text } from '@/constants/casa-theme';
import { useCasaTheme } from '@/hooks/use-casa-theme';

type TabIcon = ComponentProps<typeof Ionicons>['name'];

/** Menú principal: cada pestaña apunta a una ruta de `src/app`. */
const tabs = [
  { href: '/', icon: 'home-outline', label: 'Inicio' },
  { href: '/luces', icon: 'bulb-outline', label: 'Luces' },
  { href: '/seguridad', icon: 'shield-checkmark-outline', label: 'Seguridad' },
  { href: '/camara', icon: 'videocam-outline', label: 'Cámara' },
  { href: '/actividad', icon: 'list-outline', label: 'Actividad' },
] as const satisfies readonly { href: Href; icon: TabIcon; label: string }[];

export function TabBar() {
  const colors = useCasaTheme();
  const { bottom } = useSafeAreaInsets();
  const pathname = usePathname();

  return (
    <View style={[styles.tabBar, { bottom: bottom + 12 }]}>
      {tabs.map(({ href, icon, label }) => {
        const activa = pathname === href;
        const color = activa ? colors.teal : colors.ink;
        return (
          <Pressable
            key={href}
            style={({ pressed }) => [styles.tab, activa && styles.tabActive, pressed && !activa && { opacity: 0.6 }]}
            onPress={() => !activa && router.navigate(href)}
            accessibilityRole="tab"
            accessibilityState={{ selected: activa }}
          >
            <Ionicons name={icon} size={22} color={color} />
            <Text style={[styles.tabLabel, { color }, activa && styles.tabLabelActive]} numberOfLines={1}>
              {label}
            </Text>
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
    left: 14,
    right: 14,
    flexDirection: 'row',
    padding: 6,
    borderRadius: 30,
  },
  tab: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: 5, paddingVertical: 10, borderRadius: 22 },
  tabActive: { backgroundColor: 'rgba(31, 127, 120, 0.14)' },
  tabLabel: text('regular', 11),
  tabLabelActive: { fontFamily: 'Inter_600SemiBold' },
});

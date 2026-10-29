import { BlurView } from 'expo-blur';
import type { BottomTabBarProps } from 'expo-router/js-tabs';
import { Pressable, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { Icon, Icons, type IconSource } from './icon';
import { AppText } from './text';

import { FontFamily, Glass, Palette, Radii, Spacing, ZIndex } from '@/theme';

export type TabConfig = {
  label: string;
  icon: IconSource;
};

/** Tab metadata keyed by route name inside the (tabs) group. */
export const TAB_CONFIG: Record<string, TabConfig> = {
  index: { label: 'Inicio', icon: Icons.home },
  luces: { label: 'Luces', icon: Icons.bulb },
  seguridad: { label: 'Seguridad', icon: Icons.shield },
  camara: { label: 'Cámara', icon: Icons.camera },
  actividad: { label: 'Actividad', icon: Icons.activity },
};

/**
 * Floating frosted-glass bottom bar for the expo-router Tabs navigator.
 */
export function TabBar({ state, descriptors, navigation }: BottomTabBarProps) {
  const insets = useSafeAreaInsets();

  return (
    <View style={[styles.container, { paddingBottom: Math.max(insets.bottom, Spacing.two) }]}>
      <BlurView intensity={Glass.blurIntensity} tint="light" style={styles.bar}>
        <View style={styles.barFill} />
        <View style={styles.row}>
          {state.routes.map((route, index) => {
            const config = TAB_CONFIG[route.name];
            const isFocused = state.index === index;
            const options = descriptors[route.key].options;

            if (!config) return null;

            const onPress = () => {
              const event = navigation.emit({
                type: 'tabPress',
                target: route.key,
                canPreventDefault: true,
              });
              if (!isFocused && !event.defaultPrevented) {
                navigation.navigate(route.name);
              }
            };

            return (
              <Pressable
                key={route.key}
                accessibilityRole="tab"
                accessibilityState={isFocused ? { selected: true } : {}}
                accessibilityLabel={options.tabBarAccessibilityLabel ?? config.label}
                onPress={onPress}
                onLongPress={() =>
                  navigation.emit({ type: 'tabLongPress', target: route.key })
                }
                style={({ pressed }) => [styles.item, pressed && styles.itemPressed]}>
                <View style={[styles.itemPill, isFocused && styles.itemPillActive]}>
                  <Icon
                    source={config.icon}
                    size={19}
                    color={isFocused ? Palette.accentInk : Palette.inkMuted}
                    weight={isFocused ? 'semibold' : 'regular'}
                  />
                </View>
                <AppText
                  variant="caption"
                  style={[styles.label, isFocused && styles.labelActive]}
                  numberOfLines={1}>
                  {config.label}
                </AppText>
              </Pressable>
            );
          })}
        </View>
      </BlurView>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    position: 'absolute',
    bottom: 0,
    left: 0,
    right: 0,
    zIndex: ZIndex.tabbar,
    paddingHorizontal: Spacing.three,
    paddingTop: Spacing.one,
  },
  bar: {
    flexDirection: 'row',
    borderRadius: Radii.xl,
    overflow: 'hidden',
    borderWidth: StyleSheet.hairlineWidth,
    borderColor: Glass.border,
    shadowColor: Glass.shadowColor,
    shadowOpacity: 0.18,
    shadowRadius: 18,
    shadowOffset: { width: 0, height: 10 },
    elevation: 8,
    minHeight: 68,
  },
  barFill: {
    position: 'absolute',
    left: 0,
    right: 0,
    top: 0,
    bottom: 0,
    backgroundColor: Glass.fillStrong,
  },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    width: '100%',
    paddingHorizontal: Spacing.one,
    paddingVertical: Spacing.one,
    gap: Spacing.half,
  },
  item: {
    flex: 1,
    alignItems: 'center',
    gap: 1,
    paddingVertical: Spacing.half,
  },
  itemPressed: {
    opacity: 0.7,
  },
  itemPill: {
    width: 44,
    height: 30,
    borderRadius: Radii.pill,
    alignItems: 'center',
    justifyContent: 'center',
  },
  itemPillActive: {
    backgroundColor: 'rgba(8,127,131,0.10)',
  },
  label: {
    color: Palette.inkMuted,
    fontFamily: FontFamily.bodyMedium,
    maxWidth: 72,
  },
  labelActive: {
    color: Palette.accentInk,
  },
});

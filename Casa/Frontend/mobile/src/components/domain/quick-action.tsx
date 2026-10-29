import { Pressable, StyleSheet, View, type StyleProp, type ViewStyle } from 'react-native';

import { Icon, type IconSource } from '../ui/icon';
import { Surface } from '../ui/surface';
import { AppText } from '../ui/text';

import { Palette, Radii, Spacing, ToneColors, type Tone } from '@/theme';

export type QuickActionProps = {
  label: string;
  icon: IconSource;
  tone?: Tone;
  active?: boolean;
  onPress?: () => void;
  style?: StyleProp<ViewStyle>;
};

/** Square quick-action tile (scenes and shortcuts on the home screen). */
export function QuickAction({ label, icon, tone = 'accent', active = false, onPress, style }: QuickActionProps) {
  const tones = ToneColors[tone];
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ selected: active }}
      accessibilityLabel={label}
      onPress={onPress}
      style={({ pressed }) => [styles.wrap, pressed && styles.pressed, style]}>
      <Surface padding={Spacing.two} style={styles.surface}>
        <View style={[styles.iconWrap, { backgroundColor: active ? tones.fg : tones.bg }]}>
          <Icon source={icon} size={18} color={active ? Palette.white : tones.fg} />
        </View>
        <AppText variant="caption" numberOfLines={2} style={styles.label}>
          {label}
        </AppText>
      </Surface>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  wrap: {
    flex: 1,
  },
  pressed: {
    opacity: 0.78,
    transform: [{ scale: 0.98 }],
  },
  surface: {
    alignItems: 'center',
    gap: Spacing.one,
    borderRadius: Radii.lg,
  },
  iconWrap: {
    width: 40,
    height: 40,
    borderRadius: Radii.md,
    alignItems: 'center',
    justifyContent: 'center',
  },
  label: {
    textAlign: 'center',
    color: '#41586E',
  },
});

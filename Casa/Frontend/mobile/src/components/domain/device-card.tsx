import { Pressable, StyleSheet, View, type StyleProp, type ViewStyle } from 'react-native';

import { Icon, Icons, type IconSource } from '../ui/icon';
import { Surface } from '../ui/surface';
import { AppText } from '../ui/text';
import { Toggle } from '../ui/toggle';

import { Palette, Radii, Spacing } from '@/theme';

export type DeviceCardProps = {
  name: string;
  room: string;
  icon?: IconSource;
  /** Device power / light on-off state. */
  on: boolean;
  onToggle?: (value: boolean) => void;
  /** 0–100 intensity, rendered as a level bar when the device is on. */
  level?: number;
  /** Extra label, e.g. "62% · 12 W". */
  detail?: string;
  onPress?: () => void;
  style?: StyleProp<ViewStyle>;
};

/** Control card for a smart device (lights, plugs, sensors…). */
export function DeviceCard({ name, room, icon = Icons.bulb, on, onToggle, level, detail, onPress, style }: DeviceCardProps) {
  return (
    <Surface padding={Spacing.two} style={style}>
      <View style={styles.row}>
        <Pressable
          accessibilityRole={onPress ? 'button' : undefined}
          accessibilityLabel={onPress ? `${name}, ${room}` : undefined}
          onPress={onPress}
          disabled={!onPress}
          style={({ pressed }) => [styles.main, pressed && styles.pressed]}>
          <View style={[styles.iconWrap, on && styles.iconWrapOn]}>
            <Icon source={icon} size={17} color={on ? Palette.white : Palette.inkMuted} />
          </View>
          <View style={styles.textWrap}>
            <AppText variant="callout">{name}</AppText>
            <AppText variant="footnote" color="inkMuted">
              {detail ? `${room} · ${detail}` : room}
            </AppText>
          </View>
        </Pressable>
        <Toggle
          value={on}
          onValueChange={onToggle}
          label={`${name} encendido`}
          disabled={!onToggle}
        />
      </View>
      {level != null && (
        <View style={styles.levelTrack} accessibilityRole="progressbar" accessibilityValue={{ min: 0, max: 100, now: level }}>
          <View
            style={[
              styles.levelFill,
              { width: `${Math.max(0, Math.min(100, level))}%`, backgroundColor: on ? Palette.accent : '#C9D6E2' },
            ]}
          />
        </View>
      )}
    </Surface>
  );
}

const styles = StyleSheet.create({
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.two,
  },
  main: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.two,
  },
  pressed: {
    opacity: 0.75,
  },
  iconWrap: {
    width: 36,
    height: 36,
    borderRadius: Radii.md,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: Palette.neutralSoft,
  },
  iconWrapOn: {
    backgroundColor: Palette.accent,
  },
  textWrap: {
    flex: 1,
    gap: 1,
  },
  levelTrack: {
    height: 6,
    borderRadius: Radii.pill,
    backgroundColor: 'rgba(255,255,255,0.7)',
    marginTop: Spacing.two,
    overflow: 'hidden',
  },
  levelFill: {
    height: '100%',
    borderRadius: Radii.pill,
  },
});

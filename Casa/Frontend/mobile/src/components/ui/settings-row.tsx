import type { ReactNode } from 'react';
import { Pressable, StyleSheet, View, type StyleProp, type ViewStyle } from 'react-native';

import { Icon, Icons, type IconSource } from './icon';
import { AppText } from './text';

import { Radii, Spacing } from '@/theme';

export type SettingsRowProps = {
  label: string;
  description?: string;
  icon?: IconSource;
  /** Right-side content: Toggle, value text, chevron… */
  value?: ReactNode;
  onPress?: () => void;
  style?: StyleProp<ViewStyle>;
};

/** Preference row for settings lists (label + description + trailing control). */
export function SettingsRow({ label, description, icon, value, onPress, style }: SettingsRowProps) {
  const body = (
    <>
      {icon && (
        <View style={styles.iconWrap}>
          <Icon source={icon} size={16} color="#1D5FA8" />
        </View>
      )}
      <View style={styles.textWrap}>
        <AppText variant="callout">{label}</AppText>
        {description ? (
          <AppText variant="footnote" color="inkMuted">
            {description}
          </AppText>
        ) : null}
      </View>
      {value}
      {onPress && !value && <Icon source={Icons.chevronRight} size={14} color="#6C8299" />}
    </>
  );

  if (onPress) {
    return (
      <Pressable
        accessibilityRole="button"
        onPress={onPress}
        style={({ pressed }) => [styles.row, style, pressed && styles.pressed]}>
        {body}
      </Pressable>
    );
  }

  return <View style={[styles.row, style]}>{body}</View>;
}

const styles = StyleSheet.create({
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.two,
    paddingVertical: Spacing.one,
    borderRadius: Radii.md,
  },
  pressed: {
    opacity: 0.7,
  },
  iconWrap: {
    width: 30,
    height: 30,
    borderRadius: Radii.sm,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: 'rgba(191, 217, 242, 0.6)',
  },
  textWrap: {
    flex: 1,
    gap: 1,
  },
});

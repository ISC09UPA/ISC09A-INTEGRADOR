import type { ReactNode } from 'react';
import { Pressable, StyleSheet, View, type StyleProp, type ViewStyle } from 'react-native';

import { Icon, Icons, type IconSource } from './icon';
import { AppText } from './text';

import { Radii, Spacing, ToneColors, type Tone } from '@/theme';

export type InfoRowProps = {
  icon?: IconSource;
  tone?: Tone;
  label: string;
  value?: string;
  /** Node rendered instead of / next to the plain value text. */
  valueNode?: ReactNode;
  description?: string;
  onPress?: () => void;
  showChevron?: boolean;
  /** Right-aligned extra content (chips, toggles…). */
  trailing?: ReactNode;
  style?: StyleProp<ViewStyle>;
};

/** Icon + label + value row, optionally tappable — used inside surfaces and lists. */
export function InfoRow({
  icon,
  tone = 'neutral',
  label,
  value,
  valueNode,
  description,
  onPress,
  showChevron = false,
  trailing,
  style,
}: InfoRowProps) {
  const tones = ToneColors[tone];
  const content = (
    <>
      {icon && (
        <View style={[styles.iconWrap, { backgroundColor: tones.bg }]}>
          <Icon source={icon} size={16} color={tones.fg} />
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
      {valueNode}
      {value ? (
        <AppText variant="bodySemiBold" color="inkSoft" style={styles.value}>
          {value}
        </AppText>
      ) : null}
      {showChevron && <Icon source={Icons.chevronRight} size={14} color="#6C8299" />}
      {trailing}
    </>
  );

  if (onPress) {
    return (
      <Pressable
        accessibilityRole="button"
        onPress={onPress}
        style={({ pressed }) => [styles.row, style, pressed && styles.pressed]}>
        {content}
      </Pressable>
    );
  }

  return <View style={[styles.row, style]}>{content}</View>;
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
  },
  textWrap: {
    flex: 1,
    gap: 1,
  },
  value: {
    textAlign: 'right',
  },
});

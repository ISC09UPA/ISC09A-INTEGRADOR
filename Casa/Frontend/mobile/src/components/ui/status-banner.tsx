import type { ReactNode } from 'react';
import { StyleSheet, View, type StyleProp, type ViewStyle } from 'react-native';

import { Icon, type IconSource } from './icon';
import { AppText } from './text';

import { Radii, Spacing, ToneColors, type Tone } from '@/theme';

export type StatusBannerProps = {
  tone?: Tone;
  icon?: IconSource;
  title: string;
  description?: string;
  /** Small node rendered on the right side (e.g. a Button). */
  action?: ReactNode;
  style?: StyleProp<ViewStyle>;
};

/** Highlighted state strip: house status, alerts, sync feedback… */
export function StatusBanner({ tone = 'accent', icon, title, description, action, style }: StatusBannerProps) {
  const tones = ToneColors[tone];
  return (
    <View
      accessibilityRole="summary"
      style={[styles.banner, { backgroundColor: tones.bg }, style]}>
      {icon && (
        <View style={[styles.iconWrap, { backgroundColor: 'rgba(255,255,255,0.6)' }]}>
          <Icon source={icon} size={18} color={tones.fg} />
        </View>
      )}
      <View style={styles.textWrap}>
        <AppText variant="callout" style={{ color: tones.fg }}>
          {title}
        </AppText>
        {description ? (
          <AppText variant="footnote" style={styles.description}>
            {description}
          </AppText>
        ) : null}
      </View>
      {action}
    </View>
  );
}

const styles = StyleSheet.create({
  banner: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.two,
    paddingVertical: Spacing.two,
    paddingHorizontal: Spacing.three,
    borderRadius: Radii.lg,
  },
  iconWrap: {
    width: 34,
    height: 34,
    borderRadius: Radii.pill,
    alignItems: 'center',
    justifyContent: 'center',
  },
  textWrap: {
    flex: 1,
    gap: 2,
  },
  description: {
    color: '#41586E',
  },
});

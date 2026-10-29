import { StyleSheet, View, type StyleProp, type ViewStyle } from 'react-native';

import { Icon, type IconSource } from './icon';
import { Surface, type SurfaceTone } from './surface';
import { AppText } from './text';

import { Radii, Spacing, ToneColors, type Tone } from '@/theme';

export type MetricProps = {
  label: string;
  value: string;
  unit?: string;
  icon?: IconSource;
  tone?: Tone;
  /** Caption shown under the value, e.g. a trend. */
  caption?: string;
  toneSurface?: SurfaceTone;
  style?: StyleProp<ViewStyle>;
};

/** Compact measurement card (temperature, consumption, humidity…). */
export function Metric({ label, value, unit, icon, tone = 'neutral', caption, toneSurface = 'glass', style }: MetricProps) {
  const tones = ToneColors[tone];
  return (
    <Surface tone={toneSurface} padding={Spacing.two} radius={Radii.lg} style={style}>
      <View style={styles.row}>
        {icon && (
          <View style={[styles.iconWrap, { backgroundColor: tones.bg }]}>
            <Icon source={icon} size={15} color={tones.fg} />
          </View>
        )}
        <AppText variant="caption" color="inkSoft" numberOfLines={1} style={styles.label}>
          {label}
        </AppText>
      </View>
      <View style={styles.valueRow}>
        <AppText variant="titleLarge" numberOfLines={1}>
          {value}
        </AppText>
        {unit ? (
          <AppText variant="footnote" color="inkMuted">
            {unit}
          </AppText>
        ) : null}
      </View>
      {caption ? (
        <AppText variant="caption" style={{ color: tones.fg }}>
          {caption}
        </AppText>
      ) : null}
    </Surface>
  );
}

const styles = StyleSheet.create({
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.one,
    marginBottom: Spacing.one,
  },
  iconWrap: {
    width: 26,
    height: 26,
    borderRadius: Radii.sm,
    alignItems: 'center',
    justifyContent: 'center',
  },
  label: {
    flexShrink: 1,
  },
  valueRow: {
    flexDirection: 'row',
    alignItems: 'baseline',
    gap: Spacing.half,
  },
});

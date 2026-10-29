import { StyleSheet, View, type StyleProp, type ViewStyle } from 'react-native';

import { Icon, type IconSource } from './icon';
import { AppText } from './text';

import { Radii, Spacing, ToneColors, type Tone } from '@/theme';

export type EventItemProps = {
  icon: IconSource;
  tone?: Tone;
  title: string;
  description?: string;
  /** Absolute or relative time label, e.g. "08:24" or "Hace 5 min". */
  time: string;
  style?: StyleProp<ViewStyle>;
};

/** Timeline row for the activity feed. */
export function EventItem({ icon, tone = 'neutral', title, description, time, style }: EventItemProps) {
  const tones = ToneColors[tone];
  return (
    <View style={[styles.row, style]}>
      <View style={[styles.iconWrap, { backgroundColor: tones.bg }]}>
        <Icon source={icon} size={16} color={tones.fg} />
      </View>
      <View style={styles.textWrap}>
        <AppText variant="callout" numberOfLines={1}>
          {title}
        </AppText>
        {description ? (
          <AppText variant="footnote" color="inkMuted" numberOfLines={2}>
            {description}
          </AppText>
        ) : null}
      </View>
      <AppText variant="caption" color="inkMuted">
        {time}
      </AppText>
    </View>
  );
}

const styles = StyleSheet.create({
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.two,
    paddingVertical: Spacing.one,
  },
  iconWrap: {
    width: 30,
    height: 30,
    borderRadius: Radii.pill,
    alignItems: 'center',
    justifyContent: 'center',
  },
  textWrap: {
    flex: 1,
    gap: 1,
  },
});

import { Pressable, StyleSheet, View, type StyleProp, type ViewStyle } from 'react-native';

import { AppText } from './text';

import { Glass, Palette, Radii, Spacing } from '@/theme';

export type SegmentedOption<T extends string = string> = {
  value: T;
  label: string;
};

export type SegmentedProps<T extends string = string> = {
  options: SegmentedOption<T>[];
  value: T;
  onChange?: (value: T) => void;
  /** Accessible group label. */
  label?: string;
  style?: StyleProp<ViewStyle>;
};

/** Glass segmented control for switching scopes/filters. */
export function Segmented<T extends string = string>({
  options,
  value,
  onChange,
  label,
  style,
}: SegmentedProps<T>) {
  return (
    <View
      accessibilityRole="tablist"
      accessibilityLabel={label}
      style={[styles.track, style]}>
      {options.map((option) => {
        const selected = option.value === value;
        return (
          <Pressable
            key={option.value}
            accessibilityRole="tab"
            accessibilityState={{ selected }}
            accessibilityLabel={option.label}
            onPress={() => onChange?.(option.value)}
            style={({ pressed }) => [styles.segment, selected && styles.segmentSelected, pressed && styles.pressed]}>
            <AppText
              variant="caption"
              style={[styles.label, selected && styles.labelSelected]}>
              {option.label}
            </AppText>
          </Pressable>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  track: {
    flexDirection: 'row',
    gap: Spacing.half,
    padding: Spacing.half,
    borderRadius: Radii.pill,
    backgroundColor: Glass.fillSubtle,
    borderWidth: StyleSheet.hairlineWidth,
    borderColor: Glass.border,
  },
  segment: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: Spacing.one + 1,
    paddingHorizontal: Spacing.two,
    borderRadius: Radii.pill,
  },
  segmentSelected: {
    backgroundColor: Palette.white,
    shadowColor: Glass.shadowColor,
    shadowOpacity: 0.18,
    shadowRadius: 6,
    shadowOffset: { width: 0, height: 2 },
    elevation: 2,
  },
  pressed: {
    opacity: 0.75,
  },
  label: {
    color: '#41586E',
    letterSpacing: 0.2,
  },
  labelSelected: {
    color: Palette.accentInk,
  },
});

import { Pressable, StyleSheet, Text, View } from 'react-native';

import { GlassSurface, LightingColors, LightingTypography } from '@/constants/lighting-theme';
import { Spacing } from '@/constants/theme';

export type FilterOption<T extends string> = {
  value: T;
  label: string;
};

type FilterBarProps<T extends string> = {
  options: readonly FilterOption<T>[];
  value: T;
  onChange: (value: T) => void;
};

const BAR_RADIUS = 32;
const OPTION_RADIUS = 24;

/** Filtros en una píldora glass; la opción seleccionada se resalta. */
export function FilterBar<T extends string>({ options, value, onChange }: FilterBarProps<T>) {
  return (
    <View accessibilityRole="tablist" style={styles.bar}>
      {options.map((option) => {
        const selected = option.value === value;
        return (
          <Pressable
            key={option.value}
            accessibilityRole="tab"
            accessibilityLabel={`Filtrar: ${option.label}`}
            accessibilityState={{ selected }}
            onPress={() => onChange(option.value)}
            style={({ pressed }) => [
              styles.option,
              selected && styles.selected,
              pressed && styles.pressed,
            ]}>
            <Text
              numberOfLines={1}
              adjustsFontSizeToFit
              style={[styles.text, selected ? styles.selectedText : styles.unselectedText]}>
              {option.label}
            </Text>
          </Pressable>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  bar: {
    ...GlassSurface,
    borderRadius: BAR_RADIUS,
    flexDirection: 'row',
    padding: Spacing.one,
    gap: Spacing.one,
  },
  option: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 12,
    paddingHorizontal: Spacing.one,
    borderRadius: OPTION_RADIUS,
  },
  selected: {
    backgroundColor: LightingColors.selectedBg,
  },
  pressed: {
    opacity: 0.85,
  },
  text: {
    ...LightingTypography.button,
    fontSize: 15,
  },
  selectedText: {
    color: LightingColors.selectedText,
    fontWeight: '600',
  },
  unselectedText: {
    color: LightingColors.navy,
  },
});

import { SymbolView } from 'expo-symbols';
import { Pressable, StyleSheet, Text, View } from 'react-native';

import {
  GlassSurface,
  LightingColors,
  LightingSizes,
  LightingTypography,
} from '@/constants/lighting-theme';
import { Spacing } from '@/constants/theme';

export type ModeOption<T extends string> = {
  value: T;
  label: string;
};

type ModeSelectorProps<T extends string> = {
  options: readonly ModeOption<T>[];
  value: T;
  onChange: (value: T) => void;
  disabled?: boolean;
};

/** Selector de opciones lado a lado. */
export function ModeSelector<T extends string>({
  options,
  value,
  onChange,
  disabled = false,
}: ModeSelectorProps<T>) {
  return (
    <View style={styles.row}>
      {options.map((option) => {
        const selected = option.value === value;
        return (
          <Pressable
            key={option.value}
            accessibilityRole="button"
            accessibilityLabel={`Modo ${option.label}`}
            accessibilityState={{ selected, disabled }}
            disabled={disabled}
            onPress={() => onChange(option.value)}
            style={({ pressed }) => [
              styles.option,
              selected && styles.selected,
              pressed && styles.pressed,
            ]}>
            {selected && (
              <SymbolView
                name={{ ios: 'checkmark', android: 'check', web: 'check' }}
                size={18}
                weight="semibold"
                tintColor={LightingColors.selectedText}
              />
            )}
            <Text style={[styles.text, selected ? styles.selectedText : styles.unselectedText]}>
              {option.label}
            </Text>
          </Pressable>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  row: {
    flexDirection: 'row',
    gap: Spacing.three,
  },
  option: {
    ...GlassSurface,
    flex: 1,
    height: LightingSizes.segmentHeight,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: Spacing.two,
  },
  selected: {
    backgroundColor: LightingColors.selectedBg,
    borderColor: LightingColors.white,
  },
  pressed: {
    opacity: 0.85,
  },
  text: LightingTypography.button,
  selectedText: {
    color: LightingColors.selectedText,
    fontWeight: '600',
  },
  unselectedText: {
    color: LightingColors.navy,
  },
});

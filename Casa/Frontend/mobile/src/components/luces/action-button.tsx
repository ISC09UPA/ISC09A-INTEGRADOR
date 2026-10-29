import { Pressable, StyleSheet, Text } from 'react-native';

import {
  LightingColors,
  LightingRadii,
  LightingShadow,
  LightingSizes,
  LightingTypography,
} from '@/constants/lighting-theme';
import { Spacing } from '@/constants/theme';

type ActionButtonProps = {
  label: string;
  onPress: () => void;
  disabled?: boolean;
  accessibilityHint?: string;
};

export function ActionButton({
  label,
  onPress,
  disabled = false,
  accessibilityHint,
}: ActionButtonProps) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityHint={accessibilityHint}
      accessibilityState={{ disabled }}
      disabled={disabled}
      onPress={onPress}
      style={({ pressed }) => [
        styles.button,
        disabled ? styles.disabled : styles.enabled,
        pressed && styles.pressed,
      ]}>
      <Text style={[styles.text, disabled ? styles.disabledText : styles.enabledText]}>
        {label}
      </Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  button: {
    height: LightingSizes.actionHeight,
    borderRadius: LightingRadii.card,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: Spacing.four,
  },
  enabled: {
    backgroundColor: LightingColors.accent,
    ...LightingShadow,
  },
  disabled: {
    backgroundColor: LightingColors.disabledBg,
  },
  pressed: {
    opacity: 0.85,
  },
  text: {
    ...LightingTypography.button,
    fontSize: 18,
  },
  enabledText: {
    color: LightingColors.white,
    fontWeight: '600',
  },
  disabledText: {
    color: LightingColors.disabledText,
  },
});

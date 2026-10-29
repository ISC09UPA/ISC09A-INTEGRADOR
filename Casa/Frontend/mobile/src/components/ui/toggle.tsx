import { Platform, Switch } from 'react-native';

import { Palette } from '@/theme';

export type ToggleProps = {
  value: boolean;
  onValueChange?: (value: boolean) => void;
  label?: string;
  disabled?: boolean;
};

/** Brand-tinted switch with accessible labeling. */
export function Toggle({ value, onValueChange, label, disabled = false }: ToggleProps) {
  return (
    <Switch
      value={value}
      onValueChange={onValueChange}
      disabled={disabled}
      accessibilityLabel={label}
      accessibilityRole="switch"
      accessibilityState={{ checked: value, disabled }}
      trackColor={{ false: '#65747B', true: Palette.accent }}
      thumbColor={Platform.OS === 'android' ? Palette.white : undefined}
      ios_backgroundColor="#65747B"
    />
  );
}

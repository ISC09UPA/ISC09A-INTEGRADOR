import { ActivityIndicator, Pressable, StyleSheet, type StyleProp, type ViewStyle } from 'react-native';

import { Icon, type IconSource } from './icon';
import { AppText } from './text';

import { FontFamily, Palette, Radii, Spacing } from '@/theme';

export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger';
export type ButtonSize = 'sm' | 'md' | 'lg';

export type ButtonProps = {
  label: string;
  onPress?: () => void;
  variant?: ButtonVariant;
  size?: ButtonSize;
  icon?: IconSource;
  iconRight?: IconSource;
  loading?: boolean;
  disabled?: boolean;
  fullWidth?: boolean;
  style?: StyleProp<ViewStyle>;
};

const heightBySize: Record<ButtonSize, number> = { sm: 34, md: 46, lg: 54 };
const fontSizeBySize: Record<ButtonSize, number> = { sm: 13, md: 15, lg: 16 };

type VariantStyle = {
  container: ViewStyle;
  labelColor: string;
};

const variants: Record<ButtonVariant, VariantStyle> = {
  primary: {
    container: { backgroundColor: Palette.accent },
    labelColor: Palette.white,
  },
  secondary: {
    container: {
      backgroundColor: 'rgba(255,255,255,0.72)',
      borderWidth: StyleSheet.hairlineWidth,
      borderColor: 'rgba(255,255,255,0.9)',
    },
    labelColor: Palette.accentInk,
  },
  ghost: {
    container: { backgroundColor: 'transparent' },
    labelColor: Palette.accentInk,
  },
  danger: {
    container: { backgroundColor: Palette.danger },
    labelColor: Palette.white,
  },
};

/** Primary action control with glass-friendly variants. */
export function Button({
  label,
  onPress,
  variant = 'primary',
  size = 'md',
  icon,
  iconRight,
  loading = false,
  disabled = false,
  fullWidth = false,
  style,
}: ButtonProps) {
  const v = variants[variant];
  const isDisabled = disabled || loading;
  const iconSize = size === 'lg' ? 18 : 16;

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityState={{ disabled: isDisabled, busy: loading }}
      disabled={isDisabled}
      onPress={onPress}
      style={({ pressed }) => [
        styles.base,
        { height: heightBySize[size] },
        v.container,
        fullWidth && styles.fullWidth,
        isDisabled && styles.disabled,
        pressed && !isDisabled && styles.pressed,
        style,
      ]}>
      {loading ? (
        <ActivityIndicator size="small" color={v.labelColor} />
      ) : (
        <>
          {icon && <Icon source={icon} size={iconSize} color={v.labelColor} />}
          <AppText
            style={{
              fontFamily: FontFamily.bodySemiBold,
              fontSize: fontSizeBySize[size],
              color: v.labelColor,
            }}>
            {label}
          </AppText>
          {iconRight && <Icon source={iconRight} size={iconSize} color={v.labelColor} />}
        </>
      )}
    </Pressable>
  );
}

export type IconButtonProps = {
  label: string;
  icon: IconSource;
  onPress?: () => void;
  size?: ButtonSize;
  variant?: ButtonVariant;
  disabled?: boolean;
  style?: StyleProp<ViewStyle>;
};

/** Square icon-only button (glass secondary style). */
export function IconButton({
  label,
  icon,
  onPress,
  size = 'md',
  variant = 'secondary',
  disabled = false,
  style,
}: IconButtonProps) {
  const v = variants[variant];
  const dimension = heightBySize[size];
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityState={{ disabled }}
      disabled={disabled}
      onPress={onPress}
      style={({ pressed }) => [
        styles.iconBase,
        { width: dimension, height: dimension },
        v.container,
        disabled && styles.disabled,
        pressed && !disabled && styles.pressed,
        style,
      ]}>
      <Icon source={icon} size={size === 'lg' ? 20 : 17} color={v.labelColor} />
    </Pressable>
  );
}

const styles = StyleSheet.create({
  base: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: Spacing.one,
    paddingHorizontal: Spacing.three,
    borderRadius: Radii.pill,
  },
  fullWidth: {
    alignSelf: 'stretch',
  },
  disabled: {
    opacity: 0.45,
  },
  pressed: {
    opacity: 0.82,
    transform: [{ scale: 0.985 }],
  },
  iconBase: {
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: Radii.pill,
  },
});

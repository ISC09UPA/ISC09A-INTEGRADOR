import { useState } from 'react';
import { StyleSheet, TextInput, View, type StyleProp, type TextInputProps, type ViewStyle } from 'react-native';

import { AppText } from './text';

import { FontFamily, FontSize, Glass, Palette, Radii, Spacing } from '@/theme';

export type TextFieldProps = Omit<TextInputProps, 'style'> & {
  label?: string;
  error?: string;
  containerStyle?: StyleProp<ViewStyle>;
};

/** Labeled glass text input with focus and error states. */
export function TextField({
  label,
  error,
  placeholderTextColor,
  containerStyle,
  ...rest
}: TextFieldProps) {
  const [focused, setFocused] = useState(false);

  return (
    <View style={[styles.wrapper, containerStyle]}>
      {label ? (
        <AppText variant="caption" color="inkSoft" style={styles.label}>
          {label}
        </AppText>
      ) : null}
      <View
        style={[
          styles.field,
          focused && styles.fieldFocused,
          error != null && error.length > 0 && styles.fieldError,
        ]}>
        <TextInput
          {...rest}
          accessibilityLabel={rest.accessibilityLabel ?? label}
          accessibilityHint={error || rest.accessibilityHint}
          placeholderTextColor={placeholderTextColor ?? '#8AA0B4'}
          onFocus={(e) => {
            setFocused(true);
            rest.onFocus?.(e);
          }}
          onBlur={(e) => {
            setFocused(false);
            rest.onBlur?.(e);
          }}
          style={styles.input}
        />
      </View>
      {error ? (
        <AppText variant="caption" color="danger">
          {error}
        </AppText>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  wrapper: {
    gap: Spacing.half,
  },
  label: {
    marginLeft: Spacing.one,
  },
  field: {
    borderRadius: Radii.md,
    borderWidth: 1,
    borderColor: Glass.border,
    backgroundColor: Glass.fillStrong,
    paddingHorizontal: Spacing.three,
    minHeight: 46,
    justifyContent: 'center',
  },
  fieldFocused: {
    borderColor: Palette.accent,
    shadowColor: Palette.accent,
    shadowOpacity: 0.18,
    shadowRadius: 6,
    shadowOffset: { width: 0, height: 2 },
    elevation: 2,
  },
  fieldError: {
    borderColor: Palette.danger,
  },
  input: {
    fontFamily: FontFamily.body,
    fontSize: FontSize.body,
    color: Palette.ink,
    paddingVertical: Spacing.two,
    width: '100%',
  },
});

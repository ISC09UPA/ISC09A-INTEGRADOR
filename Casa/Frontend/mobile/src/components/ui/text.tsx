import type { StyleProp, TextProps, TextStyle } from 'react-native';
import { Text as RNText } from 'react-native';

import { FontFamily, FontSize, LineHeight, Palette } from '@/theme';

export type TextVariant =
  | 'displayLarge'
  | 'display'
  | 'titleLarge'
  | 'title'
  | 'headline'
  | 'callout'
  | 'body'
  | 'bodyMedium'
  | 'bodySemiBold'
  | 'footnote'
  | 'caption';

export type AppTextColor = 'ink' | 'inkSoft' | 'inkMuted' | 'accent' | 'success' | 'warning' | 'danger' | 'white';

type VariantStyle = Pick<TextStyle, 'fontFamily' | 'fontSize' | 'lineHeight'>;

const variants: Record<TextVariant, VariantStyle> = {
  displayLarge: {
    fontFamily: FontFamily.displayBold,
    fontSize: FontSize.displayLarge,
    lineHeight: FontSize.displayLarge * LineHeight.tight,
  },
  display: {
    fontFamily: FontFamily.displayBold,
    fontSize: FontSize.display,
    lineHeight: FontSize.display * LineHeight.tight,
  },
  titleLarge: {
    fontFamily: FontFamily.displayBold,
    fontSize: FontSize.titleLarge,
    lineHeight: FontSize.titleLarge * LineHeight.tight,
  },
  title: {
    fontFamily: FontFamily.displaySemiBold,
    fontSize: FontSize.title,
    lineHeight: FontSize.title * LineHeight.snug,
  },
  headline: {
    fontFamily: FontFamily.bodySemiBold,
    fontSize: FontSize.headline,
    lineHeight: FontSize.headline * LineHeight.snug,
  },
  callout: {
    fontFamily: FontFamily.bodyMedium,
    fontSize: FontSize.callout,
    lineHeight: FontSize.callout * LineHeight.snug,
  },
  body: {
    fontFamily: FontFamily.body,
    fontSize: FontSize.body,
    lineHeight: FontSize.body * LineHeight.relaxed,
  },
  bodyMedium: {
    fontFamily: FontFamily.bodyMedium,
    fontSize: FontSize.body,
    lineHeight: FontSize.body * LineHeight.relaxed,
  },
  bodySemiBold: {
    fontFamily: FontFamily.bodySemiBold,
    fontSize: FontSize.body,
    lineHeight: FontSize.body * LineHeight.relaxed,
  },
  footnote: {
    fontFamily: FontFamily.body,
    fontSize: FontSize.footnote,
    lineHeight: FontSize.footnote * LineHeight.snug,
  },
  caption: {
    fontFamily: FontFamily.bodyMedium,
    fontSize: FontSize.caption,
    lineHeight: FontSize.caption * LineHeight.snug,
  },
};

const colors: Record<AppTextColor, string> = {
  ink: Palette.ink,
  inkSoft: Palette.inkSoft,
  inkMuted: Palette.inkMuted,
  accent: Palette.accentInk,
  success: Palette.success,
  warning: Palette.warning,
  danger: Palette.danger,
  white: Palette.white,
};

export type AppTextProps = TextProps & {
  variant?: TextVariant;
  color?: AppTextColor;
  style?: StyleProp<TextStyle>;
};

/** Typography primitive that always uses the loaded Manrope/Inter families. */
export function AppText({ variant = 'body', color = 'ink', style, ...rest }: AppTextProps) {
  return <RNText {...rest} style={[variants[variant], { color: colors[color] }, style]} />;
}

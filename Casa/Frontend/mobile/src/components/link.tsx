import { Pressable, StyleSheet, Text } from 'react-native';
import { Feather } from '@expo/vector-icons';
import { text } from '@/constants/casa-theme';
import { useCasaTheme } from '@/hooks/use-casa-theme';
import type { StyledProps } from './card';

export interface LinkProps extends StyledProps {
  size?: number;
  /** Muestra la flecha a la derecha del texto. */
  arrow?: boolean;
  onPress?: () => void;
}

export function Link({ children, style, size = 16, arrow = true, onPress }: LinkProps) {
  const colors = useCasaTheme();

  return (
    <Pressable style={({ pressed }) => [styles.linkRow, style, pressed && { opacity: 0.6 }]} hitSlop={8} onPress={onPress}>
      <Text style={[styles.link, { fontSize: size }]}>{children}</Text>
      {arrow && <Feather name="arrow-right" size={size} color={colors.teal} />}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  linkRow: { flexDirection: 'row', alignItems: 'center', alignSelf: 'flex-start', gap: 8 },
  link: text('medium', 16, 'teal'),
});

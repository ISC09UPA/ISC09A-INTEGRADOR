import { StyleSheet, Text, TextInput, View, type TextInputProps } from 'react-native';
import { colors, font, radius } from '@/constants/tokens';

export default function Campo({ label, ...props }: { label: string } & TextInputProps) {
  return (
    <View style={{ gap: 6 }}>
      <Text style={s.label}>{label}</Text>
      <TextInput
        placeholderTextColor={colors.textoSuave}
        style={s.input}
        autoCapitalize="none"
        {...props}
      />
    </View>
  );
}

const s = StyleSheet.create({
  label: { fontFamily: font.medium, fontSize: 13, color: colors.textoSuave },
  input: {
    backgroundColor: colors.superficie, borderWidth: 1, borderColor: colors.borde,
    borderRadius: radius.md, paddingHorizontal: 16, paddingVertical: 14,
    fontFamily: font.regular, fontSize: 16, color: colors.texto,
  },
});

import { useState, type ReactNode } from 'react';
import { RefreshControl, ScrollView, StyleSheet } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { colors } from '@/constants/tokens';

interface Props {
  children: ReactNode;
  onRefrescar?: () => Promise<void>;
}

export default function Pantalla({ children, onRefrescar }: Props) {
  const [refrescando, setRefrescando] = useState(false);

  const refrescar = async () => {
    setRefrescando(true);
    await onRefrescar?.();
    setRefrescando(false);
  };

  return (
    <SafeAreaView style={s.safe} edges={['top']}>
      <ScrollView
        contentContainerStyle={s.contenido}
        showsVerticalScrollIndicator={false}
        keyboardShouldPersistTaps="handled"
        refreshControl={
          onRefrescar ? (
            <RefreshControl refreshing={refrescando} onRefresh={refrescar} tintColor={colors.verdeOscuro} />
          ) : undefined
        }
      >
        {children}
      </ScrollView>
    </SafeAreaView>
  );
}

const s = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.fondo },
  contenido: { padding: 20, paddingBottom: 32, gap: 16 },
});

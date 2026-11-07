import { useState } from 'react';
import { ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { Background } from '@/components/background';
import { BrandLabel } from '@/components/brand-label';
import { CameraPreview } from '@/components/camera-preview';
import { CaptureCard } from '@/components/capture-card';
import { ErrorNotice } from '@/components/error-notice';
import { Link } from '@/components/link';
import { PrimaryButton } from '@/components/primary-button';
import { SectionLabel } from '@/components/section-label';
import { camara, nuevaCaptura, type Captura } from '@/constants/casa';
import { text } from '@/constants/casa-theme';

export default function CamaraScreen() {
  const insets = useSafeAreaInsets();
  const [enVivo, setEnVivo] = useState(true);
  const [capturas, setCapturas] = useState<readonly Captura[]>(camara.capturas);

  const tomarCaptura = () => setCapturas((c) => [nuevaCaptura(camara.nombre), ...c]);
  const analizar = (id: string) => () =>
    setCapturas((c) => c.map((cap) => (cap.id === id ? { ...cap, analisis: 'solicitado' } : cap)));

  return (
    <View style={styles.root}>
      <Background />

      <ScrollView
        contentContainerStyle={[styles.content, { paddingTop: insets.top + 24, paddingBottom: insets.bottom + 120 }]}
        showsVerticalScrollIndicator={false}
      >
        <BrandLabel />
        <Text style={styles.title}>Cámara</Text>
        <Text style={styles.subtitle}>{camara.nombre} · sesión privada</Text>

        <View style={styles.preview}>
          <CameraPreview enVivo={enVivo} />
        </View>

        <Link size={15} arrow={false} style={styles.session} onPress={() => setEnVivo((v) => !v)}>
          {enVivo ? 'Terminar sesión' : 'Iniciar sesión'}
        </Link>

        <PrimaryButton label="Tomar y guardar captura" onPress={tomarCaptura} disabled={!enVivo} />

        <SectionLabel>Capturas privadas</SectionLabel>

        {capturas.map((captura) => (
          <CaptureCard key={captura.id} captura={captura} onAnalizar={analizar(captura.id)} />
        ))}

        {camara.aviso && <ErrorNotice>{camara.aviso}</ErrorNotice>}
      </ScrollView>
    </View>
  );
}

const styles = StyleSheet.create({
  root: { flex: 1 },
  content: { paddingHorizontal: 22 },
  title: { ...text('semibold', 28), letterSpacing: -0.5, marginTop: 18 },
  subtitle: { ...text('regular', 14, 'muted'), marginTop: 6 },
  preview: { marginTop: 22 },
  session: { marginTop: 22, marginBottom: 18 },
});

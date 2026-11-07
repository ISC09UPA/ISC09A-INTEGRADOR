import { StyleSheet, Text } from 'react-native';
import { text } from '@/constants/casa-theme';
import { textoAnalisis, type Captura } from '@/constants/casa';
import { Card } from './card';
import { Link } from './link';

export interface CaptureCardProps {
  captura: Captura;
  onAnalizar?: () => void;
}

/** Tarjeta de una captura privada guardada. */
export function CaptureCard({ captura, onAnalizar }: CaptureCardProps) {
  return (
    <Card style={styles.card}>
      <Text style={styles.title}>
        {captura.fecha}
        <Text style={styles.sep}>  ·  </Text>
        {captura.camara}
      </Text>
      <Text style={styles.status}>{textoAnalisis[captura.analisis]}</Text>
      {captura.analisis === 'no-iniciado' && (
        <Link size={14} style={styles.link} onPress={onAnalizar}>
          Analizar imagen
        </Link>
      )}
    </Card>
  );
}

const styles = StyleSheet.create({
  card: { borderRadius: 22, paddingHorizontal: 16, paddingVertical: 16 },
  title: text('regular', 15),
  sep: { color: '#3A4652' },
  status: { ...text('regular', 14, 'muted'), marginTop: 10 },
  link: { marginTop: 10, gap: 6 },
});

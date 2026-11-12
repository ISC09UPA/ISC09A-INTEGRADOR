import { ActivityIndicator, ScrollView, Text, View } from 'react-native';

import { Card } from '@/components/ui/Card';
import { Chip } from '@/components/ui/Chip';
import { useTema } from './useTema';

interface Props {
  texto: string;
  /** true mientras el texto todavía se está reconociendo (no es definitivo). */
  enProgreso?: boolean;
  /** true si el texto es de prueba y no proviene de un reconocimiento real. */
  simulada?: boolean;
  alturaMaxima?: number;
}

export function Transcripcion({ texto, enProgreso = false, simulada = false, alturaMaxima = 180 }: Props) {
  const tema = useTema();
  const vacio = texto.trim().length === 0;

  return (
    <Card tono="bajo" style={{ gap: 8 }}>
      <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }}>
        <Text style={{ fontSize: 12, fontWeight: '600', letterSpacing: 0.5, color: tema.onSurfaceVariant }}>
          {enProgreso ? 'TRANSCRIPCIÓN EN VIVO' : 'TRANSCRIPCIÓN'}
        </Text>
        {simulada ? <Chip variante="advertencia" texto="Simulada" /> : null}
      </View>

      <ScrollView style={{ maxHeight: alturaMaxima }} showsVerticalScrollIndicator={false}>
        {vacio ? (
          <Text style={{ fontSize: 15, fontStyle: 'italic', color: tema.onSurfaceVariant }}>
            {enProgreso ? 'Esperando voz…' : 'No se obtuvo texto de esta grabación.'}
          </Text>
        ) : (
          <Text
            style={{
              fontSize: 16,
              lineHeight: 24,
              // El texto provisional se muestra atenuado y en cursiva: aún no es definitivo.
              color: enProgreso ? tema.onSurfaceVariant : tema.onSurface,
              fontStyle: enProgreso ? 'italic' : 'normal',
            }}
          >
            {texto}
            {enProgreso ? ' …' : ''}
          </Text>
        )}
      </ScrollView>

      {enProgreso ? (
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: 6 }}>
          <ActivityIndicator size="small" color={tema.onSurfaceVariant} />
          <Text style={{ fontSize: 12, color: tema.onSurfaceVariant }}>Reconociendo…</Text>
        </View>
      ) : null}
    </Card>
  );
}

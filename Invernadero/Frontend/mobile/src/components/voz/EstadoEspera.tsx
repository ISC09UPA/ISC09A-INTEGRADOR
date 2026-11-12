import type { ComponentProps } from 'react';
import { Text, View } from 'react-native';
import { MaterialIcons } from '@expo/vector-icons';

import { useTema } from './useTema';

const PASOS: { icono: ComponentProps<typeof MaterialIcons>['name']; texto: string }[] = [
  { icono: 'touch-app', texto: 'Mantén presionado el botón del micrófono' },
  { icono: 'record-voice-over', texto: 'Habla mientras lo mantienes presionado' },
  { icono: 'pan-tool', texto: 'Suelta para terminar la grabación' },
  { icono: 'arrow-upward', texto: 'Desliza hacia arriba y suelta para cancelar' },
];

export function EstadoEspera() {
  const tema = useTema();

  return (
    <View style={{ alignItems: 'center', gap: 20 }}>
      <View
        style={{
          width: 110,
          height: 110,
          borderRadius: 55,
          alignItems: 'center',
          justifyContent: 'center',
          backgroundColor: tema.primaryContainer,
        }}
      >
        <MaterialIcons name="mic" size={56} color={tema.primary} />
      </View>

      <View style={{ alignItems: 'center', gap: 6 }}>
        <Text style={{ fontSize: 22, fontWeight: '600', textAlign: 'center', color: tema.onSurface }}>
          Presiona y mantén para hablar
        </Text>
        <Text style={{ fontSize: 14, textAlign: 'center', color: tema.onSurfaceVariant }}>
          Usa el botón del micrófono de la barra inferior, desde cualquier pestaña.
        </Text>
      </View>

      <View
        style={{
          alignSelf: 'stretch',
          gap: 12,
          padding: 16,
          borderRadius: 16,
          backgroundColor: tema.surfaceContainerLow,
        }}
      >
        {PASOS.map((paso) => (
          <View key={paso.texto} style={{ flexDirection: 'row', alignItems: 'center', gap: 12 }}>
            <MaterialIcons name={paso.icono} size={22} color={tema.primary} />
            <Text style={{ flex: 1, fontSize: 14, color: tema.onSurface }}>{paso.texto}</Text>
          </View>
        ))}
      </View>
    </View>
  );
}

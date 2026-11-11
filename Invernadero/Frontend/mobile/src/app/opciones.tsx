
import { useState } from 'react';
import { View, Text, Pressable } from 'react-native';
import { MaterialIcons } from '@expo/vector-icons';
import { useColorScheme } from 'nativewind';
import { colores } from '@/theme/tokens';

type Tema = 'light' | 'dark' | 'system';

export default function OpcionesScreen() {
  const { colorScheme, setColorScheme } = useColorScheme();
  const [seleccion, setSeleccion] = useState<Tema>('system');

  const tema = colorScheme === 'dark' ? colores.oscuro : colores.claro;

  const opciones: {
    valor: Tema;
    titulo: string;
    descripcion: string;
    icono: keyof typeof MaterialIcons.glyphMap;
  }[] = [
    {
      valor: 'light',
      titulo: 'Claro',
      descripcion: 'Utilizar siempre el tema claro',
      icono: 'light-mode',
    },
    {
      valor: 'dark',
      titulo: 'Oscuro',
      descripcion: 'Utilizar siempre el tema oscuro',
      icono: 'dark-mode',
    },
    {
      valor: 'system',
      titulo: 'Automático',
      descripcion: 'Seguir la configuración del dispositivo',
      icono: 'settings-brightness',
    },
  ];

  function cambiarTema(valor: Tema) {
    setSeleccion(valor);
    setColorScheme(valor);
  }

  return (
    <View
      className="flex-1 px-gutter-mobile pt-8"
      style={{ backgroundColor: tema.surface }}
    >
      <Text className="text-headline-lg" style={{ color: tema.onSurface }}>
        Opciones
      </Text>

      <Text
        className="text-body-md mt-2 mb-6"
        style={{ color: tema.onSurfaceVariant }}
      >
        Personaliza la apariencia de RiegoSmart IA.
      </Text>

      <View
        className="rounded-xl p-4"
        style={{ backgroundColor: tema.surfaceContainer }}
      >
        <Text
          className="text-title-md mb-1"
          style={{ color: tema.onSurface }}
        >
          Apariencia
        </Text>

        <Text
          className="text-body-sm mb-4"
          style={{ color: tema.onSurfaceVariant }}
        >
          Selecciona el tema de la aplicación.
        </Text>

        {opciones.map((opcion) => {
          const seleccionado = seleccion === opcion.valor;

          return (
            <Pressable
              key={opcion.valor}
              onPress={() => cambiarTema(opcion.valor)}
              accessibilityRole="radio"
              accessibilityState={{ checked: seleccionado }}
              className="flex-row items-center rounded-xl p-3 mb-2"
              style={{
                backgroundColor: seleccionado
                  ? tema.primaryContainer
                  : tema.surface,
                borderWidth: seleccionado ? 1.5 : 1,
                borderColor: seleccionado
                  ? tema.primary
                  : tema.outlineVariant,
              }}
            >
              <MaterialIcons
                name={opcion.icono}
                size={24}
                color={seleccionado ? tema.primary : tema.onSurfaceVariant}
              />

              <View className="flex-1 ml-3">
                <Text
                  className="text-body-md"
                  style={{ color: tema.onSurface, fontWeight: '600' }}
                >
                  {opcion.titulo}
                </Text>

                <Text
                  className="text-body-sm"
                  style={{ color: tema.onSurfaceVariant }}
                >
                  {opcion.descripcion}
                </Text>
              </View>

              {seleccionado && (
                <MaterialIcons
                  name="check-circle"
                  size={22}
                  color={tema.primary}
                />
              )}
            </Pressable>
          );
        })}
      </View>
    </View>
  );
}

import { View, type ViewProps } from 'react-native';
import { useColorScheme } from 'nativewind';
import { colores } from '@/theme/tokens';

interface Props extends ViewProps {
  elevada?: boolean;
  tono?: 'blanco' | 'bajo';
  className?: string;
}

export function Card({
  elevada = false,
  tono = 'blanco',
  className = '',
  children,
  style,
  ...resto
}: Props) {
  const { colorScheme } = useColorScheme();
  const tema = colorScheme === 'dark' ? colores.oscuro : colores.claro;

  const fondo =
    tono === 'blanco'
      ? tema.surfaceContainerLowest
      : tema.surfaceContainerLow;

  return (
    <View
      className={`rounded-2xl p-space-md ${className}`}
      style={[
        { backgroundColor: fondo },
        elevada
          ? {
              shadowColor: '#000',
              shadowOpacity: 0.1,
              shadowRadius: 6,
              elevation: 4,
            }
          : {
              shadowColor: '#000',
              shadowOpacity: 0.05,
              shadowRadius: 3,
              elevation: 2,
            },
        style,
      ]}
      {...resto}
    >
      {children}
    </View>
  );
}
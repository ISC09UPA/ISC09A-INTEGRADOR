
import { ActivityIndicator, Pressable, Text } from 'react-native';
import { useColorScheme } from 'nativewind';
import { colores } from '@/theme/tokens';

interface PropsBoton extends React.ComponentProps<typeof Pressable> {
  variante?: 'primario' | 'secundario' | 'peligroso';
  texto: string;
  deshabilitado?: boolean;
  cargando?: boolean;
}

export function Boton({
  variante = 'primario',
  texto,
  deshabilitado,
  cargando,
  onPress,
  className = '',
  ...resto
}: PropsBoton) {
  const { colorScheme } = useColorScheme();
  const tema = colorScheme === 'dark' ? colores.oscuro : colores.claro;

  const activo = !deshabilitado && !cargando;

  const fondos = {
    primario: tema.primary,
    secundario: tema.surfaceContainer,
    peligroso: tema.error,
  };

  const textos = {
    primario: tema.onPrimary,
    secundario: tema.onSurface,
    peligroso: tema.onError,
  };

  return (
    <Pressable
      onPress={onPress}
      disabled={!activo}
      className={`h-12 flex-row items-center justify-center gap-2 rounded-xl px-4 min-h-[48px] ${activo ? 'active:opacity-90' : 'opacity-50'} ${className}`}
      style={{ backgroundColor: fondos[variante] }}
      {...resto}
    >
      {cargando && (
        <ActivityIndicator
          size="small"
          color={textos[variante]}
        />
      )}

      <Text
        className="text-[14px] leading-5 font-semibold"
        style={{ color: textos[variante] }}
      >
        {texto}
      </Text>
    </Pressable>
  );
}

export function BotonPill({
  texto,
  onPress,
  deshabilitado,
  tono = 'secundario',
}: {
  texto: string;
  onPress?: () => void;
  deshabilitado?: boolean;
  tono?: 'primario' | 'secundario';
}) {
  const { colorScheme } = useColorScheme();
  const tema = colorScheme === 'dark' ? colores.oscuro : colores.claro;

  const fondo = tono === 'primario' ? tema.primary : tema.secondary;
  const colorTexto = tono === 'primario' ? tema.onPrimary : tema.onSecondary;

  return (
    <Pressable
      onPress={onPress}
      disabled={deshabilitado}
      className={`flex-row items-center gap-1 rounded-full px-3 py-1.5 min-h-[32px] ${deshabilitado ? 'opacity-50' : 'active:scale-95'}`}
      style={{ backgroundColor: fondo }}
      accessibilityRole="button"
    >
      <Text
        className="text-[11px] leading-[14px] font-medium"
        style={{ color: colorTexto }}
      >
        {texto}
      </Text>
    </Pressable>
  );
}

import { Pressable, Text, type ViewProps } from 'react-native';
import { useColorScheme } from 'nativewind';
import { colores } from '@/theme/tokens';

type Variante =
  | 'primario'
  | 'neutro'
  | 'contenedor'
  | 'exito'
  | 'advertencia'
  | 'error';

interface ChipProps extends ViewProps {
  variante?: Variante;
  texto: string;
  conteo?: number;
  activo?: boolean;
  onPress?: () => void;
  className?: string;
}

export function Chip({
  variante = 'neutro',
  texto,
  conteo,
  activo,
  onPress,
  className = '',
}: ChipProps) {
  const { colorScheme } = useColorScheme();
  const tema = colorScheme === 'dark' ? colores.oscuro : colores.claro;

  const v: Variante = activo ? 'primario' : variante;

  const estilos: Record<Variante, { fondo: string; texto: string }> = {
    primario: { fondo: tema.primary, texto: tema.onPrimary },
    neutro: {
      fondo: tema.surfaceContainerLowest,
      texto: tema.onSurfaceVariant,
    },
    contenedor: {
      fondo: tema.surfaceContainer,
      texto: tema.onSurface,
    },
    exito: {
      fondo: tema.primaryContainer,
      texto: tema.onPrimaryContainer,
    },
    advertencia: {
      fondo: tema.tertiaryContainer,
      texto: tema.onTertiaryContainer,
    },
    error: {
      fondo: tema.errorContainer,
      texto: tema.onErrorContainer,
    },
  };

  return (
    <Pressable
      onPress={onPress}
      disabled={!onPress}
      className={`flex-row items-center gap-1 rounded-full px-3 py-1.5 shadow-sm min-h-[36px] ${className}`}
      style={{ backgroundColor: estilos[v].fondo }}
    >
      <Text
        className="text-[12px] leading-4 font-medium"
        style={{ color: estilos[v].texto }}
      >
        {texto}
      </Text>

      {conteo !== undefined && (
        <Text
          className="rounded-full px-1.5 text-[11px] font-semibold"
          style={{ color: estilos[v].texto }}
        >
          {conteo}
        </Text>
      )}
    </Pressable>
  );
}
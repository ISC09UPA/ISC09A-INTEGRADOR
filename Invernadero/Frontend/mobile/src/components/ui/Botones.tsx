import { ActivityIndicator, Pressable, Text } from 'react-native';
import { colores } from '@/theme/tokens';

interface PropsBase {
  texto: string;
  onPress?: () => void;
  deshabilitado?: boolean;
  cargando?: boolean;
  /** Icono Material opcional. */
  icono?: string;
}

interface PropsBoton extends React.ComponentProps<typeof Pressable> {
  variante?: 'primario' | 'secundario' | 'peligroso';
  texto: string;
  deshabilitado?: boolean;
  cargando?: boolean;
}

const FONDOS = {
  primario: 'bg-primary',
  secundario: 'bg-surface-container',
  peligroso: 'bg-error',
};
const TEXTOS = {
  primario: 'text-on-primary',
  secundario: 'text-on-surface',
  peligroso: 'text-on-error',
};

/** Botón sólido con radio 16px, h≥48px, háptico y sin doble envío visual. */
export function Boton({ variante = 'primario', texto, deshabilitado, cargando, onPress, className = '', ...resto }: PropsBoton) {
  const activo = !deshabilitado && !cargando;
  return (
    <Pressable
      onPress={onPress}
      disabled={!activo}
      className={`h-12 flex-row items-center justify-center gap-2 rounded-xl px-4 min-h-[48px] ${FONDOS[variante]} ${activo ? 'active:opacity-90' : 'opacity-50'} ${className}`}
      {...resto}
    >
      {cargando && <ActivityIndicator size="small" color={variante === 'secundario' ? colores.onSurface : colores.onPrimary} />}
      <Text className={`text-[14px] leading-5 font-semibold ${TEXTOS[variante]}`}>{texto}</Text>
    </Pressable>
  );
}

/** Botón pequeño tipo "Regar 180 mL" (pill compacta). */
export function BotonPill({
  texto,
  onPress,
  deshabilitado,
  tono = 'secundario',
}: PropsBase & { tono?: 'primario' | 'secundario' }) {
  const fondo = tono === 'primario' ? 'bg-primary' : 'bg-secondary';
  const textoClase = tono === 'primario' ? 'text-on-primary' : 'text-on-secondary';
  return (
    <Pressable
      onPress={onPress}
      disabled={deshabilitado}
      className={`flex-row items-center gap-1 rounded-full px-3 py-1.5 min-h-[32px] ${fondo} ${deshabilitado ? 'opacity-50' : 'active:scale-95'}`}
      accessibilityRole="button"
    >
      <Text className={`text-[11px] leading-[14px] font-medium ${textoClase}`}>{texto}</Text>
    </Pressable>
  );
}

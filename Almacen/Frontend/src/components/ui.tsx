import MaterialIcons from '@expo/vector-icons/MaterialIcons';
import { useState, type ComponentProps, type ReactNode } from 'react';
import {
  ActivityIndicator,
  Alert,
  Modal,
  Platform,
  Pressable,
  RefreshControl,
  ScrollView,
  StyleSheet,
  Switch,
  Text,
  TextInput,
  View,
  type StyleProp,
  type TextInputProps,
  type TextStyle,
  type ViewStyle,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { MaxContentWidth, Radio, Spacing, type Paleta } from '@/constants/theme';
import { useColores } from '@/hooks/use-colores';
import type { ColorEstado } from '@/lib/formato';

export type NombreIcono = ComponentProps<typeof MaterialIcons>['name'];

export function Icono({ nombre, tamano = 22, color }: { nombre: NombreIcono; tamano?: number; color?: string }) {
  const c = useColores();
  return <MaterialIcons name={nombre} size={tamano} color={color ?? c.text} />;
}

// ---------- Texto ----------

type TipoTexto = 'titulo' | 'subtitulo' | 'cuerpo' | 'secundario' | 'etiqueta' | 'fuerte' | 'pequeno';

export function Texto({
  tipo = 'cuerpo',
  color,
  style,
  children,
  numberOfLines,
}: {
  tipo?: TipoTexto;
  color?: string;
  style?: StyleProp<TextStyle>;
  children: ReactNode;
  numberOfLines?: number;
}) {
  const c = useColores();
  const base: TextStyle = {
    titulo: { fontSize: 24, fontWeight: '600' as const, color: c.text },
    subtitulo: { fontSize: 17, fontWeight: '600' as const, color: c.text },
    cuerpo: { fontSize: 15, color: c.text },
    secundario: { fontSize: 14, color: c.textSecondary },
    etiqueta: { fontSize: 11, fontWeight: '600' as const, color: c.textSecondary, textTransform: 'uppercase' as const, letterSpacing: 0.6 },
    fuerte: { fontSize: 15, fontWeight: '600' as const, color: c.text },
    pequeno: { fontSize: 12, color: c.textSecondary },
  }[tipo];
  return (
    <Text style={[base, color ? { color } : null, style]} numberOfLines={numberOfLines}>
      {children}
    </Text>
  );
}

// ---------- Contenedores ----------

/** Pantalla con scroll, ancho máximo centrado y "jalar para recargar". */
export function Pantalla({
  children,
  alRecargar,
  recargando = false,
  sinScroll = false,
}: {
  children: ReactNode;
  alRecargar?: () => void;
  recargando?: boolean;
  sinScroll?: boolean;
}) {
  const c = useColores();
  if (sinScroll) {
    return (
      <SafeAreaView edges={['bottom', 'left', 'right']} style={{ flex: 1, backgroundColor: c.background }}>
        <View style={[estilos.contenido, { flex: 1 }]}>{children}</View>
      </SafeAreaView>
    );
  }
  return (
    <SafeAreaView edges={['bottom', 'left', 'right']} style={{ flex: 1, backgroundColor: c.background }}>
      <ScrollView
        contentContainerStyle={estilos.contenido}
        keyboardShouldPersistTaps="handled"
        refreshControl={alRecargar ? <RefreshControl refreshing={recargando} onRefresh={alRecargar} /> : undefined}>
        {children}
      </ScrollView>
    </SafeAreaView>
  );
}

export function Tarjeta({ children, style, alPresionar }: { children: ReactNode; style?: StyleProp<ViewStyle>; alPresionar?: () => void }) {
  const c = useColores();
  const caja = [estilos.tarjeta, { backgroundColor: c.surface, borderColor: c.line }, style];
  if (!alPresionar) return <View style={caja}>{children}</View>;
  return (
    <Pressable onPress={alPresionar} style={({ pressed }) => [caja, pressed && { opacity: 0.75 }]}>
      {children}
    </Pressable>
  );
}

export function Fila({ children, style, gap = Spacing.two }: { children: ReactNode; style?: StyleProp<ViewStyle>; gap?: number }) {
  return <View style={[{ flexDirection: 'row', alignItems: 'center', gap }, style]}>{children}</View>;
}

export function Encabezado({ titulo, descripcion, acciones }: { titulo: string; descripcion?: string; acciones?: ReactNode }) {
  return (
    <View style={{ gap: Spacing.one, marginBottom: Spacing.three }}>
      <Texto tipo="titulo">{titulo}</Texto>
      {descripcion ? <Texto tipo="secundario">{descripcion}</Texto> : null}
      {acciones ? <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: Spacing.two, marginTop: Spacing.two }}>{acciones}</View> : null}
    </View>
  );
}

export function Seccion({ titulo, children }: { titulo: string; children?: ReactNode }) {
  return (
    <View style={{ marginTop: Spacing.three, marginBottom: Spacing.two, gap: Spacing.two }}>
      <Texto tipo="etiqueta">{titulo}</Texto>
      {children}
    </View>
  );
}

/** Par etiqueta / valor para fichas de detalle. */
export function Dato({ etiqueta, valor, detalle }: { etiqueta: string; valor?: ReactNode; detalle?: string }) {
  return (
    <View style={{ minWidth: 130, flexGrow: 1, flexBasis: '45%', gap: 2 }}>
      <Texto tipo="etiqueta">{etiqueta}</Texto>
      {typeof valor === 'string' || typeof valor === 'number' ? <Texto>{valor}</Texto> : (valor ?? <Texto>—</Texto>)}
      {detalle ? <Texto tipo="pequeno">{detalle}</Texto> : null}
    </View>
  );
}

export function Datos({ children }: { children: ReactNode }) {
  return <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: Spacing.three }}>{children}</View>;
}

export function Separador() {
  const c = useColores();
  return <View style={{ height: StyleSheet.hairlineWidth, backgroundColor: c.line, marginVertical: Spacing.two }} />;
}

// ---------- Botones ----------

type VarianteBoton = 'lleno' | 'contorno' | 'texto' | 'peligro';

export function Boton({
  texto,
  alPresionar,
  variante = 'lleno',
  icono,
  deshabilitado,
  cargando,
  style,
}: {
  texto: string;
  alPresionar: () => void;
  variante?: VarianteBoton;
  icono?: NombreIcono;
  deshabilitado?: boolean;
  cargando?: boolean;
  style?: StyleProp<ViewStyle>;
}) {
  const c = useColores();
  const fondo = variante === 'lleno' ? c.primary : 'transparent';
  const color = variante === 'lleno' ? c.onPrimary : variante === 'peligro' ? c.error : c.primary;
  const borde = variante === 'contorno' ? c.primary : variante === 'peligro' ? c.error : 'transparent';
  const inactivo = deshabilitado || cargando;
  return (
    <Pressable
      accessibilityRole="button"
      disabled={inactivo}
      onPress={alPresionar}
      style={({ pressed }) => [
        estilos.boton,
        { backgroundColor: fondo, borderColor: borde, opacity: inactivo ? 0.5 : pressed ? 0.8 : 1 },
        variante === 'texto' && { paddingHorizontal: Spacing.two },
        style,
      ]}>
      {cargando ? <ActivityIndicator size="small" color={color} /> : icono ? <MaterialIcons name={icono} size={18} color={color} /> : null}
      <Text style={{ color, fontWeight: '600', fontSize: 15 }}>{texto}</Text>
    </Pressable>
  );
}

export function BotonIcono({ icono, alPresionar, etiqueta, color }: { icono: NombreIcono; alPresionar: () => void; etiqueta: string; color?: string }) {
  const c = useColores();
  return (
    <Pressable accessibilityRole="button" accessibilityLabel={etiqueta} onPress={alPresionar} hitSlop={8} style={({ pressed }) => [{ padding: 6, opacity: pressed ? 0.6 : 1 }]}>
      <MaterialIcons name={icono} size={22} color={color ?? c.text} />
    </Pressable>
  );
}

// ---------- Formularios ----------

export function Campo({
  etiqueta,
  error,
  ayuda,
  style,
  ...props
}: TextInputProps & { etiqueta?: string; error?: string | null; ayuda?: string }) {
  const c = useColores();
  return (
    <View style={[{ gap: 4, flexGrow: 1 }, style as StyleProp<ViewStyle>]}>
      {etiqueta ? <Texto tipo="secundario">{etiqueta}</Texto> : null}
      <TextInput
        placeholderTextColor={c.textSecondary}
        {...props}
        editable={props.editable ?? true}
        style={[
          estilos.campo,
          { color: c.text, borderColor: error ? c.error : c.line, backgroundColor: props.editable === false ? c.background : c.surface },
        ]}
      />
      {error ? <Texto tipo="pequeno" color={c.error}>{error}</Texto> : ayuda ? <Texto tipo="pequeno">{ayuda}</Texto> : null}
    </View>
  );
}

export function Busqueda({ valor, alCambiar, placeholder }: { valor: string; alCambiar: (t: string) => void; placeholder: string }) {
  const c = useColores();
  return (
    <View style={[estilos.busqueda, { borderColor: c.line, backgroundColor: c.surface }]}>
      <MaterialIcons name="search" size={20} color={c.textSecondary} />
      <TextInput
        value={valor}
        onChangeText={alCambiar}
        placeholder={placeholder}
        placeholderTextColor={c.textSecondary}
        style={{ flex: 1, color: c.text, fontSize: 15, paddingVertical: 10 }}
        autoCapitalize="none"
        autoCorrect={false}
        returnKeyType="search"
      />
      {valor ? <BotonIcono icono="close" etiqueta="Limpiar búsqueda" alPresionar={() => alCambiar('')} color={c.textSecondary} /> : null}
    </View>
  );
}

export function Interruptor({ etiqueta, valor, alCambiar }: { etiqueta: string; valor: boolean; alCambiar: (v: boolean) => void }) {
  const c = useColores();
  return (
    <Fila style={{ justifyContent: 'space-between', paddingVertical: 4 }}>
      <Texto>{etiqueta}</Texto>
      <Switch value={valor} onValueChange={alCambiar} trackColor={{ true: c.primary, false: c.line }} />
    </Fila>
  );
}

export interface OpcionSelector<T> {
  valor: T;
  texto: string;
  detalle?: string;
}

/** Lista desplegable en una hoja modal (sirve igual en teléfono y navegador). */
export function Selector<T extends string | number | null>({
  etiqueta,
  valor,
  opciones,
  alCambiar,
  placeholder = 'Elegir…',
  deshabilitado,
  error,
}: {
  etiqueta?: string;
  valor: T | undefined;
  opciones: OpcionSelector<T>[];
  alCambiar: (v: T) => void;
  placeholder?: string;
  deshabilitado?: boolean;
  error?: string | null;
}) {
  const c = useColores();
  const [abierto, setAbierto] = useState(false);
  const actual = opciones.find((o) => o.valor === valor);
  return (
    <View style={{ gap: 4, flexGrow: 1 }}>
      {etiqueta ? <Texto tipo="secundario">{etiqueta}</Texto> : null}
      <Pressable
        disabled={deshabilitado}
        onPress={() => setAbierto(true)}
        style={[estilos.campo, estilos.selector, { borderColor: error ? c.error : c.line, backgroundColor: c.surface, opacity: deshabilitado ? 0.5 : 1 }]}>
        <Text style={{ color: actual ? c.text : c.textSecondary, fontSize: 15, flex: 1 }} numberOfLines={1}>
          {actual?.texto ?? placeholder}
        </Text>
        <MaterialIcons name="expand-more" size={20} color={c.textSecondary} />
      </Pressable>
      {error ? <Texto tipo="pequeno" color={c.error}>{error}</Texto> : null}
      <Hoja visible={abierto} titulo={etiqueta ?? placeholder} alCerrar={() => setAbierto(false)}>
        <ScrollView style={{ maxHeight: 420 }}>
          {opciones.map((o) => (
            <Pressable
              key={String(o.valor)}
              onPress={() => {
                alCambiar(o.valor);
                setAbierto(false);
              }}
              style={({ pressed }) => [estilos.opcion, { borderColor: c.line, backgroundColor: pressed ? c.background : 'transparent' }]}>
              <View style={{ flex: 1 }}>
                <Texto tipo={o.valor === valor ? 'fuerte' : 'cuerpo'}>{o.texto}</Texto>
                {o.detalle ? <Texto tipo="pequeno">{o.detalle}</Texto> : null}
              </View>
              {o.valor === valor ? <MaterialIcons name="check" size={20} color={c.primary} /> : null}
            </Pressable>
          ))}
          {opciones.length === 0 ? <Texto tipo="secundario" style={{ padding: Spacing.three }}>No hay opciones.</Texto> : null}
        </ScrollView>
      </Hoja>
    </View>
  );
}

/** Hoja modal con título y botón de cerrar. */
export function Hoja({ visible, titulo, alCerrar, children }: { visible: boolean; titulo: string; alCerrar: () => void; children: ReactNode }) {
  const c = useColores();
  return (
    <Modal visible={visible} transparent animationType="fade" onRequestClose={alCerrar}>
      <Pressable style={estilos.fondoModal} onPress={alCerrar}>
        <Pressable style={[estilos.hoja, { backgroundColor: c.surface }]} onPress={() => {}}>
          <Fila style={{ justifyContent: 'space-between', marginBottom: Spacing.two }}>
            <Texto tipo="subtitulo" style={{ flex: 1 }}>{titulo}</Texto>
            <BotonIcono icono="close" etiqueta="Cerrar" alPresionar={alCerrar} />
          </Fila>
          {children}
        </Pressable>
      </Pressable>
    </Modal>
  );
}

/** Pide un texto (p. ej. el motivo de una cancelación) y confirma. */
export function DialogoTexto({
  visible,
  titulo,
  etiqueta,
  textoConfirmar,
  peligro,
  alCerrar,
  alConfirmar,
}: {
  visible: boolean;
  titulo: string;
  etiqueta: string;
  textoConfirmar: string;
  peligro?: boolean;
  alCerrar: () => void;
  alConfirmar: (texto: string) => Promise<void>;
}) {
  const [texto, setTexto] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [guardando, setGuardando] = useState(false);
  const cerrar = () => {
    setTexto('');
    setError(null);
    alCerrar();
  };
  return (
    <Hoja visible={visible} titulo={titulo} alCerrar={cerrar}>
      <View style={{ gap: Spacing.three }}>
        <Campo etiqueta={etiqueta} value={texto} onChangeText={setTexto} multiline numberOfLines={3} autoFocus />
        {error ? <Aviso tipo="error" texto={error} /> : null}
        <Fila style={{ justifyContent: 'flex-end' }}>
          <Boton texto="Volver" variante="texto" alPresionar={cerrar} />
          <Boton
            texto={textoConfirmar}
            variante={peligro ? 'peligro' : 'lleno'}
            cargando={guardando}
            alPresionar={async () => {
              if (!texto.trim()) {
                setError('Escribe el motivo.');
                return;
              }
              setGuardando(true);
              setError(null);
              try {
                await alConfirmar(texto.trim());
                cerrar();
              } catch (e) {
                setError(mensajeDe(e));
              } finally {
                setGuardando(false);
              }
            }}
          />
        </Fila>
      </View>
    </Hoja>
  );
}

// ---------- Estados ----------

export function Chip({ texto, color = 'default' }: { texto: string; color?: ColorEstado | 'primary' }) {
  const c = useColores();
  const tono = colorDe(c, color);
  return (
    <View style={[estilos.chip, { borderColor: tono, backgroundColor: tono + '1A' }]}>
      <Text style={{ color: tono, fontSize: 12, fontWeight: '600' }}>{texto}</Text>
    </View>
  );
}

function colorDe(c: Paleta, color: ColorEstado | 'primary') {
  return color === 'success' ? c.success : color === 'error' ? c.error : color === 'warning' ? c.warning : color === 'primary' ? c.primary : c.secondary;
}

export function Aviso({ tipo = 'info', texto }: { tipo?: 'info' | 'error' | 'exito' | 'alerta'; texto: string }) {
  const c = useColores();
  const tono = tipo === 'error' ? c.error : tipo === 'exito' ? c.success : tipo === 'alerta' ? c.warning : c.primary;
  const icono: NombreIcono = tipo === 'error' ? 'error-outline' : tipo === 'exito' ? 'check-circle-outline' : tipo === 'alerta' ? 'warning-amber' : 'info-outline';
  return (
    <View style={[estilos.aviso, { borderColor: tono, backgroundColor: tono + '14' }]}>
      <MaterialIcons name={icono} size={20} color={tono} />
      <Text style={{ color: c.text, flex: 1, fontSize: 14 }}>{texto}</Text>
    </View>
  );
}

export function Cargando({ texto = 'Cargando…' }: { texto?: string }) {
  const c = useColores();
  return (
    <View style={{ padding: Spacing.five, alignItems: 'center', gap: Spacing.two }}>
      <ActivityIndicator color={c.primary} />
      <Texto tipo="secundario">{texto}</Texto>
    </View>
  );
}

export function Vacio({ texto }: { texto: string }) {
  return (
    <View style={{ padding: Spacing.four, alignItems: 'center' }}>
      <Texto tipo="secundario">{texto}</Texto>
    </View>
  );
}

/** Indicador grande de un reporte (como Components/Comunes/Indicador.razor). */
export function Indicador({ titulo, valor, detalle, color }: { titulo: string; valor: string; detalle?: string; color?: ColorEstado | 'primary' }) {
  const c = useColores();
  return (
    <Tarjeta style={{ flexGrow: 1, flexBasis: '45%', minWidth: 150, gap: 4 }}>
      <Texto tipo="etiqueta">{titulo}</Texto>
      <Text style={{ fontSize: 18, fontWeight: '700', color: color ? colorDe(c, color) : c.text }} numberOfLines={1} adjustsFontSizeToFit>
        {valor}
      </Text>
      {detalle ? <Texto tipo="pequeno">{detalle}</Texto> : null}
    </Tarjeta>
  );
}

export function Indicadores({ children }: { children: ReactNode }) {
  return <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: Spacing.two, marginBottom: Spacing.three }}>{children}</View>;
}

export function Totales({ subtotal, iva, total }: { subtotal: string; iva: string; total: string }) {
  const c = useColores();
  return (
    <View style={{ alignSelf: 'flex-end', minWidth: 220, gap: 4 }}>
      <Fila style={{ justifyContent: 'space-between' }}>
        <Texto tipo="secundario">Subtotal</Texto>
        <Texto>{subtotal}</Texto>
      </Fila>
      <Fila style={{ justifyContent: 'space-between' }}>
        <Texto tipo="secundario">IVA</Texto>
        <Texto>{iva}</Texto>
      </Fila>
      <View style={{ height: 1, backgroundColor: c.line }} />
      <Fila style={{ justifyContent: 'space-between' }}>
        <Texto tipo="subtitulo">Total</Texto>
        <Texto tipo="subtitulo">{total}</Texto>
      </Fila>
    </View>
  );
}

// ---------- Mensajes ----------

export function mensajeDe(e: unknown) {
  return e instanceof Error ? e.message : 'Ocurrió un error inesperado.';
}

/** Alerta simple; en el navegador Alert de React Native no hace nada, así que se usa window.alert. */
export function avisar(titulo: string, mensaje?: string) {
  if (Platform.OS === 'web') window.alert(mensaje ? `${titulo}\n\n${mensaje}` : titulo);
  else Alert.alert(titulo, mensaje);
}

export function confirmar(titulo: string, mensaje: string, textoSi = 'Sí'): Promise<boolean> {
  if (Platform.OS === 'web') return Promise.resolve(window.confirm(`${titulo}\n\n${mensaje}`));
  return new Promise((resolver) =>
    Alert.alert(titulo, mensaje, [
      { text: 'Cancelar', style: 'cancel', onPress: () => resolver(false) },
      { text: textoSi, onPress: () => resolver(true) },
    ]),
  );
}

const estilos = StyleSheet.create({
  contenido: {
    padding: Spacing.three,
    paddingBottom: Spacing.six,
    width: '100%',
    maxWidth: MaxContentWidth,
    alignSelf: 'center',
  },
  tarjeta: {
    borderWidth: 1,
    borderRadius: Radio,
    padding: Spacing.three,
  },
  boton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: Spacing.two,
    paddingVertical: 10,
    paddingHorizontal: Spacing.three,
    borderRadius: Radio,
    borderWidth: 1,
    minHeight: 44,
  },
  campo: {
    borderWidth: 1,
    borderRadius: Radio,
    paddingHorizontal: 12,
    paddingVertical: 10,
    fontSize: 15,
    minHeight: 44,
  },
  selector: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  busqueda: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.two,
    borderWidth: 1,
    borderRadius: Radio,
    paddingHorizontal: 12,
  },
  opcion: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 12,
    paddingHorizontal: Spacing.two,
    borderBottomWidth: StyleSheet.hairlineWidth,
  },
  fondoModal: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.45)',
    justifyContent: 'center',
    padding: Spacing.three,
  },
  hoja: {
    borderRadius: 12,
    padding: Spacing.three,
    width: '100%',
    maxWidth: 560,
    alignSelf: 'center',
    maxHeight: '90%',
  },
  chip: {
    alignSelf: 'flex-start',
    borderWidth: 1,
    borderRadius: 999,
    paddingHorizontal: 10,
    paddingVertical: 3,
  },
  aviso: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    gap: Spacing.two,
    borderWidth: 1,
    borderRadius: Radio,
    padding: 12,
  },
});

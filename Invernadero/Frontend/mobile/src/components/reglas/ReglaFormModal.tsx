import { MaterialIcons } from '@expo/vector-icons';
import { useColorScheme } from 'nativewind';
import { useState } from 'react';
import {
  KeyboardAvoidingView,
  Modal,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { colores } from '@/theme/tokens';
import {
  ACTUADORES,
  OPERADORES,
  PRIORIDADES,
  SENSORES,
  type Accion,
  type Actuador,
  type DatosRegla,
  type Operador,
  type Prioridad,
  type Regla,
  type Sensor,
} from './datos';

interface Props {
  /** Si se pasa, el formulario edita esa regla; si no, crea una nueva. */
  regla?: Regla;
  onClose: () => void;
  onGuardar: (datos: DatosRegla) => void;
}

/** Formulario en una sola pantalla para crear o editar una regla SI/ENTONCES. */
export function ReglaFormModal({ regla, onClose, onGuardar }: Props) {
  const { colorScheme } = useColorScheme();
  const tema = colorScheme === 'dark' ? colores.oscuro : colores.claro;

  const [nombre, setNombre] = useState(regla?.nombre ?? '');
  const [sensor, setSensor] = useState<Sensor>(regla?.condicion?.sensor ?? 'humedadSuelo');
  const [operador, setOperador] = useState<Operador>(regla?.condicion?.operador ?? '<');
  const [umbral, setUmbral] = useState(String(regla?.condicion?.umbral ?? 35));
  const [actuador, setActuador] = useState<Actuador>(regla?.accion?.actuador ?? 'bomba');
  const [accionVent, setAccionVent] = useState<Accion>(
    regla?.accion?.actuador === 'ventilador' ? regla.accion.accion : 'encender',
  );
  const [dosis, setDosis] = useState(String(regla?.accion?.dosisMl ?? 180));
  const [prioridad, setPrioridad] = useState<Prioridad>(regla?.prioridad ?? 'media');

  const umbralNum = parseFloat(umbral.replace(',', '.'));
  const valido = !Number.isNaN(umbralNum);

  function guardar() {
    if (!valido) return;
    const etiquetaSensor = SENSORES.find((s) => s.id === sensor)?.etiqueta ?? '';
    const esBomba = actuador === 'bomba';
    const dosisNum = Math.min(1000, Math.max(20, parseInt(dosis, 10) || 180));
    onGuardar({
      nombre: nombre.trim() || `Regla ${etiquetaSensor}`,
      prioridad,
      condicion: { sensor, operador, umbral: umbralNum },
      accion: esBomba
        ? { actuador, accion: 'regar', dosisMl: dosisNum }
        : { actuador, accion: accionVent },
    });
  }

  const input = [s.input, { borderColor: tema.outlineVariant, color: tema.onSurface }];

  return (
    <Modal visible transparent animationType="slide" onRequestClose={onClose}>
      <KeyboardAvoidingView
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        style={s.fondo}
      >
        <View style={[s.hoja, { backgroundColor: tema.surfaceBright }]}>
          <View style={s.encabezado}>
            <Text style={[s.titulo, { color: tema.onSurface }]}>
              {regla ? 'Editar regla' : 'Nueva regla'}
            </Text>
            <Pressable onPress={onClose} accessibilityRole="button" accessibilityLabel="Cerrar">
              <MaterialIcons name="close" size={24} color={tema.onSurfaceVariant} />
            </Pressable>
          </View>

          <ScrollView contentContainerStyle={s.cuerpo} keyboardShouldPersistTaps="handled">
            <Text style={[s.etiqueta, { color: tema.onSurfaceVariant }]}>Nombre</Text>
            <TextInput
              value={nombre}
              onChangeText={setNombre}
              placeholder="Ej. Riego por sequedad"
              placeholderTextColor={tema.onSurfaceVariant}
              style={input}
            />

            <Text style={[s.seccion, { color: tema.primary }]}>SI</Text>
            <Opciones
              opciones={SENSORES}
              valor={sensor}
              onChange={setSensor}
            />
            <Opciones opciones={OPERADORES} valor={operador} onChange={setOperador} />
            <Text style={[s.etiqueta, { color: tema.onSurfaceVariant }]}>
              Valor ({SENSORES.find((x) => x.id === sensor)?.unidad.trim()})
            </Text>
            <TextInput
              value={umbral}
              onChangeText={setUmbral}
              keyboardType="decimal-pad"
              style={input}
            />

            <Text style={[s.seccion, { color: tema.primary }]}>ENTONCES</Text>
            <Opciones opciones={ACTUADORES} valor={actuador} onChange={setActuador} />
            {actuador === 'bomba' ? (
              <>
                <Text style={[s.etiqueta, { color: tema.onSurfaceVariant }]}>
                  Dosis por ejecución (mL)
                </Text>
                <TextInput
                  value={dosis}
                  onChangeText={setDosis}
                  keyboardType="number-pad"
                  style={input}
                />
              </>
            ) : (
              <Opciones
                opciones={[
                  { id: 'encender' as Accion, etiqueta: 'Encender' },
                  { id: 'apagar' as Accion, etiqueta: 'Apagar' },
                ]}
                valor={accionVent}
                onChange={setAccionVent}
              />
            )}

            <Text style={[s.seccion, { color: tema.primary }]}>PRIORIDAD</Text>
            <Opciones opciones={PRIORIDADES} valor={prioridad} onChange={setPrioridad} />
          </ScrollView>

          <View style={s.pie}>
            <Pressable
              onPress={onClose}
              accessibilityRole="button"
              style={[s.boton, { backgroundColor: tema.surfaceContainer }]}
            >
              <Text style={[s.botonTexto, { color: tema.onSurface }]}>Cancelar</Text>
            </Pressable>
            <Pressable
              onPress={guardar}
              disabled={!valido}
              accessibilityRole="button"
              style={[s.boton, { backgroundColor: tema.primary, opacity: valido ? 1 : 0.5 }]}
            >
              <Text style={[s.botonTexto, { color: tema.onPrimary }]}>
                {regla ? 'Actualizar' : 'Guardar'}
              </Text>
            </Pressable>
          </View>
        </View>
      </KeyboardAvoidingView>
    </Modal>
  );
}

/** Fila de chips de selección única. */
function Opciones<T extends string>({
  opciones,
  valor,
  onChange,
}: {
  opciones: { id: T; etiqueta: string }[];
  valor: T;
  onChange: (id: T) => void;
}) {
  const { colorScheme } = useColorScheme();
  const tema = colorScheme === 'dark' ? colores.oscuro : colores.claro;

  return (
    <View style={s.chips}>
      {opciones.map((o) => {
        const sel = o.id === valor;
        return (
          <Pressable
            key={o.id}
            onPress={() => onChange(o.id)}
            accessibilityRole="radio"
            accessibilityState={{ selected: sel }}
            style={[s.chip, { backgroundColor: sel ? tema.primary : tema.surfaceContainer }]}
          >
            <Text style={{ fontSize: 13, fontWeight: '600', color: sel ? tema.onPrimary : tema.onSurface }}>
              {o.etiqueta}
            </Text>
          </Pressable>
        );
      })}
    </View>
  );
}

const s = StyleSheet.create({
  fondo: { flex: 1, justifyContent: 'flex-end', backgroundColor: 'rgba(0,0,0,0.5)' },
  hoja: { maxHeight: '90%', borderTopLeftRadius: 24, borderTopRightRadius: 24, padding: 16 },
  encabezado: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  titulo: { fontSize: 18, fontWeight: '600' },
  cuerpo: { gap: 8, paddingVertical: 12 },
  seccion: { fontSize: 11, fontWeight: '700', letterSpacing: 0.5, marginTop: 8 },
  etiqueta: { fontSize: 12 },
  input: { borderWidth: 1, borderRadius: 12, paddingHorizontal: 12, paddingVertical: 10, fontSize: 15 },
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  chip: { borderRadius: 999, paddingHorizontal: 14, paddingVertical: 8 },
  pie: { flexDirection: 'row', gap: 8, paddingTop: 8 },
  boton: { flex: 1, minHeight: 48, borderRadius: 12, alignItems: 'center', justifyContent: 'center' },
  botonTexto: { fontSize: 14, fontWeight: '600' },
});

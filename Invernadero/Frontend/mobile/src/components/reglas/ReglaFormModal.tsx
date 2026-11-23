
import { MaterialIcons } from '@expo/vector-icons';
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

import type { Regla } from '../../../types';

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
  type Sensor,
} from './datos';
import { useTema } from '@/hooks/useTema';

const RANGOS: Record<Sensor, [number, number]> = {
  humedadSuelo: [0, 100],
  nivelDeposito: [0, 100],
  temperatura: [-10, 60],
  luz: [0, 120000],
};

interface Props {
  regla?: Regla;
  onClose: () => void;
  onGuardar: (datos: DatosRegla) => void;
}

export function ReglaFormModal({ regla, onClose, onGuardar }: Props) {
  const tema = useTema();

  const sensorInicial: Sensor =
    regla?.si.sensor && regla.si.sensor !== 'horario'
      ? regla.si.sensor
      : 'humedadSuelo';

  const operadorInicial: Operador =
    regla?.si.operador === '<' ||
    regla?.si.operador === '>' ||
    regla?.si.operador === '>=' ||
    regla?.si.operador === '<='
      ? regla.si.operador
      : '<';

  const actuadorInicial: Actuador =
    regla?.entonces.actuador === 'bomba' ||
    regla?.entonces.actuador === 'ventilador'
      ? regla.entonces.actuador
      : 'bomba';

  const [nombre, setNombre] = useState(regla?.nombre ?? '');
  const [sensor, setSensor] = useState<Sensor>(sensorInicial);
  const [operador, setOperador] = useState<Operador>(operadorInicial);

  const [umbral, setUmbral] = useState(
    regla?.si.sensor !== 'horario' ? regla?.si.umbral ?? '35' : '35',
  );

  const [actuador, setActuador] = useState<Actuador>(actuadorInicial);

  const [accionVent, setAccionVent] = useState<Accion>(
    regla?.entonces.accion === 'apagar' ? 'apagar' : 'encender',
  );

  const [dosis, setDosis] = useState(
    String(regla?.entonces.volumenMl ?? 180),
  );

  const [prioridad, setPrioridad] = useState<Prioridad>(
    regla?.prioridad ?? 'media',
  );

  const umbralNum = Number(umbral.replace(',', '.'));
  const [min, max] = RANGOS[sensor];

  const valido =
    nombre.trim().length > 0 &&
    umbral.trim() !== '' &&
    Number.isFinite(umbralNum) &&
    umbralNum >= min &&
    umbralNum <= max;

  function guardar() {
    if (!valido) return;

    const esBomba = actuador === 'bomba';

    const dosisNum = Math.min(
      1000,
      Math.max(20, parseInt(dosis, 10) || 180),
    );

    onGuardar({
      nombre: nombre.trim(),
      prioridad,
      condicion: {
        sensor,
        operador,
        umbral: umbralNum,
      },
      accion: esBomba
        ? { actuador, accion: 'regar', dosisMl: dosisNum }
        : { actuador, accion: accionVent },
    });
  }

  const input = [
    s.input,
    {
      borderColor: tema.outlineVariant,
      color: tema.onSurface,
    },
  ];

  return (
    <Modal
      visible
      transparent
      animationType="slide"
      onRequestClose={onClose}
    >
      <KeyboardAvoidingView
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        style={s.fondo}
      >
        <View style={[s.hoja, { backgroundColor: tema.surfaceBright }]}>
          <View style={s.encabezado}>
            <Text style={[s.titulo, { color: tema.onSurface }]}>
              {regla ? 'Editar regla' : 'Nueva regla'}
            </Text>

            <Pressable
              onPress={onClose}
              accessibilityRole="button"
              accessibilityLabel="Cerrar"
            >
              <MaterialIcons
                name="close"
                size={24}
                color={tema.onSurfaceVariant}
              />
            </Pressable>
          </View>

          <ScrollView
            contentContainerStyle={s.cuerpo}
            keyboardShouldPersistTaps="handled"
          >
            <Text style={[s.etiqueta, { color: tema.onSurfaceVariant }]}>
              Nombre
            </Text>

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

            <Opciones
              opciones={OPERADORES}
              valor={operador}
              onChange={setOperador}
            />

            <Text style={[s.etiqueta, { color: tema.onSurfaceVariant }]}>
              Valor ({SENSORES.find((x) => x.id === sensor)?.unidad.trim()})
            </Text>

            <TextInput
              value={umbral}
              onChangeText={setUmbral}
              keyboardType="decimal-pad"
              style={input}
            />

            {!valido && (
              <Text style={{ fontSize: 12, color: tema.error }}>
                Ingresa un nombre y un valor entre {min} y {max}.
              </Text>
            )}

            <Text style={[s.seccion, { color: tema.primary }]}>
              ENTONCES
            </Text>

            <Opciones
              opciones={ACTUADORES}
              valor={actuador}
              onChange={setActuador}
            />

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

            <Text style={[s.seccion, { color: tema.primary }]}>
              PRIORIDAD
            </Text>

            <Opciones
              opciones={PRIORIDADES}
              valor={prioridad}
              onChange={setPrioridad}
            />
          </ScrollView>

          <View style={s.pie}>
            <Pressable
              onPress={onClose}
              accessibilityRole="button"
              style={[
                s.boton,
                { backgroundColor: tema.surfaceContainer },
              ]}
            >
              <Text style={[s.botonTexto, { color: tema.onSurface }]}>
                Cancelar
              </Text>
            </Pressable>

            <Pressable
              onPress={guardar}
              disabled={!valido}
              accessibilityRole="button"
              accessibilityState={{ disabled: !valido }}
              style={[
                s.boton,
                {
                  backgroundColor: tema.primary,
                  opacity: valido ? 1 : 0.5,
                },
              ]}
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

function Opciones<T extends string>({
  opciones,
  valor,
  onChange,
}: {
  opciones: { id: T; etiqueta: string }[];
  valor: T;
  onChange: (id: T) => void;
}) {
  const tema = useTema();

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
            style={[
              s.chip,
              {
                backgroundColor: sel
                  ? tema.primary
                  : tema.surfaceContainer,
              },
            ]}
          >
            <Text
              style={{
                fontSize: 13,
                fontWeight: '600',
                color: sel ? tema.onPrimary : tema.onSurface,
              }}
            >
              {o.etiqueta}
            </Text>
          </Pressable>
        );
      })}
    </View>
  );
}

const s = StyleSheet.create({
  fondo: {
    flex: 1,
    justifyContent: 'flex-end',
    backgroundColor: 'rgba(0,0,0,0.5)',
  },
  hoja: {
    maxHeight: '90%',
    borderTopLeftRadius: 24,
    borderTopRightRadius: 24,
    padding: 16,
  },
  encabezado: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  titulo: {
    fontSize: 18,
    fontWeight: '600',
  },
  cuerpo: {
    gap: 8,
    paddingVertical: 12,
  },
  seccion: {
    fontSize: 11,
    fontWeight: '700',
    letterSpacing: 0.5,
    marginTop: 8,
  },
  etiqueta: {
    fontSize: 12,
  },
  input: {
    borderWidth: 1,
    borderRadius: 12,
    paddingHorizontal: 12,
    paddingVertical: 10,
    fontSize: 15,
  },
  chips: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
  },
  chip: {
    borderRadius: 999,
    paddingHorizontal: 14,
    paddingVertical: 8,
  },
  pie: {
    flexDirection: 'row',
    gap: 8,
    paddingTop: 8,
  },
  boton: {
    flex: 1,
    minHeight: 48,
    borderRadius: 12,
    alignItems: 'center',
    justifyContent: 'center',
  },
  botonTexto: {
    fontSize: 14,
    fontWeight: '600',
  },
});

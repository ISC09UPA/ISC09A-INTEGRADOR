import { MaterialIcons } from '@expo/vector-icons';
import { useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { BannerMotor } from '@/components/reglas/BannerMotor';
import { FiltrosReglas } from '@/components/reglas/FiltrosReglas';
import { ReglaFormModal } from '@/components/reglas/ReglaFormModal';
import { RuleCard } from '@/components/reglas/RuleCard';
import { Confirmar } from '@/components/Confirmar';
import { REGLAS, construirRegla, type Categoria, type DatosRegla, type Regla } from '@/components/reglas/datos';
import { colores } from '@/theme/tokens';
import { useColorScheme } from 'nativewind';

export default function Reglas() {
  const [reglas, setReglas] = useState(REGLAS);
  const [filtro, setFiltro] = useState<Categoria | 'todas'>('todas');
  // Formulario: cerrado, "nueva" o la regla que se edita.
  const [formulario, setFormulario] = useState<'nueva' | Regla | null>(null);
  const [aEliminar, setAEliminar] = useState<Regla | null>(null);
  const { colorScheme } = useColorScheme();
  const tema = colorScheme === 'dark' ? colores.oscuro : colores.claro;
  const visibles = reglas.filter((r) => filtro === 'todas' || r.categoria === filtro);
  const activas = reglas.filter((r) => r.activa).length;

  function alternar(id: string, activa: boolean) {
    setReglas((rs) => rs.map((r) => (r.id === id ? { ...r, activa } : r)));
  }

  function guardar(datos: DatosRegla) {
    const base = formulario !== 'nueva' ? formulario ?? undefined : undefined;
    const regla = construirRegla(datos, base);
    setReglas((rs) =>
      base ? rs.map((r) => (r.id === regla.id ? regla : r)) : [...rs, regla],
    );
    setFormulario(null);
  }

  function eliminar() {
    if (aEliminar) setReglas((rs) => rs.filter((r) => r.id !== aEliminar.id));
    setAEliminar(null);
  }

  return (
    <View style={[s.pantalla, { backgroundColor: tema.surface }]}>
      <ScrollView contentContainerStyle={s.contenido} showsVerticalScrollIndicator={false}>
        <BannerMotor activas={activas} total={reglas.length} />
        <FiltrosReglas seleccionado={filtro} onSelect={setFiltro} />

        {visibles.length === 0 ? (
          <Text style={[s.vacio, { color: tema.onSurfaceVariant }]}>
            Sin reglas en esta categoría.
          </Text>
        ) : (
          visibles.map((r) => (
            <RuleCard
              key={r.id}
              regla={r}
              onToggle={(a) => alternar(r.id, a)}
              onEditar={!r.bloqueada && r.condicion ? () => setFormulario(r) : undefined}
              onEliminar={!r.bloqueada ? () => setAEliminar(r) : undefined}
            />
          ))
        )}

        <Pressable
          onPress={() => setFormulario('nueva')}
          accessibilityRole="button"
          accessibilityLabel="Crear nueva regla"
          style={[s.nueva, { borderColor: tema.outline }]}
        >
          <MaterialIcons name="add-circle-outline" size={20} color={tema.primary} />
          <Text style={[s.nuevaTexto, { color: tema.primary }]}>Crear nueva regla SI / ENTONCES</Text>
        </Pressable>
      </ScrollView>

      {formulario && (
        <ReglaFormModal
          regla={formulario === 'nueva' ? undefined : formulario}
          onClose={() => setFormulario(null)}
          onGuardar={guardar}
        />
      )}

      <Confirmar
        visible={aEliminar !== null}
        titulo="Eliminar regla"
        descripcion={`¿Eliminar "${aEliminar?.nombre}"? Esta acción no se puede deshacer.`}
        textoConfirmar="Eliminar"
        peligroso
        onCancelar={() => setAEliminar(null)}
        onConfirmar={eliminar}
      />
    </View>
  );
}

const s = StyleSheet.create({
  pantalla: {
    flex: 1,
  },
  contenido: {
    padding: 16,
    paddingBottom: 32,
    gap: 12,
  },
  vacio: {
    textAlign: 'center',
    paddingVertical: 24,
  },
  nueva: {
    minHeight: 48,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
    borderWidth: 2,
    borderStyle: 'dashed',
    borderRadius: 12,
    paddingVertical: 12,
  },
  nuevaTexto: {
    fontSize: 14,
    fontWeight: '600',
  },
});

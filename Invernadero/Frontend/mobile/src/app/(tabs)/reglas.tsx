
import { MaterialIcons } from '@expo/vector-icons';
import { useCallback, useEffect, useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  FlatList,
  Pressable,
  RefreshControl,
  Text,
  View,
} from 'react-native';

import type { Regla } from '../../../types';

import {
  alternarRegla,
  eliminarRegla,
  guardarRegla,
  listarReglas,
} from '../../../services/reglasService';

import {
  construirRegla,
  type Categoria,
  type DatosRegla,
} from '@/components/reglas/datos';

import { Confirmar } from '@/components/Confirmar';
import { BannerMotor } from '@/components/reglas/BannerMotor';
import { FiltrosReglas } from '@/components/reglas/FiltrosReglas';
import { ReglaFormModal } from '@/components/reglas/ReglaFormModal';
import { RuleCard } from '@/components/reglas/RuleCard';
import { useTema } from '@/hooks/useTema';

export default function ReglasScreen() {
  const tema = useTema();

  // Reglas cargadas desde el servicio.
  const [reglas, setReglas] = useState<Regla[]>([]);

  // Filtro seleccionado.
  const [filtro, setFiltro] = useState<Categoria | 'todas'>('todas');

  // Estado del formulario: nueva regla o regla existente.
  const [formulario, setFormulario] = useState<'nueva' | Regla | null>(null);

  // Regla pendiente de confirmación para eliminar.
  const [reglaAEliminar, setReglaAEliminar] = useState<Regla | null>(null);

  // Estados de carga.
  const [cargando, setCargando] = useState(true);
  const [actualizando, setActualizando] = useState(false);

  // Cargar reglas desde el servicio.
  const cargarReglas = useCallback(async () => {
    try {
      const resultado = await listarReglas();
      setReglas(resultado);
    } catch (error) {
      if (__DEV__) {
        console.error('Error al cargar reglas:', error);
      }

      Alert.alert('Error', 'No se pudieron cargar las reglas.');
    } finally {
      setCargando(false);
      setActualizando(false);
    }
  }, []);

  // Cargar las reglas al abrir la pantalla.
  useEffect(() => {
    void cargarReglas();
  }, [cargarReglas]);

  // Crear o actualizar una regla.
  const guardar = async (datos: DatosRegla) => {
    try {
      const base =
        formulario !== null && formulario !== 'nueva'
          ? formulario
          : undefined;

      const regla = construirRegla(datos, base);

      await guardarRegla(regla);
      await cargarReglas();

      setFormulario(null);

      Alert.alert(
        'Regla guardada',
        'La regla se guardó correctamente.',
      );
    } catch (error) {
      if (__DEV__) {
        console.error('Error al guardar regla:', error);
      }

      Alert.alert('Error', 'No se pudo guardar la regla.');
    }
  };

  // Activar o desactivar una regla.
  const alternar = async (id: string, activa: boolean) => {
    try {
      await alternarRegla(id, activa);

      setReglas((actuales) =>
        actuales.map((regla) =>
          regla.id === id ? { ...regla, activa } : regla,
        ),
      );
    } catch (error) {
      if (__DEV__) {
        console.error('Error al cambiar regla:', error);
      }

      Alert.alert('Error', 'No se pudo actualizar la regla.');

      await cargarReglas();
    }
  };

  // Mostrar confirmación antes de eliminar.
  const eliminar = (regla: Regla) => {
    setReglaAEliminar(regla);
  };

  // Eliminar la regla después de confirmar.
  const confirmarEliminacion = async () => {
    if (!reglaAEliminar) return;

    const regla = reglaAEliminar;
    setReglaAEliminar(null);

    try {
      await eliminarRegla(regla.id);

      setReglas((actuales) =>
        actuales.filter((item) => item.id !== regla.id),
      );
    } catch (error) {
      if (__DEV__) {
        console.error('Error al eliminar regla:', error);
      }

      Alert.alert('Error', 'No se pudo eliminar la regla.');
    }
  };

  // Filtrar reglas por categoría.
  const reglasFiltradas = reglas.filter(
    (regla) =>
      filtro === 'todas' || regla.categoria === filtro,
  );

  // Contar reglas activas.
  const activas = reglas.filter((regla) => regla.activa).length;

  // Solo permitir editar reglas compatibles con el formulario.
  const puedeEditar = (regla: Regla) =>
    !regla.bloqueadaPorSeguridad &&
    regla.si.sensor !== 'horario' &&
    regla.si.operador !== 'entre' &&
    (regla.entonces.actuador === 'bomba' ||
      regla.entonces.actuador === 'ventilador') &&
    regla.entonces.accion !== 'bloquear';

  // No permitir eliminar reglas bloqueadas por seguridad.
  const puedeEliminar = (regla: Regla) =>
    !regla.bloqueadaPorSeguridad;

  return (
    <View
      style={{
        flex: 1,
        backgroundColor: tema.background,
      }}
    >
      <FlatList
        data={reglasFiltradas}
        keyExtractor={(item) => item.id}
        contentContainerStyle={{
          padding: 16,
          paddingBottom: 32,
        }}
        refreshControl={
          <RefreshControl
            refreshing={actualizando}
            onRefresh={() => {
              setActualizando(true);
              void cargarReglas();
            }}
            tintColor={tema.primary}
          />
        }
        ListHeaderComponent={
          <View style={{ gap: 16, marginBottom: 16 }}>
            <BannerMotor
              activas={activas}
              total={reglas.length}
            />

            <FiltrosReglas
              seleccionado={filtro}
              onSelect={setFiltro}
            />

            <View
              style={{
                flexDirection: 'row',
                justifyContent: 'space-between',
                alignItems: 'center',
              }}
            >
              <Text
                style={{
                  color: tema.onSurface,
                  fontSize: 18,
                  fontWeight: '700',
                }}
              >
                Reglas automatizadas
              </Text>

              <Text
                style={{
                  color: tema.onSurfaceVariant,
                  fontSize: 13,
                }}
              >
                {reglasFiltradas.length} reglas
              </Text>
            </View>

            <Pressable
              onPress={() => setFormulario('nueva')}
              accessibilityRole="button"
              accessibilityLabel="Crear nueva regla"
              style={{
                flexDirection: 'row',
                alignItems: 'center',
                justifyContent: 'center',
                gap: 8,
                padding: 14,
                borderRadius: 12,
                backgroundColor: tema.primary,
              }}
            >
              <MaterialIcons
                name="add"
                size={20}
                color={tema.onPrimary}
              />

              <Text
                style={{
                  color: tema.onPrimary,
                  fontWeight: '600',
                  fontSize: 14,
                }}
              >
                Nueva regla
              </Text>
            </Pressable>
          </View>
        }
        renderItem={({ item }) => (
          <RuleCard
            regla={item}
            onToggle={(activa: boolean) =>
              alternar(item.id, activa)
            }
            onEditar={
              puedeEditar(item)
                ? () => setFormulario(item)
                : undefined
            }
            onEliminar={
              puedeEliminar(item)
                ? () => eliminar(item)
                : undefined
            }
          />
        )}
        ItemSeparatorComponent={() => (
          <View style={{ height: 12 }} />
        )}
        ListEmptyComponent={
          cargando ? (
            <ActivityIndicator
              size="large"
              color={tema.primary}
              style={{ marginTop: 32 }}
            />
          ) : (
            <Text
              style={{
                color: tema.onSurfaceVariant,
                textAlign: 'center',
                marginTop: 32,
                fontSize: 14,
              }}
            >
              No hay reglas para mostrar.
            </Text>
          )
        }
      />

      {formulario && (
        <ReglaFormModal
          regla={
            formulario === 'nueva'
              ? undefined
              : formulario
          }
          onClose={() => setFormulario(null)}
          onGuardar={guardar}
        />
      )}

      {/* Confirmación para eliminar reglas. */}
      <Confirmar
        visible={reglaAEliminar !== null}
        titulo="Eliminar regla"
        descripcion={
          reglaAEliminar
            ? `¿Estás seguro de que deseas eliminar "${reglaAEliminar.nombre}"? Esta acción no se puede deshacer.`
            : ''
        }
        textoConfirmar="Eliminar"
        peligroso
        onCancelar={() => setReglaAEliminar(null)}
        onConfirmar={() => void confirmarEliminacion()}
      />
    </View>
  );
}

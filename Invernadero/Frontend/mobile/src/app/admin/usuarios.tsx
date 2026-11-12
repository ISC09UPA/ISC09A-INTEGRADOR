import { useState } from 'react';
import { Modal, Pressable, ScrollView, Switch, Text, TextInput, View } from 'react-native';
import { MaterialIcons } from '@expo/vector-icons';
import { router } from 'expo-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { Rol, Usuario } from '../../../types';
import { usuariosService } from '../../../services';
import { colores } from '@/theme/tokens';
import { useSesion } from '../../../store/useSesionStore';
import { Confirmar } from '@/components/Confirmar';
import { Skeleton, Vacio } from '@/components/ui/Estados';
import { hapticoError, hapticoExito, hapticoToque } from '../../../utils/haptico';

/** Chip de rol reutilizable. */
function ChipRol({ rol }: { rol: Rol }) {
  const fondo = rol === 'admin' ? 'bg-primary-container' : 'bg-secondary-container';
  const color = rol === 'admin' ? 'text-on-primary-container' : 'text-on-secondary-container';
  const etiqueta = rol === 'admin' ? 'Admin · Control total' : 'Operador · Solo lectura global';
  return (
    <Text className={`rounded-full px-2 py-0.5 self-start ${fondo} ${color} text-label-sm font-semibold`}>{etiqueta}</Text>
  );
}

interface Props {
  visible: boolean;
  onClose: () => void;
  onGuardar: (nombre: string, email: string, rol: Rol) => Promise<unknown>;
}

/** Modal para crear un nuevo usuario (solo admin). */
function ModalCrear({ visible, onClose, onGuardar }: Props) {
  const [nombre, setNombre] = useState('');
  const [email, setEmail] = useState('');
  const [rol, setRol] = useState<Rol>('operador');
  const [error, setError] = useState<string | null>(null);
  const [ocupado, setOcupado] = useState(false);
  const guardar = async () => {
    if (!nombre.trim() || !email.trim()) {
      setError('Nombre y correo son obligatorios.');
      return;
    }
    setOcupado(true);
    try {
      await onGuardar(nombre.trim(), email.trim(), rol);
      hapticoExito();
      setNombre('');
      setEmail('');
      setRol('operador');
      setError(null);
      onClose();
    } catch (e) {
      hapticoError();
      setError(e instanceof Error ? e.message : 'No se pudo crear el usuario.');
    } finally {
      setOcupado(false);
    }
  };
  return (
    <Modal transparent visible={visible} animationType="fade" onRequestClose={onClose}>
      <View className="flex-1 items-center justify-center bg-black/50 px-4">
        <View className="w-full max-w-[340px] rounded-2xl bg-surface-container-lowest p-4">
          <Text className="text-title-md text-on-surface">Añadir usuario</Text>
          <Text className="text-label-sm text-on-surface-variant">Los usuarios demo existen solo para la sesión actual.</Text>
          <TextInput
            value={nombre}
            onChangeText={(t) => setNombre(t)}
            placeholder="Nombre completo"
            className="mt-3 rounded-xl border border-outline-variant px-3 py-3 text-body-md text-on-surface"
            accessibilityLabel="Nombre del usuario"
          />
          <TextInput
            value={email}
            onChangeText={(t) => setEmail(t)}
            placeholder="correo@demo.com"
            autoCapitalize="none"
            keyboardType="email-address"
            className="mt-2 rounded-xl border border-outline-variant px-3 py-3 text-body-md text-on-surface"
            accessibilityLabel="Correo del usuario"
          />
          <View className="mt-3 flex-row gap-2">
            {(['operador', 'admin'] as Rol[]).map((r) => (
              <Pressable
                key={r}
                onPress={() => {
                  hapticoToque();
                  setRol(r);
                }}
                className={`min-h-[44px] flex-1 items-center justify-center gap-1 rounded-xl py-2 ${rol === r ? 'bg-primary-container' : 'bg-surface-container-low'}`}
                accessibilityRole="radio"
                accessibilityLabel={`Rol ${r}`}
              >
                <MaterialIcons name={r === 'admin' ? 'admin-panel-settings' : 'person'} size={18} color={rol === r ? colores.onPrimaryContainer : colores.onSurfaceVariant} />
                <Text className={`text-label-md font-semibold ${rol === r ? 'text-on-primary-container' : 'text-on-surface-variant'}`}>
                  {r === 'admin' ? 'Admin' : 'Operador'}
                </Text>
              </Pressable>
            ))}
          </View>
          {error && <Text className="mt-2 text-body-sm text-error">{error}</Text>}
          <View className="mt-4 flex-row gap-2">
            <Pressable
              onPress={onClose}
              className="min-h-[48px] flex-1 items-center justify-center rounded-xl bg-surface-container"
              accessibilityRole="button"
            >
              <Text className="text-label-lg font-semibold text-on-surface">Cancelar</Text>
            </Pressable>
            <Pressable
              onPress={() => void guardar()}
              disabled={ocupado}
              className={`min-h-[48px] flex-1 items-center justify-center rounded-xl bg-primary ${ocupado ? 'opacity-50' : ''}`}
              accessibilityRole="button"
            >
              <Text className="text-label-lg font-semibold text-on-primary">{ocupado ? 'Guardando…' : 'Guardar'}</Text>
            </Pressable>
          </View>
        </View>
      </View>
    </Modal>
  );
}

export default function AdminUsuarios() {
  const { usuario } = useSesion();
  const esAdmin = (usuario?.rol ?? 'operador') === 'admin';
  const consultas = useQueryClient();
  const usuarios = useQuery({ queryKey: ['usuarios'], queryFn: usuariosService.listarUsuarios });
  const [crearVisible, setCrearVisible] = useState(false);
  const [aEliminar, setAEliminar] = useState<Usuario | null>(null);

  const crear = useMutation({
    mutationFn: (entrada: { nombre: string; email: string; rol: Rol }) =>
      usuariosService.crearUsuario(entrada.nombre, entrada.email, entrada.rol),
    onSuccess: () => void consultas.invalidateQueries({ queryKey: ['usuarios'] }),
  });
  const cambiarRol = useMutation({
    mutationFn: ({ id, rol }: { id: string; rol: Rol }) => usuariosService.editarRol(id, rol),
    onSuccess: () => void consultas.invalidateQueries({ queryKey: ['usuarios'] }),
  });
  const eliminar = useMutation({
    mutationFn: usuariosService.eliminarUsuario,
    onSuccess: () => {
      hapticoExito();
      setAEliminar(null);
      void consultas.invalidateQueries({ queryKey: ['usuarios'] });
    },
  });

  if (!esAdmin) {
    return (
      <View className="flex-1 bg-surface">
        <View className="flex-row items-center px-2 pt-2">
          <Pressable onPress={() => router.back()} accessibilityLabel="Cerrar" className="h-10 w-10 items-center justify-center">
            <MaterialIcons name="close" size={24} color={colores.onSurface} />
          </Pressable>
        </View>
        <View className="flex-1 items-center justify-center px-6">
          <MaterialIcons name="lock" size={40} color={colores.onSurfaceVariant} />
          <Text className="pt-3 text-title-md text-on-surface">Solo administradores</Text>
          <Text className="pt-1 text-center text-body-sm text-on-surface-variant">
            La gestión de usuarios está reservada al rol admin. Pide a un admin que actualice tu cuenta o consulta con él las reglas de acceso.
          </Text>
        </View>
      </View>
    );
  }

  if (usuarios.isLoading) {
    return (
      <View className="flex-1 bg-surface p-4">
        <Skeleton alto={64} />
        <View style={{ height: 8 }} />
        <Skeleton alto={64} />
        <View style={{ height: 8 }} />
        <Skeleton alto={64} />
      </View>
    );
  }

  if (usuarios.isError || !usuarios.data) {
    return (
      <View className="flex-1 bg-surface p-4">
        <Vacio
          titulo="No se pudieron cargar los usuarios"
          descripcion="Reintenta la sincronización con la capa de servicios."
          accionTexto="Reintentar"
          accionOn={() => void usuarios.refetch()}
        />
      </View>
    );
  }

  return (
    <View className="flex-1 bg-surface">
      <ScrollView contentContainerStyle={{ paddingBottom: 24 }} showsVerticalScrollIndicator={false}>
        <View className="flex-row items-center justify-between px-gutter-mobile pb-3">
          <View>
            <Text className="text-headline-lg text-on-surface">Gestión de Usuarios</Text>
            <Text className="text-body-sm text-on-surface-variant">
              {usuarios.data.length} cuentas · invitado control por rol admin
            </Text>
          </View>
          <Pressable
            onPress={() => {
              hapticoToque();
              setCrearVisible(true);
            }}
            className="flex-row items-center gap-1.5 rounded-xl bg-primary px-3 py-2.5 min-h-[44px]"
            accessibilityRole="button"
            accessibilityLabel="Añadir usuario"
          >
            <MaterialIcons name="person-add" size={18} color={colores.onPrimary} />
            <Text className="text-label-md font-semibold text-on-primary">Añadir</Text>
          </Pressable>
        </View>

        <View className="gap-2 p-space-md">
          {usuarios.data.map((u) => {
            const esYo = u.id === usuario?.id;
            const cambiarAdmin = u.rol === 'admin';
            return (
              <View key={u.id} className="rounded-xl bg-surface-container-lowest p-space-md shadow-sm">
                <View className="flex-row items-center justify-between">
                  <View className="flex-row items-center gap-2">
                    <View className="h-10 w-10 items-center justify-center rounded-full bg-primary">
                      <Text className="text-[13px] font-semibold text-on-primary">
                        {u.nombre.slice(0, 2).toUpperCase()}
                      </Text>
                    </View>
                    <View className="flex-col">
                      <View className="flex-row items-center gap-1">
                        <Text className="text-title-sm text-on-surface">{u.nombre}</Text>
                        {esYo && (
                          <View className="rounded-full bg-surface-container px-2 py-0.5">
                            <Text className="text-label-sm text-on-surface-variant">Tú</Text>
                          </View>
                        )}
                      </View>
                      <Text className="text-label-sm text-on-surface-variant">{u.email}</Text>
                    </View>
                  </View>
                  <ChipRol
                    rol={u.rol}
                  />
                </View>

                {esYo ? (
                  <Text className="pt-2 text-body-sm text-on-surface-variant">
                    No puedes cambiar tu propio rol ni eliminar tu cuenta mientras seas el administrador en sesión.
                  </Text>
                ) : (
                  <View className="flex-row items-center justify-between pt-3">
                    <View className="flex-1 flex-row items-center gap-2 pr-2">
                      <Switch
                        value={cambiarAdmin}
                        onValueChange={(v) => void cambiarRol.mutateAsync({ id: u.id, rol: v ? 'admin' : 'operador' })}
                        trackColor={{ false: colores.surfaceContainerHighest, true: colores.primary }}
                        accessibilityLabel={`Cambiar rol de ${u.nombre}`}
                      />
                      <Text className="text-body-sm text-on-surface">{cambiarAdmin ? 'Admin' : 'Operador'}</Text>
                    </View>
                    <Pressable
                      onPress={() => {
                        hapticoToque();
                        setAEliminar(u);
                      }}
                      className="items-center justify-center rounded-lg bg-error-container px-3 py-2 min-h-[36px]"
                      accessibilityRole="button"
                      accessibilityLabel={`Eliminar usuario ${u.nombre}`}
                    >
                      <MaterialIcons name="person-remove" size={18} color={colores.onErrorContainer} />
                    </Pressable>
                  </View>
                )}
              </View>
            );
          })}
        </View>
      </ScrollView>

      <ModalCrear visible={crearVisible} onClose={() => setCrearVisible(false)} onGuardar={(n, e, r) => crear.mutateAsync({ nombre: n, email: e, rol: r })} />

      <Confirmar
        visible={aEliminar !== null}
        titulo="Eliminar usuario"
        descripcion={`¿Eliminar la cuenta de "${aEliminar?.nombre}"? Los datos cliente se sobrescriben al reiniciar la demo.`}
        textoConfirmar="Eliminar"
        peligroso
        onCancelar={() => setAEliminar(null)}
        onConfirmar={() => aEliminar && void eliminar.mutateAsync(aEliminar.id)}
      />
    </View>
  );
}

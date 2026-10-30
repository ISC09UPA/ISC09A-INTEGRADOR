import React, { useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  Alert,
  Modal,
  TextInput,
} from 'react-native';
import { GlassHeader } from '@/components/ui/GlassHeader';
import { BentoCard } from '@/components/ui/BentoCard';
import { PillButton } from '@/components/ui/PillButton';
import { PlateBadge } from '@/components/ui/PlateBadge';
import { StatusBadge } from '@/components/ui/StatusBadge';
import { useVehicleStore } from '@/store/vehicleStore';
import { Radii } from '@/theme/tokens';
import { IconCamera, IconCheck, IconX } from '@/components/ui/Icons';

export const VehiclesScreen: React.FC = () => {
  const { vehicles, primaryVehicle, setPrimary, addVehicle, removeVehicle } = useVehicleStore();
  const [modalVisible, setModalVisible] = useState(false);
  const [newPlate, setNewPlate] = useState('');
  const [newBrand, setNewBrand] = useState('');
  const [newModel, setNewModel] = useState('');
  const [newColor, setNewColor] = useState('');
  const [makePrimary, setMakePrimary] = useState(false);
  const [toastMessage, setToastMessage] = useState<string | null>(null);
  const [vehicleToDelete, setVehicleToDelete] = useState<{ id: string; alias: string; plate: string } | null>(null);

  const handleSelectPrimary = (v: { id: string; alias: string; plate: string }) => {
    setPrimary(v.id);
    setToastMessage(`Vehículo ${v.alias} (${v.plate}) establecido como predeterminado.`);
    setTimeout(() => {
      setToastMessage(null);
    }, 3500);
  };

  const handleSimulateOCR = () => {
    // Simulates instant on-device camera OCR recognition
    setNewPlate('JLM-456-E');
    setNewBrand('Audi');
    setNewModel('e-tron GT');
    setNewColor('Gris Daytona');
    Alert.alert('Cámara LPR Activa', 'Placa detectada por visión artificial: JLM-456-E');
  };

  const handleSaveVehicle = () => {
    if (!newPlate.trim()) {
      Alert.alert('Placa Requerida', 'Por favor ingresa el número de placa vehicular para el reconocimiento LPR.');
      return;
    }

    const cleanPlate = newPlate.toUpperCase().trim();
    const cleanBrand = newBrand.trim() || 'Vehículo';
    const cleanModel = newModel.trim() || 'Estándar';
    const cleanColor = newColor.trim() || 'Plata';

    addVehicle({
      plate: cleanPlate,
      alias: `${cleanBrand} ${cleanModel}`.trim(),
      brand: cleanBrand,
      model: cleanModel,
      color: cleanColor,
      isPrimary: makePrimary || vehicles.length === 0,
      nfcTagLinked: true,
    });

    setToastMessage(`Vehículo ${cleanBrand} ${cleanModel} (${cleanPlate}) vinculado con éxito.`);
    setTimeout(() => {
      setToastMessage(null);
    }, 3500);

    setNewPlate('');
    setNewBrand('');
    setNewModel('');
    setNewColor('');
    setMakePrimary(false);
    setModalVisible(false);
  };

  const handleConfirmDeleteVehicle = () => {
    if (!vehicleToDelete) return;
    const v = vehicleToDelete;
    removeVehicle(v.id);
    setVehicleToDelete(null);
    setToastMessage(`Vehículo ${v.alias} (${v.plate}) desvinculado exitosamente.`);
    setTimeout(() => {
      setToastMessage(null);
    }, 3500);
  };

  return (
    <View style={styles.container}>
      <GlassHeader title="Mis Vehículos" showLogo={true} showLogout={true} />

      <ScrollView contentContainerStyle={styles.scrollContent} showsVerticalScrollIndicator={false}>
        <View style={styles.headerRow}>
          <View>
            <Text style={styles.eyebrow}>FLOTA REGISTRADA</Text>
            <Text style={styles.headline}>Vehículos Vinculados</Text>
          </View>
          <TouchableOpacity
            onPress={() => setModalVisible(true)}
            style={styles.addBtnHeader}
            activeOpacity={0.7}
          >
            <Text style={styles.addBtnHeaderText}>+ Agregar Vehículo</Text>
          </TouchableOpacity>
        </View>

        <Text style={styles.subheadline}>
          Tus vehículos registrados ingresan y salen automáticamente por las plumas LPR sin recoger boletos.
        </Text>

        {/* Toast Banner */}
        {toastMessage && (
          <View style={styles.toastBanner}>
            <IconCheck size={14} color="#16A34A" strokeWidth={2.5} />
            <Text style={styles.toastText}>{toastMessage}</Text>
          </View>
        )}

        {/* Vehicles Bento List */}
        <View style={styles.vehiclesList}>
          {vehicles.map((v) => {
            const isPrimary = primaryVehicle?.id === v.id;
            return (
              <BentoCard
                key={v.id}
                variant="elevated"
                highlightBorder={isPrimary}
                style={[styles.vehicleCard, isPrimary && styles.vehicleCardActive]}
              >
                <View style={styles.cardHeader}>
                  <PlateBadge plateNumber={v.plate} size="normal" />
                  {isPrimary ? (
                    <View style={styles.primaryBadge}>
                      <IconCheck size={10} color="#16A34A" strokeWidth={2.5} />
                      <Text style={styles.primaryBadgeText}>PREDETERMINADO</Text>
                    </View>
                  ) : (
                    <TouchableOpacity
                      onPress={() => handleSelectPrimary(v)}
                      style={styles.setPrimaryBtn}
                      activeOpacity={0.7}
                    >
                      <Text style={styles.setPrimaryBtnText}>Hacer Predeterminado</Text>
                    </TouchableOpacity>
                  )}
                </View>

                <View style={styles.detailsRow}>
                  <View>
                    <Text style={styles.carName}>{v.alias}</Text>
                    <Text style={styles.carDetails}>
                      {v.brand} · {v.model} · {v.color}
                    </Text>
                  </View>
                </View>

                {!isPrimary && (
                  <TouchableOpacity
                    style={styles.deleteBtn}
                    onPress={() => setVehicleToDelete(v)}
                    activeOpacity={0.7}
                  >
                    <Text style={styles.deleteText}>Desvincular Vehículo</Text>
                  </TouchableOpacity>
                )}
              </BentoCard>
            );
          })}
        </View>
      </ScrollView>

      {/* Modal Add Vehicle */}
      <Modal visible={modalVisible} animationType="slide" transparent>
        <View style={styles.modalOverlay}>
          <View style={styles.modalCard}>
            <View style={styles.modalHeaderRow}>
              <View>
                <Text style={styles.modalTitle}>Vincular Nuevo Vehículo</Text>
                <Text style={styles.modalSubtitle}>
                  Ingresa los datos o escanea la placa con reconocimiento LPR.
                </Text>
              </View>
              <TouchableOpacity
                onPress={() => setModalVisible(false)}
                style={styles.modalCloseBtn}
                activeOpacity={0.7}
              >
                <IconX size={15} color="rgba(0, 0, 0, 0.6)" strokeWidth={2} />
              </TouchableOpacity>
            </View>

            <TouchableOpacity
              onPress={handleSimulateOCR}
              style={styles.scanBtn}
              activeOpacity={0.7}
            >
              <View style={styles.scanBtnInner}>
                <IconCamera size={16} color="#0066CC" strokeWidth={2} />
                <Text style={styles.scanBtnText}>Reconocimiento Óptico LPR</Text>
              </View>
            </TouchableOpacity>

            {/* Número de Placa */}
            <View style={styles.inputGroup}>
              <Text style={styles.inputLabel}>NÚMERO DE PLACA (RECONOCIMIENTO LPR)</Text>
              <TextInput
                style={[styles.input, styles.monoInput]}
                placeholder="Ej. ABC-123-D"
                placeholderTextColor="rgba(0, 0, 0, 0.4)"
                value={newPlate}
                onChangeText={(t) => setNewPlate(t.toUpperCase())}
                autoCapitalize="characters"
                maxLength={10}
              />
            </View>

            {/* Marca y Modelo */}
            <View style={styles.rowInputs}>
              <View style={[styles.inputGroup, { flex: 1 }]}>
                <Text style={styles.inputLabel}>MARCA</Text>
                <TextInput
                  style={styles.input}
                  placeholder="Ej. Audi, BMW, Tesla"
                  placeholderTextColor="rgba(0, 0, 0, 0.4)"
                  value={newBrand}
                  onChangeText={setNewBrand}
                />
              </View>

              <View style={[styles.inputGroup, { flex: 1 }]}>
                <Text style={styles.inputLabel}>MODELO</Text>
                <TextInput
                  style={styles.input}
                  placeholder="Ej. e-tron, M3, Model 3"
                  placeholderTextColor="rgba(0, 0, 0, 0.4)"
                  value={newModel}
                  onChangeText={setNewModel}
                />
              </View>
            </View>

            {/* Color */}
            <View style={styles.inputGroup}>
              <Text style={styles.inputLabel}>COLOR DEL VEHÍCULO</Text>
              <TextInput
                style={styles.input}
                placeholder="Ej. Blanco Perla, Gris, Negro"
                placeholderTextColor="rgba(0, 0, 0, 0.4)"
                value={newColor}
                onChangeText={setNewColor}
              />
            </View>

            {/* Checkbox Predeterminado */}
            <TouchableOpacity
              onPress={() => setMakePrimary(!makePrimary)}
              style={styles.defaultCheckRow}
              activeOpacity={0.7}
            >
              <View style={[styles.checkbox, makePrimary && styles.checkboxActive]}>
                {makePrimary && <IconCheck size={12} color="#FFFFFF" strokeWidth={3} />}
              </View>
              <View style={{ flex: 1 }}>
                <Text style={styles.checkboxLabel}>Establecer como vehículo predeterminado</Text>
                <Text style={styles.checkboxSublabel}>
                  Se preseleccionará automáticamente al realizar reservas y accesos.
                </Text>
              </View>
            </TouchableOpacity>

            <View style={styles.modalActions}>
              <PillButton
                label="Guardar Vehículo"
                onPress={handleSaveVehicle}
                variant="primary"
                size="large"
                style={{ flex: 1 }}
              />
              <PillButton
                label="Cancelar"
                onPress={() => setModalVisible(false)}
                variant="glass"
                size="large"
                style={{ width: 105 }}
              />
            </View>
          </View>
        </View>
      </Modal>

      {/* Modal Popup de Confirmación para Desvincular Vehículo */}
      <Modal
        visible={!!vehicleToDelete}
        transparent
        animationType="fade"
        onRequestClose={() => setVehicleToDelete(null)}
      >
        <View style={styles.deleteModalOverlay}>
          <View style={styles.deleteModalCard}>
            <View style={styles.deleteModalIconBox}>
              <IconX size={20} color="#DC2626" strokeWidth={2.5} />
            </View>
            <Text style={styles.deleteModalTitle}>¿Desvincular Vehículo?</Text>
            <Text style={styles.deleteModalMessage}>
              ¿Estás seguro de que deseas eliminar el vehículo{' '}
              <Text style={styles.deleteModalHighlight}>
                {vehicleToDelete?.alias} ({vehicleToDelete?.plate})
              </Text>
              ? Las cámaras LPR ya no abrirán la pluma de acceso para esta placa automáticamente.
            </Text>

            <View style={styles.deleteModalActions}>
              <TouchableOpacity
                onPress={() => setVehicleToDelete(null)}
                style={styles.deleteModalCancelBtn}
                activeOpacity={0.7}
              >
                <Text style={styles.deleteModalCancelText}>Cancelar</Text>
              </TouchableOpacity>

              <TouchableOpacity
                onPress={handleConfirmDeleteVehicle}
                style={styles.deleteModalConfirmBtn}
                activeOpacity={0.7}
              >
                <Text style={styles.deleteModalConfirmText}>Desvincular</Text>
              </TouchableOpacity>
            </View>
          </View>
        </View>
      </Modal>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#F5F5F7',
  },
  scrollContent: {
    padding: 20,
    paddingBottom: 40,
  },
  headerRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 6,
  },
  addBtnHeader: {
    backgroundColor: '#F0F6FF',
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: Radii.pill,
  },
  addBtnHeaderText: {
    color: '#0066CC',
    fontSize: 12,
    fontWeight: '700',
  },
  eyebrow: {
    color: '#0066CC',
    fontSize: 11,
    fontWeight: '700',
    letterSpacing: 1.5,
    marginBottom: 2,
  },
  headline: {
    color: '#1D1D1F',
    fontSize: 24,
    fontWeight: '700',
    letterSpacing: -0.4,
  },
  subheadline: {
    color: 'rgba(0, 0, 0, 0.55)',
    fontSize: 13,
    lineHeight: 18,
    marginBottom: 16,
  },
  toastBanner: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#F0FDF4',
    borderWidth: 1,
    borderColor: 'rgba(22, 163, 74, 0.3)',
    borderRadius: Radii.squircleSmall,
    paddingHorizontal: 14,
    paddingVertical: 10,
    marginBottom: 14,
    gap: 8,
  },
  toastText: {
    color: '#15803D',
    fontSize: 12,
    fontWeight: '600',
    flex: 1,
  },
  vehiclesList: {
    gap: 14,
  },
  vehicleCard: {
    padding: 16,
    backgroundColor: '#FFFFFF',
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.08)',
  },
  vehicleCardActive: {
    borderColor: '#0066CC',
    borderWidth: 1.5,
    shadowColor: '#0066CC',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.08,
    shadowRadius: 6,
  },
  cardHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 14,
  },
  primaryBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    backgroundColor: 'rgba(22, 163, 74, 0.1)',
    borderWidth: 1,
    borderColor: 'rgba(22, 163, 74, 0.25)',
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: Radii.pill,
  },
  primaryBadgeText: {
    color: '#15803D',
    fontSize: 10,
    fontWeight: '800',
    letterSpacing: 0.5,
  },
  setPrimaryBtn: {
    backgroundColor: '#F0F6FF',
    borderWidth: 1,
    borderColor: 'rgba(0, 102, 204, 0.25)',
    paddingHorizontal: 10,
    paddingVertical: 4,
    borderRadius: Radii.pill,
  },
  setPrimaryBtnText: {
    color: '#0066CC',
    fontSize: 11,
    fontWeight: '700',
  },
  detailsRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-end',
  },
  carName: {
    color: '#1D1D1F',
    fontSize: 17,
    fontWeight: '700',
  },
  carDetails: {
    color: 'rgba(0, 0, 0, 0.5)',
    fontSize: 12,
    marginTop: 2,
  },
  deleteBtn: {
    marginTop: 12,
    paddingTop: 10,
    borderTopWidth: 1,
    borderTopColor: 'rgba(0, 0, 0, 0.06)',
  },
  deleteText: {
    color: '#DC2626',
    fontSize: 12,
    fontWeight: '600',
  },
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.45)',
    justifyContent: 'flex-end',
  },
  modalCard: {
    backgroundColor: '#FFFFFF',
    borderTopLeftRadius: 28,
    borderTopRightRadius: 28,
    padding: 24,
    paddingBottom: 36,
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.08)',
    shadowColor: '#000000',
    shadowOffset: { width: 0, height: -6 },
    shadowOpacity: 0.12,
    shadowRadius: 20,
    elevation: 10,
    maxWidth: 640,
    width: '100%',
    alignSelf: 'center',
  },
  modalHeaderRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    marginBottom: 12,
  },
  modalCloseBtn: {
    width: 28,
    height: 28,
    borderRadius: 14,
    backgroundColor: 'rgba(0, 0, 0, 0.06)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  modalTitle: {
    color: '#1D1D1F',
    fontSize: 20,
    fontWeight: '700',
    marginBottom: 4,
  },
  modalSubtitle: {
    color: 'rgba(0, 0, 0, 0.55)',
    fontSize: 12,
    lineHeight: 16,
  },
  scanBtn: {
    backgroundColor: '#F0F6FF',
    borderWidth: 1,
    borderColor: 'rgba(0, 102, 204, 0.25)',
    borderRadius: Radii.squircleSmall,
    paddingVertical: 10,
    alignItems: 'center',
    marginBottom: 16,
  },
  scanBtnInner: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
  },
  scanBtnText: {
    color: '#0066CC',
    fontSize: 12,
    fontWeight: '700',
  },
  inputGroup: {
    marginBottom: 12,
  },
  inputLabel: {
    fontSize: 10,
    fontWeight: '700',
    color: 'rgba(0, 0, 0, 0.55)',
    letterSpacing: 0.7,
    marginBottom: 4,
  },
  input: {
    height: 44,
    backgroundColor: '#F5F5F7',
    borderWidth: 1.5,
    borderColor: 'rgba(0, 0, 0, 0.12)',
    borderRadius: Radii.squircleSmall,
    paddingHorizontal: 14,
    color: '#1D1D1F',
    fontSize: 14,
    fontWeight: '500',
  },
  monoInput: {
    fontFamily: 'Courier',
    fontWeight: '700',
    letterSpacing: 1.5,
    fontSize: 15,
  },
  rowInputs: {
    flexDirection: 'row',
    gap: 10,
  },
  defaultCheckRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    marginVertical: 10,
    backgroundColor: '#F8F9FA',
    padding: 10,
    borderRadius: Radii.squircleSmall,
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.06)',
  },
  checkbox: {
    width: 20,
    height: 20,
    borderRadius: 5,
    borderWidth: 1.5,
    borderColor: 'rgba(0, 0, 0, 0.25)',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: '#FFFFFF',
  },
  checkboxActive: {
    backgroundColor: '#0066CC',
    borderColor: '#0066CC',
  },
  checkboxLabel: {
    fontSize: 12,
    fontWeight: '700',
    color: '#1D1D1F',
  },
  checkboxSublabel: {
    fontSize: 10,
    color: 'rgba(0, 0, 0, 0.5)',
    marginTop: 2,
    lineHeight: 14,
  },
  modalActions: {
    flexDirection: 'row',
    gap: 10,
    marginTop: 10,
  },

  // Delete Confirmation Modal Styles
  deleteModalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.5)',
    justifyContent: 'center',
    alignItems: 'center',
    padding: 20,
  },
  deleteModalCard: {
    backgroundColor: '#FFFFFF',
    borderRadius: 22,
    padding: 24,
    maxWidth: 400,
    width: '100%',
    alignItems: 'center',
    shadowColor: '#000000',
    shadowOffset: { width: 0, height: 10 },
    shadowOpacity: 0.18,
    shadowRadius: 28,
    elevation: 12,
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.08)',
  },
  deleteModalIconBox: {
    width: 52,
    height: 52,
    borderRadius: 26,
    backgroundColor: 'rgba(220, 38, 38, 0.1)',
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 14,
  },
  deleteModalTitle: {
    fontSize: 19,
    fontWeight: '700',
    color: '#1D1D1F',
    marginBottom: 8,
    textAlign: 'center',
  },
  deleteModalMessage: {
    fontSize: 13,
    color: 'rgba(0, 0, 0, 0.6)',
    textAlign: 'center',
    lineHeight: 18,
    marginBottom: 22,
  },
  deleteModalHighlight: {
    fontWeight: '700',
    color: '#1D1D1F',
  },
  deleteModalActions: {
    flexDirection: 'row',
    gap: 10,
    width: '100%',
  },
  deleteModalCancelBtn: {
    flex: 1,
    paddingVertical: 12,
    borderRadius: Radii.pill,
    backgroundColor: '#F5F5F7',
    alignItems: 'center',
  },
  deleteModalCancelText: {
    fontSize: 13,
    fontWeight: '600',
    color: '#1D1D1F',
  },
  deleteModalConfirmBtn: {
    flex: 1,
    paddingVertical: 12,
    borderRadius: Radii.pill,
    backgroundColor: '#DC2626',
    alignItems: 'center',
  },
  deleteModalConfirmText: {
    fontSize: 13,
    fontWeight: '700',
    color: '#FFFFFF',
  },
});

export default VehiclesScreen;

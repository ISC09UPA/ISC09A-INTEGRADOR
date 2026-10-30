import React, { useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  TextInput,
  Alert,
  Modal,
} from 'react-native';
import { GlassHeader } from '@/components/ui/GlassHeader';
import { BentoCard } from '@/components/ui/BentoCard';
import { PillButton } from '@/components/ui/PillButton';
import { PlateBadge } from '@/components/ui/PlateBadge';
import { StatusBadge } from '@/components/ui/StatusBadge';
import { usePaymentStore, CreditCard } from '@/store/paymentStore';
import { useAuthStore } from '@/store/authStore';
import { Radii } from '@/theme/tokens';
import { IconReceipt, IconCheck, IconX, IconCreditCard } from '@/components/ui/Icons';

export const WalletScreen: React.FC = () => {
  const { user } = useAuthStore();
  const { cards, transactions, addCard, removeCard, setDefaultCard } = usePaymentStore();

  const [isAddingCard, setIsAddingCard] = useState(false);
  const [cardNumber, setCardNumber] = useState('');
  const [cardHolder, setCardHolder] = useState(user?.fullName || '');
  const [expiry, setExpiry] = useState('');
  const [cvv, setCvv] = useState('');
  const [cardBrand, setCardBrand] = useState<'VISA' | 'MASTERCARD' | 'AMEX'>('VISA');
  const [makeDefault, setMakeDefault] = useState(false);
  const [toastMessage, setToastMessage] = useState<string | null>(null);
  const [cardToDelete, setCardToDelete] = useState<CreditCard | null>(null);

  const handleSelectDefault = (card: CreditCard) => {
    setDefaultCard(card.id);
    setToastMessage(`Tarjeta ${card.brand} terminada en ${card.last4} establecida como predeterminada para cobros en carril y reservas.`);
    setTimeout(() => {
      setToastMessage(null);
    }, 4000);
  };

  const handleConfirmDelete = () => {
    if (!cardToDelete) return;
    const deleted = cardToDelete;
    removeCard(deleted.id);
    setCardToDelete(null);
    setToastMessage(`Tarjeta ${deleted.brand} terminada en ${deleted.last4} ha sido desvinculada.`);
    setTimeout(() => {
      setToastMessage(null);
    }, 4000);
  };

  // Auto detectar marca por primer dígito
  const handleCardNumberChange = (text: string) => {
    // Solo números y espacios
    const cleaned = text.replace(/[^0-9]/g, '');
    let formatted = '';
    for (let i = 0; i < cleaned.length && i < 16; i++) {
      if (i > 0 && i % 4 === 0) formatted += ' ';
      formatted += cleaned[i];
    }
    setCardNumber(formatted);

    if (cleaned.startsWith('4')) setCardBrand('VISA');
    else if (cleaned.startsWith('5') || cleaned.startsWith('2')) setCardBrand('MASTERCARD');
    else if (cleaned.startsWith('3')) setCardBrand('AMEX');
  };

  const handleExpiryChange = (text: string) => {
    const cleaned = text.replace(/[^0-9]/g, '');
    let formatted = cleaned;
    if (cleaned.length > 2) {
      formatted = `${cleaned.slice(0, 2)}/${cleaned.slice(2, 4)}`;
    }
    setExpiry(formatted);
  };

  const handleSaveCard = () => {
    const rawNumber = cardNumber.replace(/\s/g, '');
    if (rawNumber.length < 15) {
      Alert.alert('Tarjeta Inválida', 'Por favor ingresa los 16 dígitos de tu tarjeta.');
      return;
    }
    if (!cardHolder.trim()) {
      Alert.alert('Titular Requerido', 'Por favor ingresa el nombre como aparece en el plástico.');
      return;
    }
    if (expiry.length < 5) {
      Alert.alert('Fecha Inválida', 'Ingresa la fecha de vencimiento en formato MM/AA.');
      return;
    }
    if (cvv.length < 3) {
      Alert.alert('CVV Inválido', 'Ingresa el código de seguridad de 3 o 4 dígitos.');
      return;
    }

    const last4 = rawNumber.slice(-4);
    const masked = `•••• •••• •••• ${last4}`;

    addCard({
      brand: cardBrand,
      cardNumberMasked: masked,
      last4,
      holderName: cardHolder.trim(),
      expiry,
      isDefault: makeDefault || cards.length === 0,
      theme: cardBrand === 'VISA' ? 'black' : cardBrand === 'MASTERCARD' ? 'blue' : 'platinum',
    });

    Alert.alert('¡Tarjeta Guardada!', `Tu tarjeta ${cardBrand} terminada en ${last4} ha sido vinculada de forma segura.`);

    // Limpiar formulario y cerrar
    setCardNumber('');
    setExpiry('');
    setCvv('');
    setIsAddingCard(false);
  };

  const handleDeleteCard = (card: CreditCard) => {
    Alert.alert(
      'Eliminar Tarjeta',
      `¿Deseas desvincular tu tarjeta ${card.brand} terminada en ${card.last4}?`,
      [
        { text: 'Cancelar', style: 'cancel' },
        {
          text: 'Eliminar',
          style: 'destructive',
          onPress: () => removeCard(card.id),
        },
      ]
    );
  };

  const handleShowReceipt = (txId: string) => {
    const tx = transactions.find((t) => t.id === txId);
    if (!tx) return;

    Alert.alert(
      'Comprobante Fiscal CFDI 4.0',
      `ID Transacción: ${tx.id}\nConcepto: ${tx.concept}\nFecha: ${tx.date}\nMonto: ${tx.amountFormatted}\nPlaca: ${tx.plate}\nMétodo: ${tx.paymentMethod}\nFolio SAT: ${tx.uuidSat || 'f4b2-901a-8831'}\n\nEl comprobante PDF y XML han sido enviados a ${user?.email || 'tu correo registrado'}.`
    );
  };

  const defaultCard = cards.find((c) => c.isDefault) || cards[0];

  return (
    <View style={styles.container}>
      <GlassHeader title="Wallet" showLogo={true} showLogout={true} />

      <ScrollView contentContainerStyle={styles.scrollContent} showsVerticalScrollIndicator={false}>
        {/* Title Header */}
        <View style={styles.headerBox}>
          <Text style={styles.eyebrow}>AEROPARK CASHLESS WALLET</Text>
          <Text style={styles.headline}>Métodos de Pago y Cobros</Text>
          <Text style={styles.subtitle}>
            Administra tus tarjetas guardadas, selecciona la predeterminada para cobros en carril LPR y consulta tus comprobantes.
          </Text>
        </View>

        {/* Toast de confirmación de cambio de tarjeta predeterminada */}
        {toastMessage && (
          <View style={styles.toastBanner}>
            <IconCheck size={14} color="#16A34A" strokeWidth={2.5} />
            <Text style={styles.toastText}>{toastMessage}</Text>
          </View>
        )}

        {/* Featured Apple Wallet Digital Card */}
        {defaultCard ? (
          <View style={styles.featuredCardContainer}>
            <View
              style={[
                styles.walletCardVisual,
                defaultCard.theme === 'black'
                  ? styles.cardThemeBlack
                  : defaultCard.theme === 'blue'
                  ? styles.cardThemeBlue
                  : styles.cardThemePlatinum,
              ]}
            >
              <View style={styles.cardVisualHeader}>
                <Text style={styles.cardBrandWatermark}>{defaultCard.brand}</Text>
                <View style={styles.defaultBadge}>
                  <Text style={styles.defaultBadgeText}>● TARJETA PREDETERMINADA</Text>
                </View>
              </View>

              <View style={styles.cardChipRow}>
                <Text style={styles.chipSymbol}>◈</Text>
                <Text style={styles.contactlessSymbol}>◎</Text>
              </View>

              <Text style={styles.cardMaskedNumber}>{defaultCard.cardNumberMasked}</Text>

              <View style={styles.cardVisualFooter}>
                <View>
                  <Text style={styles.cardFooterLabel}>TITULAR</Text>
                  <Text style={styles.cardFooterValue}>{defaultCard.holderName}</Text>
                </View>
                <View style={{ alignItems: 'flex-end' }}>
                  <Text style={styles.cardFooterLabel}>EXPIRA</Text>
                  <Text style={styles.cardFooterValue}>{defaultCard.expiry}</Text>
                </View>
              </View>
            </View>
          </View>
        ) : null}

        {/* Saved Cards Section */}
        <View style={styles.sectionHeaderRow}>
          <View>
            <Text style={styles.sectionTitle}>TARJETAS GUARDADAS ({cards.length})</Text>
            <Text style={styles.sectionSub}>Selecciona la tarjeta predeterminada para cobro automático</Text>
          </View>
          <TouchableOpacity
            onPress={() => setIsAddingCard(!isAddingCard)}
            style={styles.addCardBtnHeader}
            activeOpacity={0.7}
          >
            <Text style={styles.addCardBtnText}>
              {isAddingCard ? 'Cerrar' : '+ Agregar'}
            </Text>
          </TouchableOpacity>
        </View>

        {/* Formulario Expandible para Agregar Tarjeta */}
        {isAddingCard && (
          <BentoCard variant="elevated" style={styles.addCardFormCard}>
            <Text style={styles.formCardTitle}>Nueva Tarjeta de Crédito o Débito</Text>
            <Text style={styles.formCardSub}>
              Tus datos se almacenan cifrados con hardware enclave y estándares PCI-DSS.
            </Text>

            {/* Selector de Marca */}
            <View style={styles.brandSelectorRow}>
              {(['VISA', 'MASTERCARD', 'AMEX'] as const).map((b) => (
                <TouchableOpacity
                  key={b}
                  onPress={() => setCardBrand(b)}
                  style={[
                    styles.brandChip,
                    cardBrand === b && styles.brandChipSelected,
                  ]}
                  activeOpacity={0.7}
                >
                  <Text
                    style={[
                      styles.brandChipText,
                      cardBrand === b && styles.brandChipTextSelected,
                    ]}
                  >
                    {b}
                  </Text>
                </TouchableOpacity>
              ))}
            </View>

            {/* Número de Tarjeta */}
            <View style={styles.inputGroup}>
              <Text style={styles.inputLabel}>NÚMERO DE TARJETA (16 DÍGITOS)</Text>
              <TextInput
                style={[styles.input, styles.monoInput]}
                value={cardNumber}
                onChangeText={handleCardNumberChange}
                placeholder="4152 3134 5678 9104"
                placeholderTextColor="rgba(0, 0, 0, 0.35)"
                keyboardType="numeric"
                maxLength={19}
              />
            </View>

            {/* Nombre Titular */}
            <View style={styles.inputGroup}>
              <Text style={styles.inputLabel}>NOMBRE DEL TITULAR</Text>
              <TextInput
                style={styles.input}
                value={cardHolder}
                onChangeText={setCardHolder}
                placeholder="Como aparece en el plástico"
                placeholderTextColor="rgba(0, 0, 0, 0.35)"
                autoCapitalize="words"
              />
            </View>

            {/* Vencimiento y CVV */}
            <View style={styles.rowInputs}>
              <View style={[styles.inputGroup, { flex: 1 }]}>
                <Text style={styles.inputLabel}>VENCE (MM/AA)</Text>
                <TextInput
                  style={[styles.input, styles.monoInput]}
                  value={expiry}
                  onChangeText={handleExpiryChange}
                  placeholder="12/28"
                  placeholderTextColor="rgba(0, 0, 0, 0.35)"
                  keyboardType="numeric"
                  maxLength={5}
                />
              </View>

              <View style={[styles.inputGroup, { flex: 1 }]}>
                <Text style={styles.inputLabel}>CVV (SEGURIDAD)</Text>
                <TextInput
                  style={[styles.input, styles.monoInput]}
                  value={cvv}
                  onChangeText={(t) => setCvv(t.replace(/[^0-9]/g, '').slice(0, 4))}
                  placeholder="•••"
                  placeholderTextColor="rgba(0, 0, 0, 0.35)"
                  keyboardType="numeric"
                  secureTextEntry
                  maxLength={4}
                />
              </View>
            </View>

            {/* Checkbox Default */}
            <TouchableOpacity
              onPress={() => setMakeDefault(!makeDefault)}
              style={styles.defaultCheckRow}
              activeOpacity={0.7}
            >
              <View style={[styles.checkbox, makeDefault && styles.checkboxActive]}>
                {makeDefault && <IconCheck size={12} color="#FFFFFF" strokeWidth={3} />}
              </View>
              <View style={{ flex: 1 }}>
                <Text style={styles.checkboxLabel}>Establecer como tarjeta predeterminada</Text>
                <Text style={styles.checkboxSublabel}>
                  Se utilizará de manera automática al abrir la pluma de salida y en tus reservas.
                </Text>
              </View>
            </TouchableOpacity>

            <PillButton
              label="Guardar Tarjeta en Enclave Cifrado"
              onPress={handleSaveCard}
              variant="primary"
              size="large"
              style={styles.saveBtn}
            />
          </BentoCard>
        )}

        {/* List of Saved Cards con selector claro de predeterminada */}
        <View style={styles.cardsList}>
          {cards.map((c) => {
            const isDefault = c.isDefault;
            return (
              <View
                key={c.id}
                style={[
                  styles.cardItemRow,
                  isDefault && styles.cardItemRowActive,
                ]}
              >
                {/* Zona interactiva para elegir predeterminada */}
                <TouchableOpacity
                  style={styles.cardItemLeftTouchable}
                  onPress={() => handleSelectDefault(c)}
                  activeOpacity={0.7}
                >
                  {/* Selector Radio Indicator */}
                  <View style={styles.radioArea}>
                    <View style={[styles.radioCircle, isDefault && styles.radioCircleActive]}>
                      {isDefault && <View style={styles.radioCircleCore} />}
                    </View>
                  </View>

                  {/* Brand Badge */}
                  <View style={styles.cardBrandBadge}>
                    <Text style={styles.cardBrandBadgeText}>{c.brand}</Text>
                  </View>

                  {/* Info Text */}
                  <View style={styles.cardItemInfo}>
                    <View style={styles.cardItemTitleRow}>
                      <Text style={styles.cardItemTitle}>
                        {c.brand} terminada en {c.last4}
                      </Text>
                      {isDefault && (
                        <View style={styles.activeCardBadge}>
                          <IconCheck size={10} color="#15803D" strokeWidth={2.5} />
                          <Text style={styles.activeCardBadgeText}>PREDETERMINADA</Text>
                        </View>
                      )}
                    </View>
                    <Text style={styles.cardItemSub}>
                      {isDefault
                        ? `Expira ${c.expiry} · Cobro automático activo en pluma`
                        : `Expira ${c.expiry} · Toca para seleccionar como predeterminada`}
                    </Text>
                  </View>
                </TouchableOpacity>

                {/* Right Action */}
                <View style={styles.cardItemRight}>
                  {!isDefault ? (
                    <TouchableOpacity
                      onPress={() => handleSelectDefault(c)}
                      style={styles.setDefaultActionBtn}
                      activeOpacity={0.7}
                    >
                      <Text style={styles.setDefaultActionText}>Hacer Predeterminada</Text>
                    </TouchableOpacity>
                  ) : null}

                  <TouchableOpacity
                    onPress={() => setCardToDelete(c)}
                    style={styles.trashBtn}
                    activeOpacity={0.7}
                    accessibilityLabel="Eliminar tarjeta"
                  >
                    <IconX size={14} color="#64748B" strokeWidth={2} />
                  </TouchableOpacity>
                </View>
              </View>
            );
          })}
        </View>

        {/* Charge & Stays History */}
        <View style={[styles.sectionHeaderRow, { marginTop: 28 }]}>
          <Text style={styles.sectionTitle}>HISTORIAL DE COBROS Y ESTANCIAS</Text>
          <Text style={styles.recordsCount}>{transactions.length} movimientos</Text>
        </View>

        <View style={styles.transactionsList}>
          {transactions.map((tx) => (
            <BentoCard key={tx.id} variant="surface" style={styles.txCard}>
              <View style={styles.txTopRow}>
                <View style={{ flex: 1 }}>
                  <Text style={styles.txConcept}>{tx.concept}</Text>
                  <Text style={styles.txDate}>{tx.date}</Text>
                </View>
                <View style={{ alignItems: 'flex-end' }}>
                  <Text style={styles.txAmount}>-{tx.amountFormatted}</Text>
                  <StatusBadge label="COBRADO" variant="success" />
                </View>
              </View>

              <View style={styles.txDivider} />

              <View style={styles.txBottomRow}>
                <View style={styles.txPlateBox}>
                  <PlateBadge plateNumber={tx.plate} size="normal" />
                  <Text style={styles.txMethodText}>{tx.paymentMethod}</Text>
                </View>

                <TouchableOpacity
                  onPress={() => handleShowReceipt(tx.id)}
                  style={styles.receiptLinkBtn}
                  activeOpacity={0.7}
                >
                  <View style={{ flexDirection: 'row', alignItems: 'center', gap: 4 }}>
                    <IconReceipt size={13} color="#0066CC" strokeWidth={1.8} />
                    <Text style={styles.receiptLinkText}>CFDI 4.0 →</Text>
                  </View>
                </TouchableOpacity>
              </View>
            </BentoCard>
          ))}
        </View>
      </ScrollView>

      {/* Modal Popup de Confirmación de Eliminación */}
      <Modal
        visible={!!cardToDelete}
        transparent
        animationType="fade"
        onRequestClose={() => setCardToDelete(null)}
      >
        <View style={styles.deleteModalOverlay}>
          <View style={styles.deleteModalCard}>
            <View style={styles.deleteModalIconBox}>
              <IconX size={20} color="#DC2626" strokeWidth={2.5} />
            </View>
            <Text style={styles.deleteModalTitle}>¿Eliminar Tarjeta?</Text>
            <Text style={styles.deleteModalMessage}>
              ¿Estás seguro de que deseas desvincular tu tarjeta{' '}
              <Text style={styles.deleteModalHighlight}>
                {cardToDelete?.brand} terminada en {cardToDelete?.last4}
              </Text>
              ? Esta acción eliminará el método de pago de tu cuenta y no podrá usarse en carriles automáticos.
            </Text>

            <View style={styles.deleteModalActions}>
              <TouchableOpacity
                onPress={() => setCardToDelete(null)}
                style={styles.deleteModalCancelBtn}
                activeOpacity={0.7}
              >
                <Text style={styles.deleteModalCancelText}>Cancelar</Text>
              </TouchableOpacity>

              <TouchableOpacity
                onPress={handleConfirmDelete}
                style={styles.deleteModalConfirmBtn}
                activeOpacity={0.7}
              >
                <Text style={styles.deleteModalConfirmText}>Eliminar Tarjeta</Text>
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
    backgroundColor: '#FFFFFF',
  },
  scrollContent: {
    paddingHorizontal: 16,
    paddingTop: 12,
    paddingBottom: 80,
    maxWidth: 640,
    width: '100%',
    alignSelf: 'center',
  },
  headerBox: {
    marginBottom: 16,
  },
  eyebrow: {
    fontSize: 10,
    fontWeight: '700',
    color: '#0066CC',
    letterSpacing: 1.2,
    marginBottom: 2,
  },
  headline: {
    fontSize: 26,
    fontWeight: '700',
    color: '#1D1D1F',
    letterSpacing: -0.5,
  },
  subtitle: {
    fontSize: 12,
    color: 'rgba(0, 0, 0, 0.55)',
    marginTop: 2,
    lineHeight: 16,
  },
  // Toast Banner
  toastBanner: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#F0FDF4',
    borderWidth: 1,
    borderColor: 'rgba(22, 163, 74, 0.3)',
    borderRadius: Radii.squircleSmall,
    paddingHorizontal: 14,
    paddingVertical: 10,
    marginBottom: 16,
    gap: 8,
  },
  toastIcon: {
    color: '#16A34A',
    fontSize: 14,
    fontWeight: '800',
  },
  toastText: {
    color: '#15803D',
    fontSize: 12,
    fontWeight: '600',
    flex: 1,
    lineHeight: 16,
  },

  // Featured Visual Card (Apple Wallet aesthetic)
  featuredCardContainer: {
    marginBottom: 20,
  },
  walletCardVisual: {
    height: 190,
    borderRadius: Radii.squircleMedium,
    padding: 20,
    justifyContent: 'space-between',
    shadowColor: '#000000',
    shadowOffset: { width: 0, height: 6 },
    shadowOpacity: 0.16,
    shadowRadius: 12,
    elevation: 4,
  },
  cardThemeBlack: {
    backgroundColor: '#1C1D21',
  },
  cardThemeBlue: {
    backgroundColor: '#0F2B48',
  },
  cardThemePlatinum: {
    backgroundColor: '#2A2A2E',
  },
  cardVisualHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  cardBrandWatermark: {
    color: '#FFFFFF',
    fontSize: 16,
    fontWeight: '800',
    letterSpacing: 1.5,
  },
  defaultBadge: {
    backgroundColor: 'rgba(22, 163, 74, 0.2)',
    borderWidth: 1,
    borderColor: 'rgba(22, 163, 74, 0.35)',
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: Radii.pill,
  },
  defaultBadgeText: {
    color: '#DCFCE7',
    fontSize: 9,
    fontWeight: '800',
    letterSpacing: 0.8,
  },
  cardChipRow: {
    flexDirection: 'row',
    gap: 12,
    alignItems: 'center',
  },
  chipSymbol: {
    fontSize: 22,
    color: '#D4AF37',
  },
  contactlessSymbol: {
    fontSize: 18,
    color: 'rgba(255, 255, 255, 0.7)',
  },
  cardMaskedNumber: {
    color: '#FFFFFF',
    fontSize: 17,
    fontFamily: 'Courier',
    fontWeight: '700',
    letterSpacing: 2,
  },
  cardVisualFooter: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-end',
  },
  cardFooterLabel: {
    color: 'rgba(255, 255, 255, 0.5)',
    fontSize: 8,
    fontWeight: '700',
    letterSpacing: 0.8,
    marginBottom: 2,
  },
  cardFooterValue: {
    color: '#FFFFFF',
    fontSize: 13,
    fontWeight: '700',
  },

  // Sections
  sectionHeaderRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 10,
  },
  sectionTitle: {
    fontSize: 11,
    fontWeight: '700',
    color: 'rgba(0, 0, 0, 0.65)',
    letterSpacing: 0.8,
  },
  sectionSub: {
    fontSize: 10,
    color: 'rgba(0, 0, 0, 0.45)',
    marginTop: 2,
  },
  addCardBtnHeader: {
    paddingVertical: 4,
    paddingHorizontal: 10,
    backgroundColor: '#F0F6FF',
    borderRadius: Radii.pill,
  },
  addCardBtnText: {
    color: '#0066CC',
    fontSize: 11,
    fontWeight: '700',
  },
  recordsCount: {
    fontSize: 11,
    color: 'rgba(0, 0, 0, 0.45)',
  },

  // Add Card Form
  addCardFormCard: {
    backgroundColor: '#FFFFFF',
    borderWidth: 1.5,
    borderColor: '#0066CC',
    borderRadius: Radii.squircleMedium,
    padding: 16,
    marginBottom: 16,
  },
  formCardTitle: {
    fontSize: 16,
    fontWeight: '700',
    color: '#1D1D1F',
    marginBottom: 2,
  },
  formCardSub: {
    fontSize: 11,
    color: 'rgba(0, 0, 0, 0.5)',
    marginBottom: 14,
    lineHeight: 15,
  },
  brandSelectorRow: {
    flexDirection: 'row',
    gap: 8,
    marginBottom: 12,
  },
  brandChip: {
    flex: 1,
    paddingVertical: 6,
    alignItems: 'center',
    backgroundColor: '#F5F5F7',
    borderRadius: Radii.pill,
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.08)',
  },
  brandChipSelected: {
    backgroundColor: '#0066CC',
    borderColor: '#0066CC',
  },
  brandChipText: {
    fontSize: 11,
    fontWeight: '700',
    color: 'rgba(0, 0, 0, 0.6)',
  },
  brandChipTextSelected: {
    color: '#FFFFFF',
  },
  inputGroup: {
    marginBottom: 10,
  },
  inputLabel: {
    fontSize: 9,
    fontWeight: '700',
    color: 'rgba(0, 0, 0, 0.5)',
    letterSpacing: 0.6,
    marginBottom: 4,
  },
  input: {
    height: 42,
    backgroundColor: '#F5F5F7',
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.1)',
    borderRadius: Radii.squircleSmall,
    paddingHorizontal: 12,
    color: '#1D1D1F',
    fontSize: 13,
  },
  monoInput: {
    fontFamily: 'Courier',
    fontWeight: '700',
    letterSpacing: 1,
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
  checkIcon: {
    color: '#FFFFFF',
    fontSize: 11,
    fontWeight: '800',
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
  saveBtn: {
    marginTop: 8,
  },

  // Cards List con Selección de Predeterminada
  cardsList: {
    gap: 10,
    marginBottom: 16,
  },
  cardItemRow: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#FFFFFF',
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.08)',
    borderRadius: Radii.squircleSmall,
    padding: 12,
  },
  cardItemRowActive: {
    backgroundColor: '#FAFCFF',
    borderColor: '#0066CC',
    borderWidth: 1.5,
    shadowColor: '#0066CC',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.08,
    shadowRadius: 6,
    elevation: 2,
  },
  radioArea: {
    marginRight: 10,
    alignItems: 'center',
    justifyContent: 'center',
  },
  radioCircle: {
    width: 20,
    height: 20,
    borderRadius: 10,
    borderWidth: 1.8,
    borderColor: 'rgba(0, 0, 0, 0.25)',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: '#FFFFFF',
  },
  radioCircleActive: {
    borderColor: '#0066CC',
  },
  radioCircleCore: {
    width: 10,
    height: 10,
    borderRadius: 5,
    backgroundColor: '#0066CC',
  },
  cardBrandBadge: {
    backgroundColor: '#1D1D1F',
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 6,
  },
  cardBrandBadgeText: {
    color: '#FFFFFF',
    fontSize: 9,
    fontWeight: '800',
    letterSpacing: 0.5,
  },
  cardItemInfo: {
    flex: 1,
    marginLeft: 10,
  },
  cardItemTitleRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    flexWrap: 'wrap',
  },
  cardItemTitle: {
    fontSize: 13,
    fontWeight: '700',
    color: '#1D1D1F',
  },
  cardItemSub: {
    fontSize: 11,
    color: 'rgba(0, 0, 0, 0.5)',
    marginTop: 2,
  },
  cardItemRight: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    marginLeft: 8,
  },
  activeCardBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    backgroundColor: 'rgba(22, 163, 74, 0.1)',
    borderWidth: 1,
    borderColor: 'rgba(22, 163, 74, 0.25)',
    paddingHorizontal: 7,
    paddingVertical: 2,
    borderRadius: Radii.pill,
  },
  activeCardBadgeText: {
    color: '#15803D',
    fontSize: 9,
    fontWeight: '800',
    letterSpacing: 0.4,
  },
  setDefaultActionBtn: {
    backgroundColor: '#F0F6FF',
    borderWidth: 1,
    borderColor: 'rgba(0, 102, 204, 0.25)',
    paddingHorizontal: 10,
    paddingVertical: 5,
    borderRadius: Radii.pill,
  },
  setDefaultActionText: {
    fontSize: 10,
    fontWeight: '700',
    color: '#0066CC',
  },
  cardItemLeftTouchable: {
    flexDirection: 'row',
    alignItems: 'center',
    flex: 1,
  },
  trashBtn: {
    width: 32,
    height: 32,
    borderRadius: 16,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: 'rgba(0, 0, 0, 0.05)',
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.04)',
  },
  trashText: {
    fontSize: 12,
    color: 'rgba(0, 0, 0, 0.55)',
    fontWeight: '700',
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

  // Transactions / Charges
  transactionsList: {
    gap: 10,
    marginBottom: 20,
  },
  txCard: {
    backgroundColor: '#FFFFFF',
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.08)',
    padding: 14,
  },
  txTopRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
  },
  txConcept: {
    fontSize: 14,
    fontWeight: '700',
    color: '#1D1D1F',
  },
  txDate: {
    fontSize: 11,
    color: 'rgba(0, 0, 0, 0.45)',
    marginTop: 2,
  },
  txAmount: {
    fontSize: 15,
    fontWeight: '800',
    color: '#1D1D1F',
    marginBottom: 4,
  },
  txDivider: {
    height: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.06)',
    marginVertical: 10,
  },
  txBottomRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  txPlateBox: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    flex: 1,
  },
  txMethodText: {
    fontSize: 11,
    color: 'rgba(0, 0, 0, 0.5)',
  },
  receiptLinkBtn: {
    paddingVertical: 4,
    paddingHorizontal: 8,
    backgroundColor: '#F5F5F7',
    borderRadius: Radii.pill,
  },
  receiptLinkText: {
    fontSize: 10,
    fontWeight: '700',
    color: '#0066CC',
  },
});

export default WalletScreen;

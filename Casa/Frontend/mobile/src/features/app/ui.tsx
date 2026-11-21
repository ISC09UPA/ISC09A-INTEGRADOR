import type { ReactNode } from 'react';
import { Pressable, StyleSheet, Text, TextInput, View, type TextInputProps } from 'react-native';
import { tokens } from '../../shared/theme/tokens';
const c = tokens.colors;
export function Copy({ children, muted = false }: { children: ReactNode; muted?: boolean }) { return <Text style={[styles.copy, muted && { color: c.secondaryText }]}>{children}</Text>; }
export function Title({ children }: { children: ReactNode }) { return <Text accessibilityRole="header" style={styles.title}>{children}</Text>; }
export function Card({ title, children }: { title?: string; children: ReactNode }) { return <View style={styles.card}>{title && <Text accessibilityRole="header" style={styles.cardTitle}>{title}</Text>}{children}</View>; }
export function Button({ label, onPress, disabled = false, secondary = false }: { label: string; onPress: () => void; disabled?: boolean; secondary?: boolean }) { return <Pressable accessibilityRole="button" accessibilityState={{ disabled }} disabled={disabled} onPress={onPress} style={({ pressed }) => [styles.button, secondary && styles.secondary, { opacity: disabled ? .45 : pressed ? .7 : 1 }]}><Text style={[styles.buttonText, secondary && { color: c.primaryText }]}>{label}</Text></Pressable>; }
export function Note({ children, error = false }: { children: ReactNode; error?: boolean }) { return <View accessibilityLiveRegion="polite" style={[styles.note, error && { backgroundColor: c.errorBackground }]}><Text style={{ color: error ? c.errorText : c.pendingText, fontSize: 14, lineHeight: 21 }}>{children}</Text></View>; }
export function Field({ label, ...props }: TextInputProps & { label: string }) { return <View style={{ gap: 8 }}><Copy>{label}</Copy><TextInput accessibilityLabel={label} placeholderTextColor={c.secondaryText} style={styles.input} {...props} /></View>; }
export function Empty({ text = 'Todavía no hay información disponible.' }: { text?: string }) { return <Card title="Sin datos"><Copy muted>{text}</Copy></Card>; }
export function date(value?: string) { return value && Number.isFinite(Date.parse(value)) ? new Date(value).toLocaleString('es-MX') : 'Sin reporte'; }
export const styles = StyleSheet.create({
  page: { flex: 1, backgroundColor: c.background }, content: { padding: 20, gap: 18, paddingBottom: 36 },
  title: { color: c.primaryText, fontSize: 28, fontWeight: '700', lineHeight: 35 },
  copy: { color: c.primaryText, fontSize: 16, lineHeight: 24 },
  card: { borderWidth: 1, borderColor: c.border, borderRadius: 20, backgroundColor: c.surface, padding: 20, gap: 12 },
  cardTitle: { color: c.primaryText, fontSize: 19, fontWeight: '600' },
  button: { minHeight: 48, borderRadius: 13, backgroundColor: c.action, padding: 14, justifyContent: 'center', alignItems: 'center' },
  secondary: { backgroundColor: c.confirmation }, buttonText: { color: '#fff', fontSize: 15, fontWeight: '600', textAlign: 'center' },
  note: { padding: 15, borderRadius: 13, backgroundColor: c.pendingBackground },
  input: { minHeight: 50, borderWidth: 1, borderColor: c.border, borderRadius: 13, padding: 14, color: c.primaryText, backgroundColor: c.surface, fontSize: 16 },
  row: { flexDirection: 'row', flexWrap: 'wrap', gap: 10, alignItems: 'center' },
  nav: { flexDirection: 'row', borderTopWidth: 1, borderColor: c.border, backgroundColor: c.surface },
  navItem: { flex: 1, minHeight: 60, justifyContent: 'center', alignItems: 'center', paddingVertical: 12, paddingHorizontal: 2 },
});

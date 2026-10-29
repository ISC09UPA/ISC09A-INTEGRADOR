import { StyleSheet, View, type StyleProp, type ViewStyle } from 'react-native';

import { Button, IconButton } from './button';
import { Icons } from './icon';
import { TextField } from './text-field';
import { AppText } from './text';

import { Palette, Radii, Spacing } from '@/theme';

export type ChatBubbleProps = {
  message: string;
  /** 'out' = the user (right side, accent); 'in' = household/assistant (left side, glass). */
  direction?: 'in' | 'out';
  time?: string;
  style?: StyleProp<ViewStyle>;
};

/** Message bubble for the household assistant chat. */
export function ChatBubble({ message, direction = 'in', time, style }: ChatBubbleProps) {
  const isOut = direction === 'out';
  return (
    <View style={[styles.row, isOut ? styles.rowOut : styles.rowIn, style]}>
      <View style={[styles.bubble, isOut ? styles.bubbleOut : styles.bubbleIn]}>
        <AppText variant="body" color={isOut ? 'white' : 'ink'}>
          {message}
        </AppText>
      </View>
      {time ? (
        <AppText variant="caption" color="inkMuted">
          {time}
        </AppText>
      ) : null}
    </View>
  );
}

export type ChatInputProps = {
  value: string;
  onChangeText?: (text: string) => void;
  onSend?: () => void;
  placeholder?: string;
  style?: StyleProp<ViewStyle>;
};

/** Chat composer: text field + send button. */
export function ChatInput({ value, onChangeText, onSend, placeholder = 'Escribe un mensaje…', style }: ChatInputProps) {
  const canSend = value.trim().length > 0;
  return (
    <View style={[styles.inputRow, style]}>
      <TextField
        value={value}
        onChangeText={onChangeText}
        placeholder={placeholder}
        containerStyle={styles.inputField}
        returnKeyType="send"
        onSubmitEditing={canSend ? onSend : undefined}
      />
      {canSend ? (
        <IconButton label="Enviar" icon={Icons.send} onPress={onSend} variant="primary" />
      ) : (
        <Button label="Enviar" icon={Icons.send} variant="secondary" disabled onPress={onSend} />
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  row: {
    gap: Spacing.half,
    maxWidth: '82%',
  },
  rowIn: {
    alignSelf: 'flex-start',
    alignItems: 'flex-start',
  },
  rowOut: {
    alignSelf: 'flex-end',
    alignItems: 'flex-end',
  },
  bubble: {
    paddingVertical: Spacing.two,
    paddingHorizontal: Spacing.three,
    borderRadius: Radii.lg,
  },
  bubbleIn: {
    backgroundColor: 'rgba(255,255,255,0.78)',
    borderTopLeftRadius: Radii.sm,
    borderWidth: StyleSheet.hairlineWidth,
    borderColor: 'rgba(255,255,255,0.9)',
  },
  bubbleOut: {
    backgroundColor: Palette.accent,
    borderTopRightRadius: Radii.sm,
  },
  inputRow: {
    flexDirection: 'row',
    alignItems: 'flex-end',
    gap: Spacing.two,
  },
  inputField: {
    flex: 1,
  },
});

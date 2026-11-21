import { useEvent } from 'expo';
import { useMemo } from 'react';
import { VideoView, useVideoPlayer } from 'expo-video';
import { Copy, Note } from './ui';

export type PrivateVideoSource = { uri: string; headers: Record<string, string> };

export function CameraPlayer({ source }: { source: PrivateVideoSource | null }) {
  if (!source) return <Note>Esperando la transmisión de la casa.</Note>;
  if (!source.uri.startsWith('https://')) return <Note error>El servicio debe proporcionar una transmisión privada segura.</Note>;
  return <Stream source={source} />;
}

function Stream({ source }: { source: PrivateVideoSource }) {
  const value = useMemo(() => ({ uri: source.uri, headers: source.headers, contentType: 'hls' as const, useCaching: false }), [source.uri, source.headers]);
  const player = useVideoPlayer(value, player => { player.play(); });
  const { status } = useEvent(player, 'statusChange', { status: player.status });
  const { isPlaying } = useEvent(player, 'playingChange', { isPlaying: player.playing });
  return <><VideoView accessibilityLabel="Video privado de la casa" player={player} style={{ width: '100%', height: 220, borderRadius: 16 }} contentFit="contain" nativeControls />{status === 'error' ? <Note error>No se pudo reproducir el video. Puedes volver a consultar la sesión o terminarla.</Note> : <Copy muted>{isPlaying && status === 'readyToPlay' ? 'Reproduciendo transmisión' : status === 'loading' ? 'Conectando video…' : 'Video en espera'}</Copy>}</>;
}

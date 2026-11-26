const RECORD_SEPARATOR = '\u001e';

export type RealtimeStatus = 'connecting' | 'connected' | 'reconnecting' | 'disconnected';

export interface RealtimeHandlers {
  getAccessToken: () => Promise<string>;
  homeUrl: string;
  onStateChanged: () => void;
  onPermissionRevoked: () => void;
  onReconnected: () => void;
  onStatusChange?: (status: RealtimeStatus) => void;
}

type NegotiatePayload = {
  url?: string;
  accessToken?: string;
  connectionToken?: string;
  connectionId?: string;
};

function toWebSocketUrl(baseHttpUrl: string): string {
  if (baseHttpUrl.startsWith('https://')) return `wss://${baseHttpUrl.slice('https://'.length)}`;
  if (baseHttpUrl.startsWith('http://')) return `ws://${baseHttpUrl.slice('http://'.length)}`;
  return baseHttpUrl;
}

function appendQuery(url: string, key: string, value: string): string {
  const separator = url.includes('?') ? '&' : '?';
  return `${url}${separator}${encodeURIComponent(key)}=${encodeURIComponent(value)}`;
}

export function connectHouseRealtime(houseId: string, handlers: RealtimeHandlers): { stop: () => void; reconnect: () => void } {
  let stopped = false;
  let socket: WebSocket | null = null;
  let retryTimer: ReturnType<typeof setTimeout> | null = null;
  let attempt = 0;
  let hasConnectedOnce = false;
  let handshakeComplete = false;

  const setStatus = (status: RealtimeStatus) => {
    if (!stopped) handlers.onStatusChange?.(status);
  };

  const clearTimer = () => {
    if (retryTimer !== null) {
      clearTimeout(retryTimer);
      retryTimer = null;
    }
  };

  const closeSocket = () => {
    if (socket) {
      const active = socket;
      socket = null;
      try { active.close(); } catch { /* Ignore already-closed sockets. */ }
    }
  };

  const scheduleReconnect = () => {
    if (stopped || retryTimer !== null) return;
    setStatus('reconnecting');
    const delayMs = Math.min(1000 * 2 ** Math.min(attempt, 4), 15000);
    attempt += 1;
    retryTimer = setTimeout(() => {
      retryTimer = null;
      void connect();
    }, delayMs);
  };

  const connect = async () => {
    if (stopped || !handlers.homeUrl || !houseId) return;
    clearTimer();
    closeSocket();
    handshakeComplete = false;
    setStatus(hasConnectedOnce ? 'reconnecting' : 'connecting');

    try {
      const token = await handlers.getAccessToken();
      if (stopped) return;

      const base = handlers.homeUrl.replace(/\/$/, '');
      const negotiateUrl = `${base}/hubs/home/negotiate?negotiateVersion=1&houseId=${encodeURIComponent(houseId)}`;
      const response = await fetch(negotiateUrl, {
        method: 'POST',
        headers: {
          Accept: 'application/json',
          Authorization: `Bearer ${token}`,
        },
      });

      if (stopped) return;
      if (response.status === 401 || response.status === 403) {
        setStatus('disconnected');
        handlers.onPermissionRevoked();
        return;
      }
      if (!response.ok) {
        scheduleReconnect();
        return;
      }

      const data = await response.json() as NegotiatePayload;
      if (stopped) return;

      let wsUrl: string;
      if (typeof data.url === 'string' && data.url) {
        wsUrl = toWebSocketUrl(data.url);
        if (typeof data.accessToken === 'string' && data.accessToken && !wsUrl.includes('access_token=')) {
          wsUrl = appendQuery(wsUrl, 'access_token', data.accessToken);
        }
      } else {
        const connectionToken = data.connectionToken ?? data.connectionId;
        if (!connectionToken) {
          scheduleReconnect();
          return;
        }
        wsUrl = `${toWebSocketUrl(base)}/hubs/home?id=${encodeURIComponent(connectionToken)}&houseId=${encodeURIComponent(houseId)}&access_token=${encodeURIComponent(token)}`;
      }

      const ws = new WebSocket(wsUrl);
      socket = ws;

      ws.onopen = () => {
        if (stopped || socket !== ws) return;
        ws.send(`${JSON.stringify({ protocol: 'json', version: 1 })}${RECORD_SEPARATOR}`);
      };

      ws.onmessage = event => {
        if (stopped || socket !== ws || typeof event.data !== 'string') return;
        const frames = event.data.split(RECORD_SEPARATOR).filter(Boolean);
        for (const frame of frames) {
          let message: { type?: number; target?: string; error?: string; allowReconnect?: boolean };
          try {
            message = JSON.parse(frame) as typeof message;
          } catch {
            continue;
          }

          if (!handshakeComplete) {
            if (message.error) {
              ws.close();
              return;
            }
            handshakeComplete = true;
            const reconnecting = hasConnectedOnce;
            hasConnectedOnce = true;
            attempt = 0;
            setStatus('connected');
            if (reconnecting) {
              handlers.onReconnected();
            }
            continue;
          }

          if (message.type === 6) {
            ws.send(`${JSON.stringify({ type: 6 })}${RECORD_SEPARATOR}`);
            continue;
          }

          if (message.type === 1) {
            if (message.target === 'stateChanged') {
              handlers.onStateChanged();
            } else if (message.target === 'permissionRevoked') {
              stopped = true;
              clearTimer();
              closeSocket();
              handlers.onPermissionRevoked();
              return;
            }
            continue;
          }

          if (message.type === 7) {
            if (message.allowReconnect === false) {
              setStatus('disconnected');
              handlers.onPermissionRevoked();
              return;
            }
            ws.close();
            return;
          }
        }
      };

      ws.onerror = () => {
        if (socket === ws && !stopped) {
          try { ws.close(); } catch { /* Ignore close failures. */ }
        }
      };

      ws.onclose = () => {
        if (socket === ws) socket = null;
        if (!stopped) scheduleReconnect();
      };
    } catch {
      if (!stopped) scheduleReconnect();
    }
  };

  void connect();

  return {
    stop: () => {
      stopped = true;
      clearTimer();
      closeSocket();
      setStatus('disconnected');
    },
    reconnect: () => {
      if (stopped) return;
      clearTimer();
      if (socket && handshakeComplete) {
        try { socket.close(); } catch { /* Re-open cleanly on resume. */ }
      } else {
        void connect();
      }
    },
  };
}

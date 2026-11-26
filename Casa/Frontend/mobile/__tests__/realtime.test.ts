import { connectHouseRealtime } from '../src/core/realtime';

class FakeWebSocket {
  static instances: FakeWebSocket[] = [];
  url: string;
  sent: string[] = [];
  onopen: (() => void) | null = null;
  onmessage: ((event: { data: string }) => void) | null = null;
  onerror: (() => void) | null = null;
  onclose: (() => void) | null = null;
  closed = false;

  constructor(url: string) {
    this.url = url;
    FakeWebSocket.instances.push(this);
  }

  send(data: string) {
    this.sent.push(data);
  }

  close() {
    if (this.closed) return;
    this.closed = true;
    this.onclose?.();
  }
}

const flush = () => new Promise(resolve => setTimeout(resolve, 0));

describe('connectHouseRealtime (SignalR JSON v1)', () => {
  const originalWs = global.WebSocket;
  const originalFetch = global.fetch;

  beforeEach(() => {
    FakeWebSocket.instances = [];
    (global as unknown as { WebSocket: typeof WebSocket }).WebSocket = FakeWebSocket as unknown as typeof WebSocket;
  });

  afterEach(() => {
    (global as unknown as { WebSocket: typeof WebSocket }).WebSocket = originalWs;
    global.fetch = originalFetch;
    jest.useRealTimers();
  });

  it('negotiates with bearer token, completes SignalR handshake, handles stateChanged and reconnects with REST callback', async () => {
    const fetchMock = jest.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({ url: 'https://signalr.example/client/?hub=househub', accessToken: 'sr-token' }),
    });
    global.fetch = fetchMock;

    const onStateChanged = jest.fn();
    const onPermissionRevoked = jest.fn();
    const onReconnected = jest.fn();
    const statuses: string[] = [];

    const client = connectHouseRealtime('house-1', {
      homeUrl: 'https://home.example',
      getAccessToken: async () => 'jwt-1',
      onStateChanged,
      onPermissionRevoked,
      onReconnected,
      onStatusChange: status => statuses.push(status),
    });

    await flush();
    expect(fetchMock).toHaveBeenCalledWith(
      'https://home.example/hubs/home/negotiate?negotiateVersion=1&houseId=house-1',
      expect.objectContaining({
        method: 'POST',
        headers: expect.objectContaining({ Authorization: 'Bearer jwt-1' }),
      }),
    );

    const ws1 = FakeWebSocket.instances[0];
    expect(ws1.url).toBe('wss://signalr.example/client/?hub=househub&access_token=sr-token');
    ws1.onopen?.();
    expect(ws1.sent[0]).toBe('{"protocol":"json","version":1}\u001e');

    ws1.onmessage?.({ data: '{}\u001e{"type":6}\u001e{"type":1,"target":"stateChanged","arguments":[{"houseId":"house-1"}]}\u001e' });
    expect(ws1.sent[1]).toBe('{"type":6}\u001e');
    expect(onStateChanged).toHaveBeenCalledTimes(1);
    expect(onReconnected).not.toHaveBeenCalled();
    expect(statuses).toContain('connected');

    client.reconnect();
    await new Promise(resolve => setTimeout(resolve, 1100));
    await flush();

    const ws2 = FakeWebSocket.instances[1];
    expect(ws2).toBeDefined();
    ws2.onopen?.();
    ws2.onmessage?.({ data: '{}\u001e' });
    expect(onReconnected).toHaveBeenCalledTimes(1);

    ws2.onmessage?.({ data: '{"type":1,"target":"permissionRevoked"}\u001e' });
    expect(onPermissionRevoked).toHaveBeenCalledTimes(1);
    expect(ws2.closed).toBe(true);

    client.stop();
  });

  it('triggers permission revocation without reconnect loop when negotiate returns 403', async () => {
    const fetchMock = jest.fn().mockResolvedValue({ ok: false, status: 403 });
    global.fetch = fetchMock;

    const onPermissionRevoked = jest.fn();
    const client = connectHouseRealtime('other-house', {
      homeUrl: 'https://home.example',
      getAccessToken: async () => 'jwt-1',
      onStateChanged: jest.fn(),
      onPermissionRevoked,
      onReconnected: jest.fn(),
    });

    await flush();
    expect(onPermissionRevoked).toHaveBeenCalledTimes(1);
    expect(FakeWebSocket.instances).toHaveLength(0);
    client.stop();
  });
});

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TurnKit.Internal.SimpleJSON;
using TurnKit.NativeWebSocket;
using UnityEngine;

namespace TurnKit
{
    internal sealed class RelayTransport
    {
        private readonly Action _onOpen;
        private readonly Action<ulong> _onClose;
        private readonly Action<string> _onError;
        private readonly Action<string> _onMessage;

        private readonly float _pingInterval = 10f;
        private WebSocket _ws;
        private bool _reconnectMessageSent;
        private bool _disconnectNotified;
        private bool _suppressDisconnectCallbacks;
        private float _pingTimer;
        private string _lastWebSocketUrl;
        private string _lastRelayToken;
        private int _lastAckMoveNumber;

        public RelayTransport(Action onOpen, Action<ulong> onClose, Action<string> onError, Action<string> onMessage)
        {
            _onOpen = onOpen;
            _onClose = onClose;
            _onError = onError;
            _onMessage = onMessage;
        }

        public bool IsConnected { get; private set; }
        public string LastWebSocketUrl => _lastWebSocketUrl;
        public bool HasReconnectContext => !string.IsNullOrEmpty(_lastWebSocketUrl) && !string.IsNullOrEmpty(_lastRelayToken);

        public async Task Connect(string relayToken, int lastMoveNumber)
        {
            _lastRelayToken = relayToken;
            _lastAckMoveNumber = lastMoveNumber;
            _lastWebSocketUrl = BuildWebSocketUrl(relayToken);

            await ConnectInternal(_lastWebSocketUrl, relayToken, lastMoveNumber, sendReconnectMessage: false);
        }

        public async Task<bool> Resume(string relayToken, int lastMoveNumber)
        {
            if (string.IsNullOrWhiteSpace(relayToken))
            {
                return false;
            }

            _lastRelayToken = relayToken;
            _lastAckMoveNumber = lastMoveNumber;
            _lastWebSocketUrl = BuildWebSocketUrl(relayToken);

            try
            {
                await ConnectInternal(_lastWebSocketUrl, relayToken, lastMoveNumber, sendReconnectMessage: true);
                return true;
            }
            catch (Exception ex)
            {
                _onError?.Invoke($"Resume failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> Reconnect(int lastMoveNumber, bool force = false)
        {
            if (!HasReconnectContext)
            {
                return false;
            }

            _lastAckMoveNumber = lastMoveNumber;

            try
            {
                Debug.unityLogger.Log($"Reconnecting to {_lastWebSocketUrl} with move number {lastMoveNumber}");
                if (!force && IsConnected)
                {
                    return true;
                }

                await ConnectInternal(_lastWebSocketUrl, _lastRelayToken, lastMoveNumber, sendReconnectMessage: true);
                return true;
            }
            catch (Exception ex)
            {
                _onError?.Invoke($"Reconnect failed: {ex.Message}");
                return false;
            }
        }

        public void Tick(float deltaTime)
        {
            if (_ws == null)
            {
                return;
            }

#if !UNITY_WEBGL || UNITY_EDITOR
            _ws.DispatchMessageQueue();
#endif
            if (!IsConnected)
            {
                return;
            }

            _pingTimer += deltaTime;
            if (_pingTimer >= _pingInterval)
            {
                _pingTimer = 0f;
                Send("{\"type\":\"PING\"}");
            }
        }

        public void Send(string json)
        {
            if (_ws != null && IsConnected)
            {
                _ws.SendText(json);
            }
        }

        public async Task Close()
        {
            if (_ws != null)
            {
                await _ws.Close();
                _ws = null;
            }
        }

        public void MarkDisconnected()
        {
            IsConnected = false;
            _disconnectNotified = true;
        }

        private void SendReconnectMessage(int lastMoveNumber)
        {
            if (_reconnectMessageSent)
            {
                return;
            }

            var msg = new JSONObject();
            msg["type"] = "RECONNECT";
            msg["lastMoveNumber"] = lastMoveNumber;
            Send(msg.ToString());
            _reconnectMessageSent = true;

            if (TurnKitConfig.Instance.enableLogging)
            {
                Debug.Log($"TurnKit - Sent RECONNECT (lastMove: {lastMoveNumber})");
            }
        }

        private async Task ConnectInternal(string wsUrl, string relayToken, int lastMoveNumber, bool sendReconnectMessage)
        {
            await DisposeCurrentSocket(suppressDisconnectCallbacks: true);
            IsConnected = false;

#if UNITY_WEBGL && !UNITY_EDITOR
            _ws = new WebSocket(wsUrl);
#else
            _ws = new WebSocket(wsUrl, new Dictionary<string, string>
            {
                { "Authorization", $"Bearer {relayToken}" }
            });
#endif

            _ws.OnOpen += () =>
            {
                IsConnected = true;
                _disconnectNotified = false;
                _reconnectMessageSent = false;
                _pingTimer = 0f;
                _onOpen?.Invoke();
                if (sendReconnectMessage)
                {
                    SendReconnectMessage(lastMoveNumber);
                }
            };

            _ws.OnMessage += bytes => _onMessage?.Invoke(Encoding.UTF8.GetString(bytes));
            _ws.OnClose += code => HandleSocketDisconnected((ulong)code);
            _ws.OnError += err =>
            {
                _onError?.Invoke(err);
                HandleSocketDisconnected(0);
            };

            await _ws.Connect();
        }

        private async Task DisposeCurrentSocket(bool suppressDisconnectCallbacks)
        {
            if (_ws == null)
            {
                return;
            }

            _suppressDisconnectCallbacks = suppressDisconnectCallbacks;
            if (suppressDisconnectCallbacks)
            {
                _disconnectNotified = true;
            }

            try
            {
                await _ws.Close();
            }
            catch
            {
                // Ignore close errors while replacing socket.
            }
            finally
            {
                _suppressDisconnectCallbacks = false;
                _ws = null;
            }
        }

        private void HandleSocketDisconnected(ulong code)
        {
            IsConnected = false;

            if (_suppressDisconnectCallbacks || _disconnectNotified)
            {
                return;
            }

            _disconnectNotified = true;
            _onClose?.Invoke(code);
        }

        private static string BuildWebSocketUrl(string relayToken)
        {
            var baseUrl = TurnKitConfig.Instance.serverUrl.Replace("http", "ws") + "/v1/client/relay/ws";
#if UNITY_WEBGL && !UNITY_EDITOR
            return $"{baseUrl}?token={Uri.EscapeDataString(relayToken)}";
#else
            return baseUrl;
#endif
        }
    }
}

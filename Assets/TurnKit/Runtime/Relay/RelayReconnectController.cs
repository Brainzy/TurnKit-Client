using System;
using System.Threading.Tasks;
using UnityEngine;

namespace TurnKit
{
    internal sealed class RelayReconnectController
    {
        private readonly RelayTransport _transport;
        private readonly Func<int> _lastAcknowledgedMoveNumber;
        private readonly Action _onReconnectFailedExhausted;
        private readonly Func<bool> _isLoggingEnabled;
        private readonly bool _autoReconnectEnabled;
        private readonly int _autoReconnectMaxAttempts;
        private readonly float _autoReconnectInitialDelaySeconds;
        private readonly float _autoReconnectMaxDelaySeconds;

        private bool _allowReconnect;
        private bool _sessionTerminated;
        private bool _reconnectScheduled;
        private bool _reconnectInProgress;
        private float _reconnectTimer;
        private int _reconnectAttempts;

        public RelayReconnectController(
            RelayTransport transport,
            Func<int> lastAcknowledgedMoveNumber,
            Action onReconnectFailedExhausted,
            Func<bool> isLoggingEnabled,
            bool autoReconnectEnabled,
            int autoReconnectMaxAttempts,
            float autoReconnectInitialDelaySeconds,
            float autoReconnectMaxDelaySeconds)
        {
            _transport = transport;
            _lastAcknowledgedMoveNumber = lastAcknowledgedMoveNumber;
            _onReconnectFailedExhausted = onReconnectFailedExhausted;
            _isLoggingEnabled = isLoggingEnabled;
            _autoReconnectEnabled = autoReconnectEnabled;
            _autoReconnectMaxAttempts = autoReconnectMaxAttempts;
            _autoReconnectInitialDelaySeconds = autoReconnectInitialDelaySeconds;
            _autoReconnectMaxDelaySeconds = autoReconnectMaxDelaySeconds;
        }

        public void MarkSessionActive()
        {
            _sessionTerminated = false;
            _allowReconnect = true;
            ResetReconnectBackoff();
        }

        public void DisableReconnect()
        {
            _allowReconnect = false;
            _sessionTerminated = true;
            ResetReconnectBackoff();
        }

        public void Tick(float deltaTime)
        {
            if (!_reconnectScheduled || _reconnectInProgress || _transport == null || _transport.IsConnected)
            {
                return;
            }

            _reconnectTimer -= deltaTime;
            if (_reconnectTimer > 0f)
            {
                return;
            }

            _ = AttemptAutoReconnectAsync();
        }

        public void ScheduleAutoReconnect()
        {
            if (!_autoReconnectEnabled || !_allowReconnect || _sessionTerminated || _transport == null || _transport.IsConnected)
            {
                return;
            }

            if (_reconnectAttempts >= _autoReconnectMaxAttempts)
            {
                return;
            }

            _reconnectScheduled = true;
            _reconnectTimer = CalculateReconnectDelay(_reconnectAttempts);
        }

        public async Task<bool> Reconnect(bool manual)
        {
            if (_sessionTerminated || _transport == null)
            {
                return false;
            }

            if (_transport.IsConnected && !manual)
            {
                return true;
            }

            if (manual)
            {
                _reconnectScheduled = false;
            }

            _reconnectInProgress = true;
            try
            {
                return await _transport.Reconnect(_lastAcknowledgedMoveNumber(), force: manual);
            }
            finally
            {
                _reconnectInProgress = false;
            }
        }

        public async Task<bool> Resume(string relayToken, int lastMoveNumber)
        {
            if (_sessionTerminated || _transport == null)
            {
                return false;
            }

            _reconnectScheduled = false;
            _reconnectInProgress = true;
            try
            {
                return await _transport.Resume(relayToken, lastMoveNumber);
            }
            finally
            {
                _reconnectInProgress = false;
            }
        }

        public async Task ReconnectFromStaleSocketError()
        {
            if (_reconnectInProgress || _sessionTerminated || _transport == null)
            {
                return;
            }

            _reconnectScheduled = false;
            await Reconnect(manual: true);
        }

        private async Task AttemptAutoReconnectAsync()
        {
            if (_reconnectInProgress || _transport.IsConnected || _sessionTerminated)
            {
                return;
            }

            if (_reconnectAttempts >= _autoReconnectMaxAttempts)
            {
                _reconnectScheduled = false;
                _onReconnectFailedExhausted?.Invoke();
                return;
            }

            _reconnectScheduled = false;
            _reconnectAttempts++;

            if (_isLoggingEnabled())
            {
                Debug.Log($"TurnKit - Reconnect attempt {_reconnectAttempts}/{_autoReconnectMaxAttempts}");
            }

            var success = await Reconnect(manual: false);
            if (success || _sessionTerminated || _transport.IsConnected)
            {
                return;
            }

            if (_reconnectAttempts >= _autoReconnectMaxAttempts)
            {
                if (_isLoggingEnabled())
                {
                    Debug.LogWarning("TurnKit - Reconnect retries exhausted.");
                }

                _onReconnectFailedExhausted?.Invoke();
                return;
            }

            _reconnectScheduled = true;
            _reconnectTimer = CalculateReconnectDelay(_reconnectAttempts);
        }

        private void ResetReconnectBackoff()
        {
            _reconnectScheduled = false;
            _reconnectInProgress = false;
            _reconnectAttempts = 0;
            _reconnectTimer = 0f;
        }

        private float CalculateReconnectDelay(int failedAttempts)
        {
            var delay = _autoReconnectInitialDelaySeconds * Mathf.Pow(2f, failedAttempts);
            return Mathf.Min(delay, _autoReconnectMaxDelaySeconds);
        }
    }
}

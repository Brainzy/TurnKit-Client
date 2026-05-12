using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace TurnKit
{
    public partial class Relay
    {
        private void HandleConnected()
        {
            _state.MarkConnected();
            _disconnectEventRaised = false;
            _reconnectController.MarkSessionActive();
            OnConnected?.Invoke();

            if (TurnKitConfig.Instance != null && TurnKitConfig.Instance.enableLogging)
            {
                Debug.Log("TurnKit - WebSocket Connected");
            }
        }

        private void HandleDisconnected(ulong code)
        {
            HandleDisconnectInternal($"closed ({code})");
        }

        private void HandleTransportError(string err)
        {
            HandleDisconnectInternal("error");
            Debug.LogError($"TurnKit - WebSocket error: {err}");
        }

        private void HandleIncomingMessage(string raw)
        {
            var outcome = _messageRouter.Process(raw);
            if (outcome == null)
            {
                return;
            }

            _commandExecutor.ResetDelegatedIntendedMoveTracking();

            switch (outcome.EventType)
            {
                case RelayEventType.MatchStarted:
                    _sessionId = outcome.MatchStarted.sessionId;
                    var localPlayer = outcome.MatchStarted.players?.FirstOrDefault(player => player.playerId == _myPlayerId);
                    if (localPlayer != null)
                    {
                        _mySlot = localPlayer.slot;
                    }

                    if (outcome.MatchStarted.yourTurn)
                    {
                        ApplyTurnTimerFromServer(outcome.MatchStarted.serverNowUtcMs, outcome.MatchStarted.timerEndUtcMs);
                    }
                    else
                    {
                        StopTurnTimer();
                    }
                    OnMatchStarted?.Invoke(outcome.MatchStarted, _state.AllLists);
                    if (TurnKitConfig.Instance.enableLogging)
                    {
                        Debug.Log(outcome.MatchStarted.ToString(_state.AllLists.Count));
                    }

                    break;
                case RelayEventType.MoveMade:
                    OnMoveMade?.Invoke(outcome.MoveMade, _state.AllLists);
                    SaveLastMatchReconnectFromCurrentState();
                    if (TurnKitConfig.Instance.enableLogging)
                    {
                        Debug.Log($"TurnKit - MoveMade #{outcome.MoveMade.moveNumber} from {outcome.MoveMade.actingPlayerId}");
                    }

                    break;
                case RelayEventType.SyncComplete:
                    ApplyTurnTimerFromServer(outcome.SyncComplete.serverNowUtcMs, outcome.SyncComplete.timerEndUtcMs);
                    OnSyncComplete?.Invoke();
                    SaveLastMatchReconnectFromCurrentState();
                    if (TurnKitConfig.Instance.enableLogging)
                    {
                        Debug.Log($"TurnKit - Sync complete (move: {outcome.SyncComplete.moveNumber})");
                    }

                    break;
                case RelayEventType.TurnStarted:
                    ApplyTurnTimerFromServer(outcome.TurnStarted.serverNowUtcMs, outcome.TurnStarted.timerEndUtcMs);
                    OnTurnStarted?.Invoke(outcome.TurnStarted);
                    if (TurnKitConfig.Instance.enableLogging)
                    {
                        Debug.Log($"TurnKit - TurnStarted: {outcome.TurnStarted.activePlayerId}");
                    }

                    break;
                case RelayEventType.MoveRequestedForPlayer:
                    ApplyTurnTimerFromServer(outcome.MoveRequestedForPlayer.serverNowUtcMs, outcome.MoveRequestedForPlayer.timerEndUtcMs);
                    OnMoveRequestedForPlayer?.Invoke(outcome.MoveRequestedForPlayer);
                    if (TurnKitConfig.Instance.enableLogging)
                    {
                        Debug.Log($"TurnKit - MoveRequestedForPlayer slot: {outcome.MoveRequestedForPlayer.playerSlot}");
                    }

                    break;
                case RelayEventType.VoteFailed:
                    OnVoteFailed?.Invoke(outcome.VoteFailed);
                    if (TurnKitConfig.Instance.enableLogging)
                    {
                        Debug.Log($"TurnKit - OnVoteFailed: {outcome.VoteFailed.failAction} - {outcome.VoteFailed.moveNumber}");
                    }

                    break;
                case RelayEventType.Error:
                    OnError?.Invoke(outcome.Error);
                    if (IsReconnectTerminalError(outcome.Error))
                    {
                        DisableReconnect();
                        ClearSavedReconnect();
                        OnReconnectFailed?.Invoke(outcome.Error);
                    }
                    else if (ShouldReconnectOnError(outcome.Error))
                    {
                        _ = ReconnectFromStaleSocketError();
                    }
                    if (TurnKitConfig.Instance.enableLogging)
                    {
                        Debug.Log($"TurnKit - OnError: {outcome.Error.code} - {outcome.Error.message}");
                    }

                    break;
                case RelayEventType.GameEnded:
                    OnGameEnded?.Invoke(outcome.GameEnded);
                    StopTurnTimer();
                    DisableReconnect();
                    ClearSavedReconnect();
                    _transport.MarkDisconnected();
                    _state.MarkDisconnected();
                    if (TurnKitConfig.Instance.enableLogging)
                    {
                        Debug.Log($"TurnKit - OnGameEnded: {outcome.GameEnded.reason}");
                    }
                    break;
            }
        }

        private void DispatchListChanged(RelayList list, ListChangeType changeType)
        {
            OnListChanged?.Invoke(list, changeType);
        }

        private void HandleDisconnectInternal(string reason)
        {
            _state.MarkDisconnected();
            StopTurnTimer();
            if (!_disconnectEventRaised)
            {
                _disconnectEventRaised = true;
                OnDisconnected?.Invoke();
            }

            if (TurnKitConfig.Instance != null && TurnKitConfig.Instance.enableLogging)
            {
                Debug.Log($"TurnKit - WebSocket disconnected ({reason})");
            }

            _reconnectController.ScheduleAutoReconnect();
        }

        private void ApplyTurnTimerFromServer(long serverNowUtcMs, long? timerEndUtcMs)
        {
            _turnTimer.ApplyFromServer(serverNowUtcMs, timerEndUtcMs);
        }

        private void StopTurnTimer()
        {
            _turnTimer.Stop();
        }

        private void ResetTurnTimer()
        {
            _turnTimer.Reset();
        }

        private void DisableReconnect()
        {
            _reconnectController.DisableReconnect();
            StopTurnTimer();
        }

        private static bool ShouldReconnectOnError(ErrorMessage error)
        {
            return error != null && string.Equals(error.code, "STALE_SOCKET", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsReconnectTerminalError(ErrorMessage error)
        {
            if (error == null)
            {
                return false;
            }

            return string.Equals(error.code, "RECONNECT_EXPIRED", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(error.code, "RECONNECT_MOVE_GAP_TOO_LARGE", StringComparison.OrdinalIgnoreCase);
        }

        private async Task ReconnectFromStaleSocketError()
        {
            await _reconnectController.ReconnectFromStaleSocketError();
        }

        private static void SaveLastMatchReconnectFromCurrentState()
        {
            if (_instance == null)
            {
                return;
            }

            _instance._reconnectStore.Save(
                _instance._myPlayerId,
                _instance._relaySlug,
                _instance._relayToken,
                _instance._state.LastAcknowledgedMoveNumber);
        }
    }
}

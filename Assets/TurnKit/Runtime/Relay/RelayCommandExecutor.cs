using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TurnKit
{
    internal sealed class RelayCommandExecutor
    {
        private readonly RelayValidator _validator;
        private readonly RelaySessionState _state;
        private readonly RelayCommandQueue _commandQueue;
        private readonly RelayTransport _transport;
        private readonly Func<bool> _isAfk;
        private readonly Action<Action<MoveMadeMessage, IReadOnlyList<RelayList>>> _subscribeMoveMade;
        private readonly Action<Action<MoveMadeMessage, IReadOnlyList<RelayList>>> _unsubscribeMoveMade;
        private readonly Action<Action<ErrorMessage>> _subscribeError;
        private readonly Action<Action<ErrorMessage>> _unsubscribeError;
        private readonly Action<Action> _subscribeDisconnected;
        private readonly Action<Action> _unsubscribeDisconnected;
        private int _localDelegatedPendingMoves;

        public RelayCommandExecutor(
            RelayValidator validator,
            RelaySessionState state,
            RelayCommandQueue commandQueue,
            RelayTransport transport,
            Func<bool> isAfk,
            Action<Action<MoveMadeMessage, IReadOnlyList<RelayList>>> subscribeMoveMade,
            Action<Action<MoveMadeMessage, IReadOnlyList<RelayList>>> unsubscribeMoveMade,
            Action<Action<ErrorMessage>> subscribeError,
            Action<Action<ErrorMessage>> unsubscribeError,
            Action<Action> subscribeDisconnected,
            Action<Action> unsubscribeDisconnected)
        {
            _validator = validator;
            _state = state;
            _commandQueue = commandQueue;
            _transport = transport;
            _isAfk = isAfk;
            _subscribeMoveMade = subscribeMoveMade;
            _unsubscribeMoveMade = unsubscribeMoveMade;
            _subscribeError = subscribeError;
            _unsubscribeError = unsubscribeError;
            _subscribeDisconnected = subscribeDisconnected;
            _unsubscribeDisconnected = unsubscribeDisconnected;
        }

        public async Task ExecuteQueuedActionsAndWait(bool shouldEndTurn, bool delegated, string delegateForPlayerId)
        {
            int? expectedMoveNumber = ExecuteQueuedActions(shouldEndTurn, delegated, delegateForPlayerId);
            if (!expectedMoveNumber.HasValue)
            {
                return;
            }

            try
            {
                await WaitForMoveApplied(expectedMoveNumber.Value);
            }
            catch (TaskCanceledException)
            {
                Debug.LogWarning($"[TurnKit] Awaited move #{expectedMoveNumber.Value} was not confirmed before disconnect/error/timeout.");
            }
        }

        public void ResetDelegatedIntendedMoveTracking()
        {
            _localDelegatedPendingMoves = 0;
        }

        private int? ExecuteQueuedActions(bool shouldEndTurn, bool delegated, string delegateForPlayerId)
        {
            _validator.ValidateReadyToSend(_transport.IsConnected, _state.IsInSyncWindow);

            if (!_state.IsMyTurn && !delegated)
            {
                Debug.LogError("[TurnKit] Not your turn. Actions not sent.");
                _commandQueue.Clear();
                return null;
            }

            int expectedMoveNumber = _state.LastAcknowledgedMoveNumber + 1;
            int? intendedMoveNumber = delegated
                ? GetAndAdvanceDelegatedIntendedMoveNumber()
                : (int?)null;

            _transport.Send(_commandQueue.BuildMovePayload(
                shouldEndTurn,
                delegated,
                delegateForPlayerId,
                _isAfk(),
                intendedMoveNumber));
            _commandQueue.Clear();
            return expectedMoveNumber;
        }

        private Task WaitForMoveApplied(int expectedMoveNumber)
        {
            if (_state.LastAcknowledgedMoveNumber >= expectedMoveNumber)
            {
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            CancellationTokenSource timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

            Action<MoveMadeMessage, IReadOnlyList<RelayList>> onMoveMade = null;
            Action<ErrorMessage> onError = null;
            Action onDisconnected = null;

            void Cleanup()
            {
                _unsubscribeMoveMade(onMoveMade);
                _unsubscribeError(onError);
                _unsubscribeDisconnected(onDisconnected);
                timeoutCts.Dispose();
            }

            onMoveMade = (msg, _) =>
            {
                if (msg == null || msg.moveNumber < expectedMoveNumber)
                {
                    return;
                }

                Cleanup();
                tcs.TrySetResult(true);
            };

            onError = _ =>
            {
                Cleanup();
                tcs.TrySetCanceled();
            };

            onDisconnected = () =>
            {
                Cleanup();
                tcs.TrySetCanceled();
            };

            timeoutCts.Token.Register(() =>
            {
                Cleanup();
                tcs.TrySetCanceled();
            });

            _subscribeMoveMade(onMoveMade);
            _subscribeError(onError);
            _subscribeDisconnected(onDisconnected);

            if (_state.LastAcknowledgedMoveNumber >= expectedMoveNumber)
            {
                Cleanup();
                tcs.TrySetResult(true);
            }

            return tcs.Task;
        }

        private int GetAndAdvanceDelegatedIntendedMoveNumber()
        {
            int intended = _state.LastAcknowledgedMoveNumber + _localDelegatedPendingMoves + 1;
            _localDelegatedPendingMoves++;
            return intended;
        }
    }
}

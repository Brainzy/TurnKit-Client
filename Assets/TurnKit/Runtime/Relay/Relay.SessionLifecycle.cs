using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace TurnKit
{
    public partial class Relay
    {
        public static Task<bool> MatchWithAnyone(
            string playerId,
            string slug,
            Dictionary<string, List<RelayItem>> items = null,
            FillPolicy fillPolicy = FillPolicy.REQUIRE_ALL_PLAYERS,
            int? delegatedFillAfterSeconds = null)
        {
            return MatchWithAnyone(TurnKitClientIdentity.NoAuth(playerId), slug, items, fillPolicy, delegatedFillAfterSeconds);
        }

        public static Task<bool> MatchWithAnyone(
            TurnKitPlayerSession session,
            string slug,
            Dictionary<string, List<RelayItem>> items = null,
            FillPolicy fillPolicy = FillPolicy.REQUIRE_ALL_PLAYERS,
            int? delegatedFillAfterSeconds = null)
        {
            return MatchWithAnyone(TurnKitClientIdentity.Authenticated(session), slug, items, fillPolicy, delegatedFillAfterSeconds);
        }

        public static Task<bool> MatchWithAnyone(
            TurnKitYourBackendProof proof,
            string slug,
            Dictionary<string, List<RelayItem>> items = null,
            FillPolicy fillPolicy = FillPolicy.REQUIRE_ALL_PLAYERS,
            int? delegatedFillAfterSeconds = null)
        {
            return MatchWithAnyone(TurnKitClientIdentity.YourBackend(proof), slug, items, fillPolicy, delegatedFillAfterSeconds);
        }

        private static async Task<bool> MatchWithAnyone(
            TurnKitClientIdentity identity,
            string slug,
            Dictionary<string, List<RelayItem>> items = null,
            FillPolicy fillPolicy = FillPolicy.REQUIRE_ALL_PLAYERS,
            int? delegatedFillAfterSeconds = null)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                throw new ArgumentException("Relay slug is required.", nameof(slug));
            }

            if (Registry.Initializers.TryGetValue(slug, out var initAction))
            {
                initAction.Invoke();
            }
            else
            {
                Debug.LogError($"[TurnKit] No config found for slug: {slug}");
                return false;
            }

            var body = RelayQueueRequestBuilder.BuildQueueRequestJson(slug, items, fillPolicy, delegatedFillAfterSeconds);
            using var req = TurnKitClientRequest.CreateJson("/v1/client/relay/queue", "POST", body);
            await TurnKitClientRequest.PrepareIdentity(req, identity);

            try
            {
                var response = await TurnKitClientRequest.SendJson<RelayQueueResponse>(req);
                Instance._relayToken = response.relayToken;
                Instance._relaySlug = slug;
                Instance._sessionId = response.sessionId;
                Instance._mySlot = (TurnKitConfig.PlayerSlot)response.slot;
                Instance._myPlayerId = identity.PlayerId;
                Instance._state.SetLocalPlayer(identity.PlayerId);
                Instance._reconnectController.MarkSessionActive();
                Instance.ResetTurnTimer();
                Instance._commandExecutor.ResetDelegatedIntendedMoveTracking();

                await Instance._transport.Connect(Instance._relayToken, Instance._state.LastAcknowledgedMoveNumber);
                Instance._reconnectStore.Save(identity.PlayerId, slug, Instance._relayToken, Instance._state.LastAcknowledgedMoveNumber);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TurnKit] Queue join failed: {ex.Message}");
                return false;
            }
        }

        public static async Task<bool> Reconnect()
        {
            if (_instance == null)
            {
                return false;
            }

            return await Instance._reconnectController.Reconnect(manual: true);
        }

        public static async Task<bool> Resume(string playerId, string slug, string relayToken, int lastMoveNumber)
        {
            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(relayToken))
            {
                return false;
            }

            if (Registry.Initializers.TryGetValue(slug, out var initAction))
            {
                initAction.Invoke();
            }
            else
            {
                Debug.LogError($"[TurnKit] No config found for slug: {slug}");
                return false;
            }

            Instance._relayToken = relayToken;
            Instance._relaySlug = slug;
            Instance._myPlayerId = playerId;
            Instance._state.SetLocalPlayer(playerId);
            Instance._reconnectController.MarkSessionActive();
            Instance.ResetTurnTimer();
            Instance._commandExecutor.ResetDelegatedIntendedMoveTracking();

            bool resumed = await Instance._reconnectController.Resume(relayToken, lastMoveNumber);
            if (resumed)
            {
                Instance._reconnectStore.Save(playerId, slug, relayToken, Instance._state.LastAcknowledgedMoveNumber);
            }

            return resumed;
        }

        public static async Task<bool> ResumeLastMatch()
        {
            RelayReconnectSnapshot snapshot = Instance._reconnectStore.Load();
            if (snapshot == null)
            {
                return false;
            }

            bool success = await Resume(snapshot.PlayerId, snapshot.Slug, snapshot.RelayToken, snapshot.LastMoveNumber);
            if (!success)
            {
                ClearSavedReconnect();
            }

            return success;
        }

        public static void ClearSavedReconnect()
        {
            Instance._reconnectStore.Clear();
        }

        public static Task LeaveQueue(string playerId, string slug)
        {
            return LeaveQueue(TurnKitClientIdentity.NoAuth(playerId), slug);
        }

        public static Task LeaveQueue(TurnKitPlayerSession session, string slug)
        {
            return LeaveQueue(TurnKitClientIdentity.Authenticated(session), slug);
        }

        public static Task LeaveQueue(TurnKitYourBackendProof proof, string slug)
        {
            return LeaveQueue(TurnKitClientIdentity.YourBackend(proof), slug);
        }

        private static async Task LeaveQueue(TurnKitClientIdentity identity, string slug)
        {
            if (_instance != null)
            {
                _instance.DisableReconnect();
                await _instance._transport.Close();
            }
            ClearSavedReconnect();

            using var req = TurnKitClientRequest.Create($"/v1/client/relay/queue/{slug}", "DELETE");
            await TurnKitClientRequest.PrepareIdentity(req, identity);
            await TurnKitClientRequest.Send(req);
        }
    }
}

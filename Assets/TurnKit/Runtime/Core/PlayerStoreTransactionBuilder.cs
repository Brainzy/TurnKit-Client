using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace TurnKit
{
    public sealed class PlayerStoreTransactionBuilder
    {
        private readonly string _transactionId;
        private readonly TurnKitClientIdentity _identity;

        internal PlayerStoreTransactionBuilder(string transactionId, TurnKitClientIdentity identity)
        {
            _transactionId = transactionId;
            _identity = identity;
        }

        public async Task<PlayerStoreTransactionResult> Execute()
        {
            PlayerStoreTransactionExecutor.ValidateTransactionId(_transactionId);

            var requestBody = JsonUtility.ToJson(new PlayerStoreTransactionRequest
            {
                transactionId = _transactionId
            });

            using var request = TurnKitClientRequest.CreateJson("/v1/client/player-store/tx", "POST", requestBody);
            await TurnKitClientRequest.PrepareIdentity(request, _identity);
            await PlayerStoreTransactionExecutor.SendRequest(request);

            var response = JsonUtility.FromJson<PlayerStoreTransactionResponse>(request.downloadHandler.text);
            if (response == null || string.IsNullOrWhiteSpace(response.transactionId))
            {
                throw new Exception("PlayerStore.Transaction response parse failed.");
            }

            return new PlayerStoreTransactionResult(response.transactionId, response.applied, response.alreadyApplied);
        }
    }

    internal static class PlayerStoreTransactionExecutor
    {
        private static readonly Regex TransactionIdRegex = new("^[a-z0-9._-]{1,64}$", RegexOptions.Compiled);

        internal static void ValidateTransactionId(string transactionId)
        {
            if (string.IsNullOrWhiteSpace(transactionId) || !TransactionIdRegex.IsMatch(transactionId))
            {
                throw new PlayerStoreInvalidTransactionIdException(transactionId);
            }
        }

        internal static async Task SendRequest(UnityWebRequest request)
        {
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                return;
            }

            string responseText = request.downloadHandler?.text ?? string.Empty;
            long status = request.responseCode;
            if (responseText.Contains("TX_CONDITION_FAILED"))
            {
                throw new PlayerStoreTransactionConditionFailedException(responseText);
            }

            if (responseText.Contains("PLAYER_STORE_COOLDOWN_ACTIVE"))
            {
                throw new PlayerStoreCooldownActiveException(responseText);
            }

            if (responseText.Contains("TX_NOT_ALLOWED"))
            {
                throw new PlayerStoreTransactionNotAllowedException(responseText);
            }

            if (responseText.Contains("INVALID_TRANSACTION_ID"))
            {
                throw new PlayerStoreInvalidTransactionIdException(responseText);
            }

            if (responseText.Contains("TX_MISMATCH"))
            {
                throw new PlayerStoreTransactionMismatchException(responseText);
            }

            throw new Exception($"TurnKit [{status}]: {responseText}");
        }
    }

    internal static class PlayerStoreRequestExecutor
    {
        internal static async Task SendWriteRequest(UnityWebRequest request, string storeKey)
        {
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                return;
            }

            string responseText = request.downloadHandler?.text ?? string.Empty;
            if (responseText.Contains("PLAYER_STORE_COOLDOWN_ACTIVE"))
            {
                throw new PlayerStoreCooldownActiveException($"storeKey={storeKey}. {responseText}");
            }

            throw new Exception($"TurnKit [{request.responseCode}]: {responseText}");
        }
    }
}

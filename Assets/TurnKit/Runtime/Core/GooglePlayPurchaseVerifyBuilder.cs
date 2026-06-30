using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace TurnKit
{
    public sealed class GooglePlayPurchaseVerifyBuilder
    {
        private readonly string _packageName;
        private readonly string _productId;
        private readonly string _purchaseToken;
        private readonly StorePurchaseType _purchaseType;
        private readonly TurnKitClientIdentity _identity;

        internal GooglePlayPurchaseVerifyBuilder(
            string packageName,
            string productId,
            string purchaseToken,
            StorePurchaseType purchaseType,
            TurnKitClientIdentity identity)
        {
            _packageName = packageName;
            _productId = productId;
            _purchaseToken = purchaseToken;
            _purchaseType = purchaseType;
            _identity = identity;
        }

        public async Task<GooglePlayPurchaseVerifyResult> Verify()
        {
            if (string.IsNullOrWhiteSpace(_packageName))
            {
                throw new InvalidOperationException("Google Play purchase verification requires packageName.");
            }

            if (string.IsNullOrWhiteSpace(_productId))
            {
                throw new InvalidOperationException("Google Play purchase verification requires productId.");
            }

            if (string.IsNullOrWhiteSpace(_purchaseToken))
            {
                throw new InvalidOperationException("Google Play purchase verification requires purchaseToken.");
            }

            string requestBody = JsonUtility.ToJson(new GooglePlayPurchaseVerifyRequest
            {
                packageName = _packageName,
                productId = _productId,
                purchaseToken = _purchaseToken,
                purchaseType = _purchaseType
            });

            using var request = TurnKitClientRequest.CreateJson("/v1/client/player-store/purchases/google/verify", "POST", requestBody);
            await TurnKitClientRequest.PrepareIdentity(request, _identity);
            Debug.Log($"TurnKit GooglePlayPurchaseVerify: sending verify request. packageName={_packageName}, productId={_productId}, purchaseType={_purchaseType}, tokenLength={_purchaseToken.Length}, url={request.url}");
            await SendVerifyRequest(request, _productId);

            var response = JsonUtility.FromJson<GooglePlayPurchaseVerifyResponse>(request.downloadHandler.text);
            if (response == null)
            {
                throw new Exception("Google Play purchase verify response parse failed.");
            }

            Debug.Log($"TurnKit GooglePlayPurchaseVerify: verify response productId={_productId}, verified={response.verified}, granted={response.granted}, alreadyGranted={response.alreadyGranted}, orderId={response.orderId}, state={response.state}.");
            return new GooglePlayPurchaseVerifyResult(
                response.verified,
                response.granted,
                response.alreadyGranted,
                response.orderId,
                response.state);
        }

        private static async Task SendVerifyRequest(UnityWebRequest request, string productId)
        {
            float startedAt = Time.realtimeSinceStartup;
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                if (Time.realtimeSinceStartup - startedAt > request.timeout + 2f)
                {
                    request.Abort();
                    throw new TimeoutException($"Google Play purchase verify timed out for productId={productId}, url={request.url}");
                }

                await Task.Yield();
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                return;
            }

            string responseText = request.downloadHandler?.text ?? string.Empty;
            throw new Exception($"TurnKit [{request.responseCode}] Google Play verify failed for productId={productId}. {responseText}");
        }
    }
}

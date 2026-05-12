using System;
using System.Collections;
using System.Text;
using TurnKit.Internal.SimpleJSON;
using UnityEngine.Networking;

namespace TurnKit.Editor
{
    public static partial class TurnKitAPI
    {
        private static IEnumerator SendWebhookWrite(
            string url,
            string method,
            string body,
            string jwt,
            Action<TurnKitConfig.WebhookConfig> onSuccess,
            Action<string> onError)
        {
            var request = new UnityWebRequest(url, method);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            return SendRequest(request, jwt, responseText =>
            {
                try
                {
                    onSuccess?.Invoke(ParseWebhook(JSON.Parse(responseText)));
                }
                catch (Exception e)
                {
                    onError?.Invoke($"Webhook Parse Error: {e.Message}");
                }
            }, onError);
        }

        private static IEnumerator SendRequest(UnityWebRequest req, string jwt, Action<string> onSuccess, Action<string> onError)
        {
            using (req)
            {
                if (!string.IsNullOrEmpty(jwt))
                {
                    req.SetRequestHeader("Authorization", $"Bearer {jwt}");
                }

                var op = req.SendWebRequest();
                while (!op.isDone)
                {
                    yield return null;
                }

                if (req.result == UnityWebRequest.Result.Success)
                {
                    onSuccess?.Invoke(req.downloadHandler?.text ?? string.Empty);
                }
                else
                {
                    string details = string.IsNullOrEmpty(req.downloadHandler?.text)
                        ? req.error
                        : $"{req.error}\nCode: {req.responseCode}\nResponse: {req.downloadHandler.text}";
                    onError?.Invoke(details);
                }
            }
        }


    }
}

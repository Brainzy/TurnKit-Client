using System;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace TurnKit
{
    public sealed class PlayerStoreValueBuilder<TValue>
    {
        private readonly PlayerStoreToken<TValue> _token;
        private readonly TurnKitClientIdentity _identity;

        internal PlayerStoreValueBuilder(PlayerStoreToken<TValue> token, TurnKitClientIdentity identity)
        {
            _token = token;
            _identity = identity;
        }

        public async Task<TValue> Get()
        {
            return (await GetResult()).Value;
        }

        public async Task<PlayerStoreValueResult<TValue>> GetResult()
        {
            if (!_token.ClientReadable)
            {
                throw new InvalidOperationException($"PlayerStore key '{_token.StoreKey}' is not client readable.");
            }

            using var request = TurnKitClientRequest.CreateGet($"/v1/client/player-store/{UnityWebRequest.EscapeURL(_token.StoreKey)}");
            await TurnKitClientRequest.PrepareIdentity(request, _identity);
            await TurnKitClientRequest.Send(request);
            return PlayerStoreValueCodec.ParseResponse<TValue>(request.downloadHandler.text, _token.ValueType, _token.StoreKey);
        }

        public async Task Set(TValue value)
        {
            await SetResult(value);
        }

        public async Task<PlayerStoreValueResult<TValue>> SetResult(TValue value)
        {
            if (!_token.ClientWritable)
            {
                throw new InvalidOperationException($"PlayerStore key '{_token.StoreKey}' is not client writable.");
            }

            string body = PlayerStoreValueCodec.BuildRequestBody(value, _token.ValueType, _token.StoreKey);
            using var request = TurnKitClientRequest.CreateJson($"/v1/client/player-store/{UnityWebRequest.EscapeURL(_token.StoreKey)}", "PUT", body);
            await TurnKitClientRequest.PrepareIdentity(request, _identity);
            await PlayerStoreRequestExecutor.SendWriteRequest(request, _token.StoreKey);
            return PlayerStoreValueCodec.ParseResponse<TValue>(request.downloadHandler.text, _token.ValueType, _token.StoreKey);
        }
    }
}

using System;
using System.Threading.Tasks;

#if UNITY_ANDROID && !UNITY_EDITOR && TURNKIT_GOOGLE_PLAY_GAMES
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

namespace TurnKit
{
    public static class TurnKitGooglePlayGamesAuth
    {
        public static Task<TurnKitPlayerSession> SignInAndExchange(bool forceRefreshToken = true)
        {
            return SignInAndExchange(forceRefreshToken, false);
        }

        public static async Task<TurnKitPlayerSession> SignInAndExchange(bool forceRefreshToken, bool manualSignIn)
        {
            string serverAuthCode = await GetServerAuthCode(forceRefreshToken, manualSignIn);
            return await TurnKitAuth.ExchangeGooglePlayGames(serverAuthCode);
        }

        public static Task<string> GetServerAuthCode(bool forceRefreshToken = true, bool manualSignIn = false)
        {
#if UNITY_ANDROID && !UNITY_EDITOR && TURNKIT_GOOGLE_PLAY_GAMES
            var completion = new TaskCompletionSource<string>();
            EnsurePlatformActivated();

            Action<SignInStatus> afterAuth = status =>
            {
                if (status != SignInStatus.Success)
                {
                    completion.TrySetException(new Exception($"Google Play Games sign-in failed: {status}"));
                    return;
                }

                PlayGamesPlatform.Instance.RequestServerSideAccess(forceRefreshToken, authCode =>
                {
                    if (string.IsNullOrWhiteSpace(authCode))
                    {
                        completion.TrySetException(new Exception("Google Play Games did not return a server auth code."));
                        return;
                    }

                    completion.TrySetResult(authCode);
                });
            };

            if (manualSignIn)
            {
                PlayGamesPlatform.Instance.ManuallyAuthenticate(afterAuth);
            }
            else
            {
                PlayGamesPlatform.Instance.Authenticate(afterAuth);
            }

            return completion.Task;
#else
            throw new PlatformNotSupportedException(
                "Google Play Games auth requires Android player build plus the Google Play Games Unity plugin. " +
                "Import the plugin and add TURNKIT_GOOGLE_PLAY_GAMES to Scripting Define Symbols.");
#endif
        }

        public static void SignOut()
        {
#if UNITY_ANDROID && !UNITY_EDITOR && TURNKIT_GOOGLE_PLAY_GAMES
            PlayGamesPlatform.Instance.SignOut();
#else
            throw new PlatformNotSupportedException(
                "Google Play Games sign-out requires Android player build plus the Google Play Games Unity plugin.");
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR && TURNKIT_GOOGLE_PLAY_GAMES
        private static bool _activated;

        private static void EnsurePlatformActivated()
        {
            if (_activated)
            {
                return;
            }

            PlayGamesPlatform.Activate();
            _activated = true;
        }
#endif
    }
}

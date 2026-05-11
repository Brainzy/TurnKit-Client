using System;
using UnityEngine;

namespace TurnKit
{
    internal sealed class RelayReconnectStore
    {
        private const string InstallIdPrefsKey = "TK.I";
        private const string ReconnectPrefix = "TK.R.";

        private string _installId;
        private string _prefsScope;

        public void Save(string playerId, string slug, string relayToken, int lastMoveNumber)
        {
            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(relayToken))
            {
                return;
            }

            PlayerPrefs.SetString(GetReconnectKey("P"), playerId);
            PlayerPrefs.SetString(GetReconnectKey("S"), slug);
            PlayerPrefs.SetString(GetReconnectKey("T"), relayToken);
            PlayerPrefs.SetInt(GetReconnectKey("M"), Mathf.Max(0, lastMoveNumber));
            PlayerPrefs.Save();
        }

        public RelayReconnectSnapshot Load()
        {
            string playerId = PlayerPrefs.GetString(GetReconnectKey("P"), null);
            string slug = PlayerPrefs.GetString(GetReconnectKey("S"), null);
            string relayToken = PlayerPrefs.GetString(GetReconnectKey("T"), null);
            int lastMoveNumber = Mathf.Max(0, PlayerPrefs.GetInt(GetReconnectKey("M"), 0));

            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(relayToken))
            {
                return null;
            }

            return new RelayReconnectSnapshot(playerId, slug, relayToken, lastMoveNumber);
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(GetReconnectKey("P"));
            PlayerPrefs.DeleteKey(GetReconnectKey("S"));
            PlayerPrefs.DeleteKey(GetReconnectKey("T"));
            PlayerPrefs.DeleteKey(GetReconnectKey("M"));
            PlayerPrefs.Save();
        }

        private string GetReconnectKey(string suffix)
        {
            return $"{ReconnectPrefix}{ResolvePrefsScope()}.{suffix}";
        }

        private string ResolvePrefsScope()
        {
            if (!string.IsNullOrWhiteSpace(_prefsScope))
            {
                return _prefsScope;
            }

#if UNITY_EDITOR
            _prefsScope = Hash8(Application.dataPath);
            return _prefsScope;
#elif UNITY_WEBGL
            _prefsScope = Hash8(Application.absoluteURL);
            return _prefsScope;
#else
            _prefsScope = GetOrCreateInstallId();
            return _prefsScope;
#endif
        }

        private string GetOrCreateInstallId()
        {
            if (!string.IsNullOrWhiteSpace(_installId))
            {
                return _installId;
            }

            string existing = PlayerPrefs.GetString(InstallIdPrefsKey, null);
            if (!string.IsNullOrWhiteSpace(existing))
            {
                _installId = existing;
                return _installId;
            }

            const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
            var random = new System.Random();
            var value = new char[12];
            for (int i = 0; i < value.Length; i++)
            {
                value[i] = chars[random.Next(chars.Length)];
            }

            _installId = new string(value);
            PlayerPrefs.SetString(InstallIdPrefsKey, _installId);
            PlayerPrefs.Save();
            return _installId;
        }

        private static string Hash8(string value)
        {
            string source = string.IsNullOrWhiteSpace(value) ? "main" : value;
            string hash = Hash128.Compute(source).ToString();
            return hash.Length <= 8 ? hash : hash.Substring(0, 8);
        }
    }
}

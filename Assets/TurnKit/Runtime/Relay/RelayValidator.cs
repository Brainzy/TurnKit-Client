using System;
using TurnKit.Internal.SimpleJSON;
using UnityEngine;

namespace TurnKit
{
    internal sealed class RelayValidator
    {
        public bool ValidateVotingConfiguration(TurnKitConfig.RelayConfig relay, out string error)
        {
            error = null;

            if (relay == null || !relay.votingEnabled)
            {
                return true;
            }

            if (relay.votingMode == TurnKitConfig.VotingMode.ASYNC &&
                relay.failAction != TurnKitConfig.FailAction.END_GAME)
            {
                error = $"{relay.slug}: ASYNC voting requires END_GAME fail action.";
                return false;
            }

            return true;
        }

        public void ValidateReadyToSend(bool isConnected, bool isInSyncWindow)
        {
            if (!isConnected)
            {
                throw new InvalidOperationException("[TurnKit] Not connected. Cannot send actions.");
            }

            if (isInSyncWindow)
            {
                throw new InvalidOperationException("[TurnKit] In sync window. Cannot send actions yet.");
            }
        }

        public bool ValidateSpawn(RelayList toList)
        {
            if (toList == null)
            {
                throw new ArgumentNullException(nameof(toList), "[TurnKit] Target list cannot be null.");
            }

            if (!toList.IsVisibleToMe)
            {
                Debug.LogError($"[TurnKit] Cannot spawn in '{toList.Name}': List not visible to you.");
                return false;
            }

            return true;
        }

        public bool ValidateMove(RelayList fromList, RelayList toList, bool ignoreOwnership)
        {
            if (fromList == null)
            {
                throw new ArgumentNullException(nameof(fromList), "[TurnKit] Source list cannot be null.");
            }

            if (toList == null)
            {
                throw new ArgumentNullException(nameof(toList), "[TurnKit] Target list cannot be null.");
            }

            if (!fromList.IsVisibleToMe && !ignoreOwnership)
            {
                Debug.LogError($"[TurnKit] Cannot move from '{fromList.Name}': List not visible to you.");
                return false;
            }

            if (!toList.IsVisibleToMe && !ignoreOwnership)
            {
                Debug.LogError($"[TurnKit] Cannot move to '{toList.Name}': List not visible to you.");
                return false;
            }

            if (!ignoreOwnership && !fromList.IsOwnedByMe)
            {
                Debug.LogError($"[TurnKit] Cannot move from '{fromList.Name}': You don't own this list. Use .IgnoreOwnership() if server allows it.");
                return false;
            }

            return true;
        }

        public bool ValidateRemove(RelayList fromList, bool ignoreOwnership)
        {
            if (fromList == null)
            {
                throw new ArgumentNullException(nameof(fromList), "[TurnKit] Source list cannot be null.");
            }

            if (!fromList.IsVisibleToMe)
            {
                Debug.LogError($"[TurnKit] Cannot remove from '{fromList.Name}': List not visible to you.");
                return false;
            }

            if (!ignoreOwnership && !fromList.IsOwnedByMe)
            {
                Debug.LogError($"[TurnKit] Cannot remove from '{fromList.Name}': You don't own this list. Use .IgnoreOwnership() if server allows it.");
                return false;
            }

            return true;
        }

        public bool ValidateShuffle(RelayList list)
        {
            if (list == null)
            {
                throw new ArgumentNullException(nameof(list), "[TurnKit] List cannot be null.");
            }

            if (!list.IsVisibleToMe)
            {
                Debug.LogError($"[TurnKit] Cannot shuffle '{list.Name}': List not visible to you.");
                return false;
            }

            return true;
        }

        public bool ValidateTrackedStatMetadata(string statName, TrackedStatMetadata metadata)
        {
            if (string.IsNullOrWhiteSpace(statName) || metadata == null)
            {
                Debug.LogError($"[TurnKit] Unknown tracked stat '{statName}'. Generate enums and make sure the relay config is initialized.");
                return false;
            }

            return true;
        }

        public bool ValidateStatTarget(TrackedStatMetadata metadata, TurnKitConfig.PlayerSlot? slot, string playerId)
        {
            if (metadata.Scope == TurnKitConfig.TrackedStatScope.MATCH)
            {
                if (slot.HasValue || !string.IsNullOrEmpty(playerId))
                {
                    Debug.LogError($"[TurnKit] MATCH stat '{metadata.Name}' must not target a player.");
                    return false;
                }

                return true;
            }

            if (!slot.HasValue || string.IsNullOrWhiteSpace(playerId))
            {
                Debug.LogError($"[TurnKit] PER_PLAYER stat '{metadata.Name}' requires a resolved player slot.");
                return false;
            }

            return true;
        }

        public bool ValidateSetStat(TrackedStatMetadata metadata, JSONNode value)
        {
            if (value == null || value.IsNull)
            {
                Debug.LogError($"[TurnKit] Stat '{metadata.Name}' requires a value.");
                return false;
            }

            return ValidateValueType(metadata, value, nameof(value));
        }

        public bool ValidateAddStat(TrackedStatMetadata metadata, double? delta, string[] values)
        {
            switch (metadata.DataType)
            {
                case TurnKitConfig.TrackedStatDataType.DOUBLE:
                    if (!delta.HasValue)
                    {
                        Debug.LogError($"[TurnKit] DOUBLE stat '{metadata.Name}' requires a numeric delta for Add().");
                        return false;
                    }

                    return true;
                case TurnKitConfig.TrackedStatDataType.LIST_STRING:
                    if (values == null)
                    {
                        Debug.LogError($"[TurnKit] LIST_STRING stat '{metadata.Name}' requires one or more string values for Add().");
                        return false;
                    }

                    foreach (var item in values)
                    {
                        if (item == null)
                        {
                            Debug.LogError($"[TurnKit] LIST_STRING stat '{metadata.Name}' cannot add null values.");
                            return false;
                        }
                    }

                    return true;
                default:
                    Debug.LogError($"[TurnKit] STRING stat '{metadata.Name}' does not support Add(). Use Set() instead.");
                    return false;
            }
        }

        private bool ValidateValueType(TrackedStatMetadata metadata, JSONNode value, string fieldName)
        {
            switch (metadata.DataType)
            {
                case TurnKitConfig.TrackedStatDataType.DOUBLE:
                    if (value.Tag != JSONNodeType.Number)
                    {
                        Debug.LogError($"[TurnKit] Stat '{metadata.Name}' requires a numeric {fieldName}.");
                        return false;
                    }

                    return true;
                case TurnKitConfig.TrackedStatDataType.STRING:
                    if (value.Tag != JSONNodeType.String)
                    {
                        Debug.LogError($"[TurnKit] Stat '{metadata.Name}' requires a string {fieldName}.");
                        return false;
                    }

                    return true;
                case TurnKitConfig.TrackedStatDataType.LIST_STRING:
                    if (value.Tag != JSONNodeType.Array)
                    {
                        Debug.LogError($"[TurnKit] Stat '{metadata.Name}' requires a string-array {fieldName}.");
                        return false;
                    }

                    foreach (JSONNode item in value.AsArray)
                    {
                        if (item.Tag != JSONNodeType.String)
                        {
                            Debug.LogError($"[TurnKit] Stat '{metadata.Name}' requires all array {fieldName} items to be strings.");
                            return false;
                        }
                    }

                    return true;
                default:
                    return false;
            }
        }
    }
}

using System;
using UnityEngine;

namespace TurnKit
{
    internal sealed class RelayValidator
    {
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

            if (!fromList.IsVisibleToMe)
            {
                Debug.LogError($"[TurnKit] Cannot move from '{fromList.Name}': List not visible to you.");
                return false;
            }

            if (!toList.IsVisibleToMe)
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
    }
}

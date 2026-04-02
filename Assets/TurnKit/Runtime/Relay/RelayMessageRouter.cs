using System;
using TurnKit.Internal.SimpleJSON;
using UnityEngine;

namespace TurnKit
{
    internal sealed class RelayMessageRouter
    {
        private readonly RelaySessionState _state;
        private readonly Action<RelayList, ListChangeType> _notifyListChanged;

        public RelayMessageRouter(RelaySessionState state, Action<RelayList, ListChangeType> notifyListChanged)
        {
            _state = state;
            _notifyListChanged = notifyListChanged;
        }

        public RelayMessageOutcome Process(string raw)
        {
            var node = JSON.Parse(raw);
            string type = node["type"];
            if (string.IsNullOrEmpty(type))
            {
                return null;
            }

            switch (type)
            {
                case "MATCH_STARTED":
                    return HandleMatchStarted(raw, node);
                case "MOVE_MADE":
                    return HandleMoveMade(raw, node);
                case "SYNC_COMPLETE":
                    return HandleSyncComplete(raw);
                case "TURN_CHANGED":
                    return HandleTurnChanged(raw);
                case "VOTE_FAILED":
                    return new RelayMessageOutcome
                    {
                        EventType = RelayEventType.VoteFailed,
                        VoteFailed = JsonUtility.FromJson<VoteFailedMessage>(raw)
                    };
                case "ERROR":
                    return new RelayMessageOutcome
                    {
                        EventType = RelayEventType.Error,
                        Error = JsonUtility.FromJson<ErrorMessage>(raw)
                    };
                case "GAME_ENDED":
                    return new RelayMessageOutcome
                    {
                        EventType = RelayEventType.GameEnded,
                        GameEnded = JsonUtility.FromJson<GameEndedMessage>(raw)
                    };
                default:
                    return null;
            }
        }

        private RelayMessageOutcome HandleMatchStarted(string raw, JSONNode node)
        {
            var msg = JsonUtility.FromJson<MatchStartedMessage>(raw);
            _state.ApplyMatchStarted(msg, node);

            return new RelayMessageOutcome
            {
                EventType = RelayEventType.MatchStarted,
                MatchStarted = msg
            };
        }

        private RelayMessageOutcome HandleMoveMade(string raw, JSONNode node)
        {
            var msg = JsonUtility.FromJson<MoveMadeMessage>(raw);
            var changesArray = node["changes"].AsArray;

            if (changesArray != null)
            {
                msg.changes = new VisibleChange[changesArray.Count];
                for (int i = 0; i < changesArray.Count; i++)
                {
                    msg.changes[i] = ParseVisibleChange(changesArray[i]);
                }

                _state.ApplyVisibleChanges(msg.changes, _notifyListChanged);
            }
            else
            {
                msg.changes = Array.Empty<VisibleChange>();
            }

            _state.ApplyMoveMade(msg);

            return new RelayMessageOutcome
            {
                EventType = RelayEventType.MoveMade,
                MoveMade = msg
            };
        }

        private RelayMessageOutcome HandleSyncComplete(string raw)
        {
            var msg = JsonUtility.FromJson<SyncCompleteMessage>(raw);
            _state.ApplySyncComplete(msg);

            return new RelayMessageOutcome
            {
                EventType = RelayEventType.SyncComplete,
                SyncComplete = msg
            };
        }

        private RelayMessageOutcome HandleTurnChanged(string raw)
        {
            var msg = JsonUtility.FromJson<TurnChangedMessage>(raw);
            _state.ApplyTurnChanged(msg);

            return new RelayMessageOutcome
            {
                EventType = RelayEventType.TurnChanged,
                TurnChanged = msg
            };
        }

        private VisibleChange ParseVisibleChange(JSONNode node)
        {
            var change = new VisibleChange
            {
                type = (ChangeType)Enum.Parse(typeof(ChangeType), node["type"].Value.ToUpper()),
                fromList = node["fromList"]?.Value,
                toList = node["toList"]?.Value,
                actingSlot = node["actingSlot"]?.Value
            };

            var itemsArray = node["items"]?.AsArray;
            if (itemsArray == null)
            {
                change.items = Array.Empty<RelayItem>();
                return change;
            }

            change.items = new RelayItem[itemsArray.Count];
            for (int i = 0; i < itemsArray.Count; i++)
            {
                var itemNode = itemsArray[i];
                change.items[i] = new RelayItem(
                    itemNode["id"],
                    itemNode["slug"],
                    (TurnKitConfig.PlayerSlot)itemNode["creatorSlot"].AsInt
                );
            }

            return change;
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace TurnKit.Example
{
    /*
    public class RockPaperScissorsControllerExample : MonoBehaviour
    {
        [SerializeField] private InputField playerIdText;
        [SerializeField] private Text gameEndText;
        [SerializeField] private Text statusText;
        [SerializeField] private Text opponentText;

        private string myList;
        private bool isSignPicked;
        private void Awake()
        {
#if UNITY_EDITOR
            playerIdText.text = ClonesManager.IsClone() ? "player2" : "player1";
#endif
            Relay.OnMoveMade += OnMoveMade;
            Relay.OnTurnChanged += OnTurnChanged;
            Relay.OnVoteFailed += OnVoteFailed;
            Relay.OnMatchStarted += (msg,initialLists) =>
            {
                var a = Relay.GetMyList();
                myList = msg.yourTurn ? "p1_hidden" : "p2_hidden";
                statusText.text = "Game started";
            };
        }

        public async void FindMatch()
        {
            isSignPicked = false;
            opponentText.text = "";
            gameEndText.text = "";
            statusText.text = "Waiting for opponent, connect with another client";
            await Relay.MatchWithAnyone(playerIdText.text, "example");
        }

        public void SignChosen(string sign)
        {
            if (isSignPicked) return;
            Relay.List().Spawn();
            Relay.Move(null, true, new List<RelayAction>(){ RelayAction.Add(myList, sign, 1, playerIdText.text) });
        }

        private void OnVoteFailed(VoteFailedMessage voteFailedMessage)
        {
            gameEndText.text = "Cheating detected game ended";
            statusText.text = "Game ended";
        }

        private void OnMoveMade(MoveMadeMessage arg1, IReadOnlyList<RelayList> arg2)
        {
            currentLists = lists;
            
            Relay.Vote(msg.moveNumber, IsMoveValid(msg));
            if (msg.playerId != playerIdText.text) opponentText.text = "Opponent chosen sign";
            else isSignPicked = true;
            
            var publicList = currentLists["results_public"];
            if (publicList.Count == 2) //signs are revealed
            {
                var mySign = publicList.First(x => x.creatorSlot == Relay.Instance.MySlot()).slug;
                var opponentSign = publicList.First(x => x.creatorSlot != Relay.Instance.MySlot()).slug;
                opponentText.text = $"Opponent chose {opponentSign}";
                if (mySign == opponentSign)
                {
                    EndGame("A draw, close one! Press Find Match to replay it");
                    return;
                }
                
                bool iWin = (mySign == "ROCK" && opponentSign == "SCISSORS") ||
                            (mySign == "PAPER" && opponentSign == "ROCK") ||
                            (mySign == "SCISSORS" && opponentSign == "PAPER");
                EndGame(iWin ? "You won ! Press Find Match to replay it" : "You lost! Press Press Find Match to replay it");
            }
        }

        private bool IsMoveValid(MoveMadeMessage msg)
        {
            if (currentLists == null) return false;

            foreach (var change in msg.changes)
            {
                if (change.type == ChangeType.MOVE)
                {
                    currentLists
                }
                switch (act.action)
                {
                    case "add": // player adding to not owned list is covered by server, it checks ownership
                        if (currentLists[act.targetList].Count > 0) return false; // {msg.playerId} tried to pick a second sign.
                        string[] validSigns = {"ROCK", "PAPER", "SCISSORS"};
                        if (!validSigns.Contains(act.slug)) return false; // {act.slug} is not a valid sign.
                        break;
                    case "moveAll":
                        bool p1Ready = currentLists["p1_hidden"].Count > 0;
                        bool p2Ready = currentLists["p2_hidden"].Count > 0;
                        if (!p1Ready || !p2Ready) return false; // Attempted to reveal before both players picked
                        break;
                    default: return false; // Reject any unknown actions for security
                }
            }
            return true;
        }
        
        private void OnTurnChanged(TurnChangedMessage message)
        {
            if (currentLists == null) return;
            if (currentLists["p1_hidden"].Count > 0 && currentLists["p2_hidden"].Count > 0) { // both players picked their sign
                Debug.Log("Both players ready. Executing Reveal...");
                Relay.Move(null, true, new List<RelayAction> {
                    RelayAction.MoveAll("p1_hidden", "results_public", true),
                    RelayAction.MoveAll("p2_hidden", "results_public", true)
                });
            }
        }
        
        private void EndGame(string gameEndReason)
        {
            Relay.EndGame();
            gameEndText.text = gameEndReason;
            statusText.text = "Game ended";
        }
    }
    */
}
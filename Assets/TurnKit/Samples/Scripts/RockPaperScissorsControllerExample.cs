using System.Collections.Generic;
using System.Linq;
using TurnKit.Internal.ParrelSync;
using UnityEngine;
using UnityEngine.UI;

namespace TurnKit.Example
{
    public class RockPaperScissorsControllerExample : MonoBehaviour
    {
        /*
        [SerializeField] private InputField playerIdText;
        [SerializeField] private Text gameEndText;
        [SerializeField] private Text statusText;
        [SerializeField] private Text opponentText;

        private RelayList myHand;
        private RelayList opponentHand;
        private RelayList table;
        private bool isSignPicked;
        private readonly string[] validSigns = {"ROCK", "PAPER", "SCISSORS"};
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
                myHand = Relay.GetMyLists(ExampleConfig.Tag.hand).First();
                opponentHand = Relay.GetOpponentsLists(ExampleConfig.Tag.hand).First();
                table = Relay.GetMyLists(ExampleConfig.Tag.table).First();
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
            myHand.Spawn(sign);
            Relay.EndMyTurn();
        }

        private void OnVoteFailed(VoteFailedMessage voteFailedMessage)
        {
            gameEndText.text = "Cheating detected game ended";
            statusText.text = "Game ended";
        }

        private void OnMoveMade(MoveMadeMessage msg, IReadOnlyList<RelayList> arg2)
        {
            Relay.Vote(msg.moveNumber, IsMoveValid(msg));
            if (msg.playerId != playerIdText.text) opponentText.text = "Opponent chosen sign";
            else isSignPicked = true;
            
            if (table.Count == 2) //signs are revealed
            {
                var mySign = myHand.Items.First().Slug;
                var opponentSign = opponentHand.Items.First().Slug;
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
            foreach (var change in msg.changes)
            {
                if (change.type == ChangeType.SPAWN) // player adding to not owned list is covered by server, it checks ownership
                {
                    var list = change.fromList;
                    if (list.Items.Count > 0) return false; // tried to pick a second sign.
                    if (!validSigns.Contains(list.Items.First().Slug)) return false; // {act.slug} is not a valid sign
                }

                if (change.type != ChangeType.MOVE)
                {
                    var fromList = change.fromList;
                    var toList = change.toList;
                    if (change.items.Length != 2) return false; // must be 2 signs moved to public list
                  
                    bool p1Ready = currentLists["p1_hidden"].Count > 0;
                        bool p2Ready = currentLists["p2_hidden"].Count > 0;
                        if (!p1Ready || !p2Ready) return false; // Attempted to reveal before both players picked
                        break;

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
*/
    }
}

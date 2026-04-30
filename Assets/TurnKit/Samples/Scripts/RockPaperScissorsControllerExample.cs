using System.Collections.Generic;
using System.Linq;
using TurnKit.Internal.ParrelSync;
using UnityEngine;
using UnityEngine.UI;

namespace TurnKit.Example
{
    public class RockPaperScissorsControllerExample : MonoBehaviour
    {
        [SerializeField] private InputField playerIdText;
        [SerializeField] private Text gameEndText;
        [SerializeField] private Text statusText;
        [SerializeField] private Text opponentText;

        private RelayList myHand;
        private RelayList opponentHand;
        private RelayList revealedList;
        private bool isSignPicked;
        private readonly string[] validSigns = {"ROCK", "PAPER", "SCISSORS"};
        private void Awake()
        {
#if UNITY_EDITOR
            playerIdText.text = ClonesManager.IsClone() ? "player2" : "player1";
#endif
            Relay.OnMoveMade += OnMoveMade;
            Relay.OnTurnStarted += OnTurnStarted;
            Relay.OnVoteFailed += OnVoteFailed;
            Relay.OnMatchStarted += (msg,initialLists) =>
            {
                myHand = Relay.GetMyLists(ExampleConfig.Tag.hand).First();
                opponentHand = Relay.GetOpponentsLists(ExampleConfig.Tag.hand).First();
                revealedList = Relay.GetMyLists(ExampleConfig.Tag.table).First();
                statusText.text = "Game started";
            };
        }

        public async void FindMatch()
        {
            isSignPicked = false;
            opponentText.text = "";
            gameEndText.text = "";
            statusText.text = "Waiting for opponent, connect with another client";
            await Relay.MatchWithAnyone(playerIdText.text, ExampleConfig.Slug);
        }

        public void SignChosen(string sign)
        {
            if (isSignPicked) return;
            myHand.Spawn(sign);
            Relay.EndTurn().ForPlayer(Relay.MySlot);
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
            
            if (revealedList.Count == 2) //signs are revealed
            {
                var mySign = revealedList.Items.First(x => x.CreatorSlot == Relay.MySlot).Slug;
                var opponentSign = revealedList.Items.First(x => x.CreatorSlot != Relay.MySlot).Slug;
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
                    if (change.toList.Items.Count > 1) return false; // tried to pick a second sign.
                    if (!validSigns.Contains(change.toList.Items.First().Slug)) return false; // {act.slug} is not a valid sign
                }

                if (change.type == ChangeType.MOVE)
                {
                    if (change.ids.Length != 1) return false; // 1 sign moved from each list
                    if (myHand.Items.Count != 0 || opponentHand.Items.Count != 0) return false; // must be no items remaining
                }
            }
            return true;
        }
        
        private void OnTurnStarted(TurnStartedMessage message)
        {
            if (myHand.Items.Count == 1 && opponentHand.Items.Count == 1 && Relay.IsMyTurn) // both players picked their sign
            {
                Debug.Log("Both players ready. Executing Reveal...");
                myHand.Move(SelectorType.ALL).To(revealedList);
                opponentHand.Move(SelectorType.ALL).IgnoreOwnership().To(revealedList);
                Relay.EndTurn().ForPlayer(Relay.MySlot);
            }
        }
        
        private void EndGame(string gameEndReason)
        {
            Relay.EndGame();
            gameEndText.text = gameEndReason;
            statusText.text = "Game ended";
        }
    }
}

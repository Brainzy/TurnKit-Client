using System.Collections.Generic;
using System.Linq;
using TurnKit.Internal.ParrelSync;
using UnityEngine;
using UnityEngine.UI;

namespace TurnKit.Example
{
    public class TicTacToeControllerExample : MonoBehaviour
    {
        [SerializeField] private List<Text> texts;
        [SerializeField] private InputField playerIdText;
        [SerializeField] private Text gameEndText;
        [SerializeField] private Text statusText;
        [SerializeField] private Toggle allowInvalidMovesToggle;
        private readonly int[][] _winConditions = { new[] {0, 1, 2}, new[] {3, 4, 5}, new[] {6, 7, 8}, // Rows
            new[] {0, 3, 6}, new[] {1, 4, 7}, new[] {2, 5, 8}, // Cols
            new[] {0, 4, 8}, new[] {2, 4, 6}             // Diagonals
        };

        private void Awake()
        {
#if UNITY_EDITOR
            playerIdText.text = ClonesManager.IsClone() ? "player2" : "player1";
#endif
            Relay.OnMoveMade += (message, _) => OnMoveMade(message);
            Relay.OnVoteFailed += OnVoteFailed;
            Relay.OnMatchStarted += (_, _) => { statusText.text = "Game started"; };
        }
        
        public async void FindMatch()
        {
            foreach (var text in texts) text.text = "";
            gameEndText.text = "";
            statusText.text = "Waiting for opponent, connect with another client";
            await Relay.MatchWithAnyone(playerIdText.text, ExampleConfig.Slug);
        }

        public void OnCellClick(int index)
        {
            if (!string.IsNullOrEmpty(texts[index].text) && !allowInvalidMovesToggle.isOn) return;
            Relay.SendJson(index.ToString());
            Relay.EndMyTurn();
        }

        private void OnVoteFailed(VoteFailedMessage voteFailedMessage)
        {
            gameEndText.text = "Cheating detected game ended";
            statusText.text = "Game ended";
        }

        private void OnMoveMade(MoveMadeMessage message)
        {
            int cellIndex = int.Parse(message.payload);
            bool isLegalMove = string.IsNullOrEmpty(texts[cellIndex].text) || allowInvalidMovesToggle.isOn;
            Relay.Vote(message.moveNumber, isLegalMove);
            
            if (!isLegalMove) return;
            
            string symbol = message.moveNumber % 2 == 0 ? "O" : "X"; // move number 1 is X symbol and every odd one after
            texts[cellIndex].text = symbol; // placing of the X or O
            if (CheckWin()) EndGame($"{symbol} won ! Press Find Match to replay it");
            else if (message.moveNumber == 9) EndGame("A draw, close one! Press Find Match to replay it");
        }

        private void EndGame(string gameEndReason)
        {
            Relay.EndGame();
            gameEndText.text = gameEndReason;
            statusText.text = "Game ended";
        }
        
        private bool CheckWin() => _winConditions.Any(l => !string.IsNullOrEmpty(texts[l[0]].text) && texts[l[0]].text == texts[l[1]].text && texts[l[0]].text == texts[l[2]].text);
    }
}

using BepInEx.IL2CPP.Utils.Collections;
using System;
using System.Collections;
using static Multi1v1.RankSystemConstants;
using static Multi1v1.RankSystemUtility;

namespace Multi1v1
{
    public class RankSystemConstants
    {
    }

    public class RankSystemPatches
    {
        /// <summary>
        /// Handles Elo updates when a player dies in a ranked game.
        /// </summary>
        [HarmonyPatch(typeof(ServerSend), nameof(ServerSend.PlayerDied))]
        [HarmonyPostfix]
        public static void OnServerSendPlayerDiedPost(ulong __0, ulong __1)
        {
            if (!IsHost()) return;
            if (!gameHasStarted || !playerToMatchIndex.ContainsKey(__0) || !ranked) return;

            MainManager.Instance.StartCoroutine(OnPlayerDieUpdateEloCoroutine(__0).WrapToIl2Cpp());
        }

        /// <summary>
        /// Handles Elo updates when a player leaves the lobby mid-game.
        /// </summary>
        [HarmonyPatch(typeof(LobbyManager), nameof(LobbyManager.RemovePlayerFromLobby))]
        [HarmonyPrefix]
        public static void OnLobbyManagerRemovePlayerFromLobbyPre(CSteamID __0)
        {
            if (!IsHost()) return;
            if (!gameHasStarted || !playerToMatchIndex.ContainsKey((ulong)__0) || !ranked) return;

            ulong oppId = Multi1v1Utility.GetOpponent((ulong)__0);

            if (oppId == 0) return;

            UpdateMatchElo(oppId, (ulong)__0);

            playerToMatchIndex.Remove((ulong)__0);
            playerToMatchIndex.Remove(oppId);
        }

    }

    public class RankSystemUtility
    {

        public static IEnumerator OnPlayerDieUpdateEloCoroutine(ulong deadPlayer)
        {
            yield return new WaitForSeconds(1f);
            if (!playerToMatchIndex.ContainsKey((ulong)deadPlayer)) yield break;

            ulong oppId = Multi1v1Utility.GetOpponent(deadPlayer);

            if (oppId == 0) yield break;

            UpdateMatchElo(oppId, deadPlayer);

            playerToMatchIndex.Remove(deadPlayer);
            playerToMatchIndex.Remove(oppId);
        }
        /// <summary>
        /// Calculates the probability of a player winning based on their Elo compared to the average game Elo.
        /// </summary>
        public static float WinExpectative(float playerElo, float averageGameElo)
            => 1.0f / (1.0f + (float)Math.Pow(10.0, (averageGameElo - playerElo) / eloScalingFactor));

        /// <summary>
        /// Updates a player's Elo based on their ranking and whether they shared the rank with others.
        /// </summary>
        public static void UpdateMatchElo(ulong winnerId, ulong loserId)
        {
            var winnerData = Database._instance?.GetPlayerData(winnerId);
            var loserData = Database._instance?.GetPlayerData(loserId);

            if (winnerData == null || loserData == null) return;
            
            float winnerElo = winnerData.Properties.TryGetValue("Elo", out var storedWinnerElo) && storedWinnerElo is float wElo
                ? wElo
                : (winnerData.Properties.TryGetValue("LastElo", out var storedWinnerLastElo) && storedWinnerLastElo is float wLastElo ? wLastElo : 1000f);

            float loserElo = loserData.Properties.TryGetValue("Elo", out var StoredLoserElo) && StoredLoserElo is float lElo
              ? lElo
              : (loserData.Properties.TryGetValue("LastElo", out var storedLastLoserElo) && storedLastLoserElo is float lastLElo ? lastLElo : 1000f);

            Database._instance.SetData(winnerId, "LastElo", winnerElo);
            Database._instance.SetData(loserId, "LastElo", loserElo);


            float winnerEloGain = kFactor * (1 - WinExpectative(winnerElo, loserElo));
            float loserEloGain = kFactor * (0 - WinExpectative(loserElo, winnerElo));

            winnerElo = Math.Max(100, winnerElo + winnerEloGain);
            loserElo = Math.Max(100, loserElo + loserEloGain);

            string winnerSign = winnerEloGain >= 0 ? "+" : "";
            SendPrivateMessage(winnerId, $"You Won | [{winnerSign}{winnerEloGain:F1}] --> Your Elo: {winnerElo:F1} ({RankName.GetRankFromElo(winnerElo)})");

            string loserSign = loserEloGain >= 0 ? "+" : "";
            SendPrivateMessage(loserId, $"You lose | [{loserSign}{loserEloGain:F1}] --> Your Elo: {loserElo:F1} ({RankName.GetRankFromElo(loserElo)})");



            // Update Elo in database
            Database._instance.SetData(winnerId, "Elo", winnerElo);
            Database._instance.SetData(winnerId, "Rank", RankName.GetRankFromElo(winnerElo));

            Database._instance.SetData(loserId, "Elo", loserElo);
            Database._instance.SetData(loserId, "Rank", RankName.GetRankFromElo(loserElo));
        }

        public static class RankName
        {
            private static readonly SortedDictionary<int, string> EloRanks = new()
            {
                { 100, "Clown" },
                { 800, "Bronze I" },
                { 850, "Bronze II" },
                { 900, "Bronze III" },
                { 950, "Bronze IV" },
                { 975, "Silver I" },
                { 1000, "Silver II" },
                { 1025, "Silver III" },
                { 1050, "Silver IV" },
                { 1075, "Gold I" },
                { 1100, "Gold II" },
                { 1125, "Gold III" },
                { 1150, "Gold IV" },
                { 1170, "Platinum I" },
                { 1190, "Platinum II" },
                { 1210, "Platinum III" },
                { 1230, "Platinum IV" },
                { 1245, "Diamond I" },
                { 1260, "Diamond II" },
                { 1275, "Diamond III" },
                { 1290, "Diamond IV" },
                { 1300, "Master I" },
                { 1310, "Master II" },
                { 1320, "Master III" },
                { 1330, "Master IV" },
                { 1350, "GM I" },
                { 1370, "GM II" },
                { 1390, "GM III" },
                { 1400, "GM IV" },
                { 2000, "Challenger" },
            };

            /// <summary>
            /// Retrieves the player's rank based on their Elo rating.
            /// </summary>
            public static string GetRankFromElo(float elo)
            {
                string rank = "Unranked"; // Default rank if below the lowest range

                foreach (var entry in EloRanks)
                {
                    if (elo >= entry.Key)
                        rank = entry.Value;
                    else
                        break; // Stop checking once we exceed the current Elo range
                }

                return rank;
            }
        }

    }
}

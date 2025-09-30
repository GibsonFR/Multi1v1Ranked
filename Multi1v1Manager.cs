namespace Multi1v1
{
    using BepInEx.IL2CPP.Utils.Collections;
    using System;
    using System.Collections;
    using static Multi1v1.Multi1v1Utility;
    

    internal class Multi1v1Patches
    {

        [HarmonyPatch(typeof(ServerSend), nameof(ServerSend.GameSpawnPlayer))]
        [HarmonyPrefix]
        public static bool OnServerSendGameSpawnPlayerPre(ulong __0, ulong __1, ref Vector3 __2)
        {
            if (!IsHost()) return true;

            ulong spawnerId = __0;
            ulong spawnedId = __1;

            // Spawn everyone for host, spawn everyone to themselves, spawn everyone to spectators and spawn each matches to each others
            if (spawnerId == clientId || !playerToMatchIndex.ContainsKey(spawnerId) || spawnerId == spawnedId || playerToMatchIndex[spawnerId] == playerToMatchIndex[spawnedId]) return true;
            else return false;
        }

        [HarmonyPatch(typeof(ServerHandle), nameof(ServerHandle.GameRequestToSpawn))]
        [HarmonyPrefix]
        public static void OnServerHandleGameRequestToSpawnPre(ulong __0)
        {
            if (!IsHost()) return;

            ulong requestingClientId = __0;

            // Prevent the host to spawn, and player not assigned to a match
            if (requestingClientId == clientId || !playerToMatchIndex.ContainsKey(requestingClientId)) LobbyManager.Instance.GetClient(requestingClientId).field_Public_Boolean_0 = false;
            else
            {
                LobbyManager.Instance.GetClient(requestingClientId).field_Public_Boolean_0 = true;
            }
        }

        [HarmonyPatch(typeof(ServerSend), nameof(ServerSend.GameSpawnPlayer))]
        [HarmonyPostfix]
        public static void OnServerHandleGameRequestToSpawnPost()
        {
            if (!IsHost()) return;

            if (connectedPlayers.Count >= 3 && gamemodeId == 0) MainManager.Instance.StartCoroutine(NextGameCoroutine().WrapToIl2Cpp());
        }

        [HarmonyPatch(typeof(GameMode), nameof(GameMode.Init))]
        [HarmonyPostfix]
        public static void GameModeInit()
        {
            if (!IsHost()) return;

            LobbyManager.Instance.gameMode.shortModeTime = 55;
            LobbyManager.Instance.gameMode.longModeTime = 55;
            LobbyManager.Instance.gameMode.mediumModeTime = 55;
        }

        [HarmonyPatch(typeof(GameLoop), nameof(GameLoop.StartGames))]
        [HarmonyPrefix]
        public static void OnServerSendGameStartPre()
        {
            Multi1v1MatchMaking();
        }

        // Prevent the game to give tag to players automatically
        [HarmonyPatch(typeof(GameModeTag), nameof(GameModeTag.OnFreezeOver))]
        [HarmonyPrefix]
        public static bool GameModeTagOnFreezeOver()
        {
            if (!IsHost() || !ranked) return true;

            gameHasStarted = true;

            foreach (var match in matches)
            {
                // Filter invalid matches
                if (match.Value.Count != 2)
                {
                    foreach (var player in match.Value)
                    {
                        playerToMatchIndex.Remove(player);
                        try { ServerSend.PlayerDied(player, player, Vector3.zero); }
                        catch { }
                    }
                }

                // Assign tag to a random player of the match
                Random rand = new Random();
                ulong tagger = match.Value[rand.Next(1)];

                try
                {
                    ServerSend.TagPlayer(0, tagger);
                    GameServer.ForceGiveWeapon(tagger, 10, 0);
                }
                catch { }
            }

            return false;
        }

        [HarmonyPatch(typeof(GameModeTag), nameof(GameModeTag.OnRoundOver))]
        [HarmonyPrefix]
        public static void GameModeTagOnRoundOverPre()
        {
            MainManager.Instance.StartCoroutine(NextGameCoroutine().WrapToIl2Cpp());
        }


        [HarmonyPatch(typeof(ServerSend), nameof(ServerSend.SendToLobby))]
        [HarmonyPrefix]
        public static bool OnServerSendSendToLobbyPre()
        {
            GameLoop.Instance.RestartLobby();
            GameLoop.Instance.StartGames();
            return false;
        }

        [HarmonyPatch(typeof(ServerSend), nameof(ServerSend.LoadMap), [typeof(int), typeof(int)])]
        [HarmonyPrefix]
        public static void OnServerSendLoadMapPre(ref int __0, ref int __1)
        {
            gameHasStarted = false;

            if (connectedPlayers.Count >= 3)
            {
                List<int> playableMaps = [35, 36, 38, 39, 40];

                Random random = new Random();

                __0 = playableMaps[random.Next(playableMaps.Count)];
                __1 = 4;
            }
            else
            {
                __0 = 6;
                __1 = 0;
            }

            gamemodeId = __1;
        }
    }

    public static class Multi1v1Utility
    {
        public static void Multi1v1MatchMaking()
        {
            Random random1 = new Random();
            Random random2 = new Random();

            var playerToAssign = connectedPlayers.Keys.ToList();

            // Exclude host from player to assign
            playerToAssign.Remove(clientId);

            playerToMatchIndex.Clear();
            matches.Clear();

            int index = 0;


            // will randomly assign two player to a match until 1 player remain
            while (playerToAssign.Count > 1)
            {
                var p1 = playerToAssign[random1.Next(playerToAssign.Count)];
                playerToMatchIndex.Add(p1, index);
                playerToAssign.Remove(p1);

                var p2 = playerToAssign[random2.Next(playerToAssign.Count)];
                playerToMatchIndex.Add(p2, index);
                playerToAssign.Remove(p2);

                matches.Add(index, [p1, p2]);

                index++;
            }
        }

        public static ulong GetOpponent(ulong clientId)
        {
            if (!playerToMatchIndex.ContainsKey(clientId)) return 0;

            int matchIndex = playerToMatchIndex[clientId];

            foreach (var player in matches[matchIndex]) if (player != clientId) return player;

            return 0;
        }

        public static IEnumerator NextGameCoroutine()
        {
            yield return new WaitForSeconds(2f);

            GameLoop.Instance.RestartLobby();
            GameLoop.Instance.StartGames();
        }
    }
}

using System.Collections;
using HarmonyLib;
using Steamworks;
using Unity.Netcode;
using UnityEngine;

namespace ShipLobby.Patches
{
    [HarmonyPatch]
    internal class GameNetworkManagerPatcher
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.Singleton_OnClientConnectedCallback))]
        private static void LogConnect()
        {
            ShipLobby.Log.LogDebug("Player connected.");
        }
        
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.Singleton_OnClientDisconnectCallback))]
        private static void LogDisconnect()
        {
            ShipLobby.Log.LogDebug("Player disconnected.");
        }

        /// <summary>
        /// Ensure that any incoming connections are properly accepted.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.ConnectionApproval))]
        private static void FixConnectionApproval(GameNetworkManager __instance, NetworkManager.ConnectionApprovalResponse response)
        {
            // Only override refusals that are due to the current game state.
            if (response.Approved || response.Reason != "Game has already started!")
                return;
            
            if (__instance.gameHasStarted && StartOfRound.Instance.inShipPhase)
            {
                ShipLobby.Log.LogDebug("Approving almost-refused incoming connection.");
                response.Reason = "";
                response.Approved = true;
            }
        }

        /// <summary>
        /// Prevent leaving the lobby on starting the first game.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.LeaveLobbyAtGameStart))]
        private static bool PreventSteamLobbyLeave(GameNetworkManager __instance)
        {
            // Set the lobby unjoinable so we won't have people trying to get in mid-game.
            __instance.SetLobbyJoinable(false);
            ShipLobby.Log.LogDebug("Preventing the closing of Steam lobby.");
            // Prevent closing down the lobby.
            return false;
        }

        /// <summary>
        /// Reopen the steam lobby after a game has ended.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.EndOfGame))]
        private static void ReopenSteamLobbyPatch(StartOfRound __instance)
        {
            ShipLobby.Instance.StartCoroutine(ReopenSteamLobby(__instance));
        }

        private static IEnumerator ReopenSteamLobby(StartOfRound startOfRound)
        {
            // Nothing to do at all if this is not the host.
            if (!startOfRound.IsServer)
                yield break;
            // Ensure the lobby does not get opened until after any "getting fired" cutscene.
            yield return new WaitUntil(() => !startOfRound.firingPlayersCutsceneRunning);
            
            ShipLobby.Log.LogDebug("Reopening host lobby.");
            GameNetworkManager manager = GameNetworkManager.Instance;
            if (manager.currentLobby != null)
                manager.SetLobbyJoinable(true);
            
            // var lobbyTask = SteamMatchmaking.CreateLobbyAsync(4);
            // Plugin.Log.LogDebug("Requested lobby creation from steam, waiting...");
            // yield return new WaitUntil(() => lobbyTask.IsCompleted);
            // Plugin.Log.LogDebug($"Lobby creation wait complete. Result: {lobbyTask.Result}");
            // if (lobbyTask.Result == null)
            // {
            //     Plugin.Log.LogDebug("Failed to create Steam lobby!");
            //     yield break;
            // }
            //
            // manager.gameHasStarted = false;
        }
    }
}
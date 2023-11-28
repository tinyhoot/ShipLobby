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
        private static QuickMenuManager _quickMenuManager;
        
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
            // Only override refusals that are due to the current game state being set to "has already started".
            if (response.Approved || response.Reason != "Game has already started!")
                return;
            
            if (__instance.gameHasStarted && StartOfRound.Instance.inShipPhase)
            {
                ShipLobby.Log.LogDebug("Approving incoming late connection.");
                response.Reason = "";
                response.Approved = true;
            }
        }

        /// <summary>
        /// Make the friend invite button work again once we are back in orbit.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(QuickMenuManager), nameof(QuickMenuManager.InviteFriendsButton))]
        private static void FixFriendInviteButton()
        {
            // Only do this if the game isn't doing it by itself already.
            if (GameNetworkManager.Instance.gameHasStarted)
                GameNetworkManager.Instance.InviteFriendsUI();
        }

        /// <summary>
        /// Prevent leaving the lobby on starting the first game.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.LeaveLobbyAtGameStart))]
        private static bool PreventSteamLobbyLeaving(GameNetworkManager __instance)
        {
            ShipLobby.Log.LogDebug("Preventing the closing of Steam lobby.");
            // Do not run the method that would usually close down the lobby.
            return false;
        }
        
        /// <summary>
        /// Temporarily close the lobby while a game is ongoing. This prevents people trying to join mid-game.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.StartGame))]
        private static void CloseSteamLobby(StartOfRound __instance)
        {
            if (__instance.IsServer && __instance.inShipPhase)
            {
                ShipLobby.Log.LogDebug("Setting lobby to not joinable.");
                GameNetworkManager.Instance.SetLobbyJoinable(false);
            }
        }

        /// <summary>
        /// Reopen the steam lobby after a game has ended.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.EndOfGame))]
        private static IEnumerator ReopenSteamLobby(IEnumerator coroutine, StartOfRound __instance)
        {
            // The method we're patching here is a coroutine. Fully exhaust it before adding our code.
            while (coroutine.MoveNext())
                yield return coroutine.Current;
            // At this point all players should have been revived and the stats screen should have been shown.
            
            // Nothing to do at all if this is not the host.
            if (!__instance.IsServer)
                yield break;
            
            // The "getting fired" cutscene runs in a separate coroutine. Ensure we don't open the lobby until after
            // it is over.
            yield return new WaitUntil(() => !__instance.firingPlayersCutsceneRunning);
            
            ShipLobby.Log.LogDebug("Reopening lobby, setting to joinable.");
            GameNetworkManager manager = GameNetworkManager.Instance;
            if (manager.currentLobby == null)
                yield break;
            manager.SetLobbyJoinable(true);
            
            // Restore the friend invite button in the ESC menu.
            if (_quickMenuManager == null)
                _quickMenuManager = Object.FindObjectOfType<QuickMenuManager>();
            _quickMenuManager.inviteFriendsTextAlpha.alpha = 1f;
        }
    }
}
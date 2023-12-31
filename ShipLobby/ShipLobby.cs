﻿using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace ShipLobby
{
    [BepInPlugin(GUID, NAME, VERSION)]
    internal class ShipLobby : BaseUnityPlugin
    {
        public const string GUID = "com.github.tinyhoot.ShipLobby";
        public const string NAME = "ShipLobby";
        public const string VERSION = "1.0.2";
        
        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;

            Harmony harmony = new Harmony(GUID);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }
}
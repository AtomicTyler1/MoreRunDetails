using AsmResolver.DotNet.Serialized;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoreRunDetails
{
    [BepInAutoPlugin]
    public partial class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log { get; private set; } = null!;
        public static Plugin Instance { get; private set; } = null!;
        private Harmony harmony = null!;

        public static List<SegmentInfo> sectionTimes = new List<SegmentInfo>();

        public ConfigEntry<KeyCode> toggleKeybind;

        private void Awake()
        {
            Log = Logger;
            Instance = this;
            harmony = new Harmony(Id);
            harmony.PatchAll();
            Log.LogInfo($"Plugin {Name} is loaded!");

            toggleKeybind = Config.Bind("General", "Toggle Keybind", KeyCode.G, "Click this button to toggle the UI into view. (ONLY VISIBLE ON THE SCOUT REPORT)");
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKeybind.Value))
            {
                UICreator.ToggleUI();
            }
        }
    }

    public class SegmentInfo
    {
        public Segment segment;
        public float time;
        public float duration;
        public bool died;
    }

    [HarmonyPatch]
    public static partial class Patches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(EndScreen), nameof(EndScreen.Awake))]
        public static void EndScreenShown(EndScreen __instance)
        {
            int currentSegmentInt = MapHandler.Instance.currentSegment;
            Segment currentSegment = (Segment)currentSegmentInt;

            bool alreadyTracked = Plugin.sectionTimes.Any(s => s.segment == currentSegment);

            if (!alreadyTracked)
            {
                float currentTime = RunManager.Instance.timeSinceRunStarted;
                float totalPreviousDuration = Plugin.sectionTimes.Sum(s => s.duration);
                float duration = currentTime - totalPreviousDuration;

                Plugin.sectionTimes.Add(new SegmentInfo
                {
                    segment = currentSegment,
                    time = currentTime,
                    duration = duration,
                    died = !Character.localCharacter.refs.stats.won
                });

                Plugin.Log.LogInfo($"{currentSegment} tracking the stats.");
            }

            UICreator.CreateOrUpdate(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Campfire), nameof(Campfire.Light_Rpc))]
        public static void OnCampfireLight(Campfire __instance)
        {
            Segment nextSegment = __instance.advanceToSegment;
            Segment currentSegment = nextSegment - 1;

            float currentTime = RunManager.Instance.timeSinceRunStarted;

            bool alreadyTracked = Plugin.sectionTimes.Any(s => s.segment == currentSegment || s.segment == nextSegment);
            if (alreadyTracked) { return; }

            float totalPreviousDuration = Plugin.sectionTimes.Sum(s => s.duration);
            float duration = currentTime - totalPreviousDuration;

            Plugin.sectionTimes.Add(new SegmentInfo
            {
                segment = currentSegment,
                time = currentTime,
                duration = duration,
                died = false,
            });

            Plugin.Log.LogInfo($"{currentSegment} recorded: {duration}s (Total: {currentTime:F2}s)");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(RunManager), nameof(RunManager.StartRun))]
        public static void OnRunStart(RunManager __instance)
        {
            Plugin.sectionTimes.Clear();

            UICreator.ResetUI();

            if (!SceneManager.GetActiveScene().name.Contains("Level")) { return; }
            foreach (var segment in MapHandler.Instance.segments)
            {
                Plugin.Log.LogInfo($"Segment available: {segment.biome.ToString()}");
            }
        }
    }
}
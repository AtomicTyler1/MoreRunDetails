using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;
using Peak;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoreRunDetails
{
    [BepInAutoPlugin]
    [BepInDependency("com.snosz.terrainrandomiser", BepInDependency.DependencyFlags.SoftDependency)]
    public partial class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log { get; private set; } = null!;
        public static Plugin Instance { get; private set; } = null!;
        private Harmony harmony = null!;

        public static List<SegmentInfo> sectionTimes = new List<SegmentInfo>();

        public static ConfigEntry<KeyCode> toggleKeybind;
        public static ConfigEntry<bool> showDayAndLevel;
        public static ConfigEntry<bool> showCurrentAscent;
        public static ConfigEntry<bool> autoOpen;
        public static ConfigEntry<bool> terrainRandomiserSeed;

        public static bool hasTerrainRandomiser = false;

        public static string ModSavePath => Path.Combine(Application.persistentDataPath, "betterscoutreport.json");

        private void Awake()
        {
            Log = Logger;
            Instance = this;
            harmony = new Harmony(Id);
            harmony.PatchAll();
            Log.LogInfo($"Plugin {Name} is loaded!");

            autoOpen = Config.Bind("General", "Auto Open UI", true, "Automatically open the UI when the end screen is shown.");
            toggleKeybind = Config.Bind("General", "Toggle Keybind", KeyCode.G, "Click this button to toggle the UI into view. (ONLY VISIBLE ON THE SCOUT REPORT)");
            showDayAndLevel = Config.Bind("General", "Show Day and Level", true, "Show the current Day and Level on the UI, this is shown next to the timeline title.");
            showCurrentAscent = Config.Bind("General", "Show Current Ascent", true, "Show the current Ascent on the UI, this is shown just below the scout report.");
            terrainRandomiserSeed = Config.Bind("General", "Show Terrain Randomiser Seed", true, "Show the Terrain Randomiser Seed on the UI, if Terrain Randomiser is installed.");

            hasTerrainRandomiser = Chainloader.PluginInfos.ContainsKey("com.snosz.terrainrandomiser");
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
        public static void TrackSegment(Segment currentSegment, bool dead = false)
        {
            float currentTime = RunManager.Instance.timeSinceRunStarted;

            bool alreadyTracked = Plugin.sectionTimes.Any(s => s.segment == currentSegment);
            if (alreadyTracked) { return; }

            float totalPreviousDuration = Plugin.sectionTimes.Sum(s => s.duration);
            float duration = currentTime - totalPreviousDuration;

            if (RunSettings.isMiniRun) { return; }

            Plugin.sectionTimes.Add(new SegmentInfo
            {
                segment = currentSegment,
                time = currentTime,
                duration = duration,
                died = dead,
            });

            Plugin.Log.LogInfo($"{currentSegment} recorded: {duration}s (Total: {currentTime:F2}s)");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(EndScreen), nameof(EndScreen.Awake))]
        public static void EndScreenShown(EndScreen __instance)
        {
            int currentSegmentInt = MapHandler.Instance.currentSegment;
            Segment currentSegment = (Segment)currentSegmentInt;

            bool alreadyTracked = Plugin.sectionTimes.Any(s => s.segment == currentSegment);
            if (!alreadyTracked)
            {
                bool dead = Player.localPlayer.character.data.dead;
                TrackSegment(currentSegment, dead);
            }

            UICreator.CreateOrUpdate(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GUIManager), nameof(GUIManager.SetHeroTitle))]
        public static void HeroTitleShown(GUIManager __instance, string text)
        {
            if (text.ToLower() == "peak")
            {
                var mapHandler = MapHandler.Instance;
                Segment currentSegment = mapHandler.GetCurrentSegment();

                TrackSegment(currentSegment);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Action_WarpToBiome), nameof(Action_WarpToBiome.RunAction))]
        public static void TeleportToNadir()
        {
            var mapHandler = MapHandler.Instance;
            Segment currentSegment = mapHandler.GetCurrentSegment();

            TrackSegment(currentSegment);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PeakGatePortal), nameof(PeakGatePortal.Interact_CastFinished))]
        public static void PEAKGatePoralTrack(PeakGatePortal __instance, Character interactor)
        {
            var mapHandler = MapHandler.Instance;
            Segment currentSegment = mapHandler.GetCurrentSegment();

            TrackSegment(currentSegment);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Campfire), nameof(Campfire.Light_Rpc))]
        public static void OnCampfireLight(Campfire __instance)
        {
            Segment nextSegment = __instance.advanceToSegment;
            Segment currentSegment = nextSegment - 1;

            TrackSegment(currentSegment);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(RunManager), nameof(RunManager.StartRun))]
        public static void OnRunStart(RunManager __instance)
        {
            try
            {
                if (Quicksave.ShouldUseSaveData)
                {
                    if (File.Exists(Plugin.ModSavePath))
                    {
                        string json = File.ReadAllText(Plugin.ModSavePath);
                        var loadedTimes = JsonConvert.DeserializeObject<List<SegmentInfo>>(json);
                        if (loadedTimes != null && loadedTimes.Count > 0)
                        {
                            Plugin.sectionTimes = loadedTimes;
                            Plugin.Log.LogInfo($"Restored {loadedTimes.Count} saved segment(s):");
                            foreach (var info in Plugin.sectionTimes)
                            {
                                Plugin.Log.LogInfo($"  * {info.segment}: Duration = {info.duration:F2}s (Total Run Time = {info.time:F2}s)");
                            }
                        }
                        else
                        {
                            Plugin.Log.LogWarning($"Save file was found at '{Plugin.ModSavePath}' but contained no data.");
                            Plugin.sectionTimes.Clear();
                        }
                    }
                    else
                    {
                        Plugin.Log.LogWarning($"Loaded quicksave, but mod save file was missing at '{Plugin.ModSavePath}'. Starting with empty history.");
                        Plugin.sectionTimes.Clear();
                    }
                }
                else
                {
                    Plugin.Log.LogInfo("New run started. Cleared previous segment history.");
                    Plugin.sectionTimes.Clear();
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError("Error handling run start save data: " + e.Message);
                Plugin.sectionTimes.Clear();
            }

            UICreator.ResetUI();

            if (!SceneManager.GetActiveScene().name.Contains("Level")) { return; }
            foreach (var segment in MapHandler.Instance.segments)
            {
                Plugin.Log.LogInfo($"Segment available: {segment.biome.ToString()}");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Quicksave), nameof(Quicksave.SaveNow))]
        public static void OnQuicksaveSave()
        {
            try
            {
                string json = JsonConvert.SerializeObject(Plugin.sectionTimes);
                File.WriteAllText(Plugin.ModSavePath, json);
                Plugin.Log.LogInfo($"Wrote {Plugin.sectionTimes.Count} segment(s) to '{Plugin.ModSavePath}'.");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError("Failed to save BetterScoutReport data: " + e.Message);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Quicksave), nameof(Quicksave.DestroySaveData))]
        public static void OnQuicksaveDestroy()
        {
            try
            {
                if (File.Exists(Plugin.ModSavePath))
                {
                    File.Delete(Plugin.ModSavePath);
                    Plugin.Log.LogInfo("Deleted mod data file.");
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError("Failed to delete BetterScoutReport data: " + e.Message);
            }
        }
    }
}
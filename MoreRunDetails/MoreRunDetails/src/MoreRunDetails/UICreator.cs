using JetBrains.Annotations;
using Photon.Pun.Demo.PunBasics;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

namespace MoreRunDetails
{
    internal class UICreator
    {
        // Tracking lists for individual animation
        private static List<GameObject> uiParts = new List<GameObject>();
        private static Dictionary<GameObject, Vector3> originalPositions = new Dictionary<GameObject, Vector3>();

        private static bool isVisible = false;
        private static Coroutine animationRoutine;

        // Configuration
        private static float slideDistance = 700f;
        private static float lerpSpeed = 8f; // Lowered for a slower, smoother slide

        private static Vector3 baseOffset = new Vector3(425, 115, 0);
        private static Vector3 verticalPageOffset = new Vector3(0, -230, 0);
        private static Vector3 entrySpacing = new Vector3(0f, -25f, 0f);
        private static Vector3 entryStartOffset = new Vector3(0f, -35f, 0f);

        public static void ResetUI()
        {
            uiParts.Clear();
            originalPositions.Clear();
            isVisible = false;
        }

        public static void CreateOrUpdate(EndScreen __instance)
        {
            Plugin.Log.LogInfo("Creating custom end screen UI.");

            // FIX: Clear lists BEFORE tracking anything new
            uiParts.Clear();
            originalPositions.Clear();

            var HolePunch = __instance.transform.Find("Panel/HolePunch").gameObject;
            var BG = __instance.transform.Find("Panel/BG").gameObject;
            var SCOUTING_REPORT = __instance.transform.Find("Panel/Margin/SCOUTING_REPORT").gameObject;
            var panel = __instance.transform.Find("Panel");

            // Instantiate and Track the Master Hole Punch
            GameObject newHole = GameObject.Instantiate(HolePunch, HolePunch.transform.parent);
            newHole.name = "MoreDetailsHolePunch";
            newHole.transform.localPosition = new Vector3(221.4f, 0.1225f, 0f);
            newHole.transform.localScale = new Vector3(0.99f, 0.99f, 0.99f);
            TrackObject(newHole);

            GameObject keybindFrame = GameObject.Instantiate(BG, BG.transform.parent);
            keybindFrame.name = "MoreDetailsKeybindFrame";
            keybindFrame.transform.localPosition = new Vector3(256.6f, 0f, 0f);
            keybindFrame.transform.localScale = new Vector3(0.12f, 0.11f, 0.1f);

            GameObject keybindText = GameObject.Instantiate(SCOUTING_REPORT, keybindFrame.transform);
            keybindText.name = $"MoreDetailsKeybind";
            keybindText.transform.localPosition = Vector3.zero;
            keybindText.transform.localScale = Vector3.one;

            GameObject.Destroy(keybindText.GetComponent<LocalizedText>());
            
            TextMeshProUGUI tmp_keybind = keybindText.GetComponent<TextMeshProUGUI>();
            tmp_keybind.text = Plugin.Instance.toggleKeybind.Value.ToString();
            tmp_keybind.fontSizeMin = 250;
            tmp_keybind.fontSizeMax = 250;
            tmp_keybind.fontSize = 250;

            GameObject currentAscentObj = GameObject.Instantiate(SCOUTING_REPORT, SCOUTING_REPORT.transform.parent);
            currentAscentObj.name = "MoreDetailsAscent";
            currentAscentObj.transform.localPosition = SCOUTING_REPORT.transform.localPosition - new Vector3(0f, 15f, 0f);
            currentAscentObj.transform.localScale = SCOUTING_REPORT.transform.localScale;

            GameObject.Destroy(currentAscentObj.GetComponent<LocalizedText>());

            TextMeshProUGUI currentAscentText = currentAscentObj.GetComponent<TextMeshProUGUI>();

            var AscentDisplay = "PEAK (ASCENT 0)";
            if (Ascents.currentAscent < 0)
            {
                AscentDisplay = "TENDERFOOT";
            }
            else if (Ascents.currentAscent > 0)
            {
                AscentDisplay = $"ASCENT {Ascents.currentAscent}";
            }

            currentAscentText.text = AscentDisplay;
            currentAscentText.fontSizeMin = 16;
            currentAscentText.fontSizeMax = 16;
            currentAscentText.fontSize = 16;

            // Create Pages (these will call TrackObject internally)
            GameObject titleExited = CreatePage(__instance, "Exited", baseOffset, "CAMPFIRE", BG, panel, SCOUTING_REPORT);
            PopulateEntries(titleExited, useTotalTime: true);

            GameObject titleSpent = CreatePage(__instance, "Spent", baseOffset + verticalPageOffset, "TIME SPENT", BG, panel, SCOUTING_REPORT);
            PopulateEntries(titleSpent, useTotalTime: false);

            // Set initial state to hidden (offset to the right)
            foreach (var part in uiParts)
            {
                if (part != null)
                {
                    part.transform.localPosition = originalPositions[part] + new Vector3(slideDistance, 0, 0);
                }
            }
            isVisible = false;
        }

        public static void ToggleUI()
        {
            if (uiParts.Count == 0) return;

            isVisible = !isVisible;

            if (animationRoutine != null) Plugin.Instance.StopCoroutine(animationRoutine);
            animationRoutine = Plugin.Instance.StartCoroutine(TweenIndividualParts());
        }

        private static IEnumerator TweenIndividualParts()
        {
            float elapsed = 0;
            float duration = 1.0f; // Roughly 1 second based on lerp logic

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                foreach (var part in uiParts)
                {
                    if (part == null) continue;

                    Vector3 hiddenPos = originalPositions[part] + new Vector3(slideDistance, 0, 0);
                    Vector3 visiblePos = originalPositions[part];
                    Vector3 targetPos = isVisible ? visiblePos : hiddenPos;

                    // Smooth Lerp towards target
                    part.transform.localPosition = Vector3.Lerp(
                        part.transform.localPosition,
                        targetPos,
                        Time.deltaTime * lerpSpeed
                    );
                }
                yield return null;
            }

            // Final snap to target for precision
            foreach (var part in uiParts)
            {
                if (part == null) continue;
                part.transform.localPosition = isVisible ? originalPositions[part] : originalPositions[part] + new Vector3(slideDistance, 0, 0);
            }

            animationRoutine = null;
        }

        private static GameObject CreatePage(EndScreen __instance, string suffix, Vector3 offset, string title, GameObject BG, Transform panel, GameObject SCOUTING_REPORT)
        {
            Vector3 BGOffset = new Vector3(-105, 0, 0) + offset;

            // Background
            GameObject newBG = GameObject.Instantiate(BG, panel);
            newBG.name = $"MoreDetailsBG_{suffix}";
            newBG.transform.localPosition = BG.transform.localPosition + BGOffset;
            newBG.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            TrackObject(newBG);

            // Title
            GameObject newTitle = GameObject.Instantiate(SCOUTING_REPORT, panel);
            newTitle.name = $"MoreDetailsTitle_{suffix}";
            newTitle.transform.localPosition = SCOUTING_REPORT.transform.localPosition + offset + new Vector3(-95f, -90f, 0f);
            newTitle.transform.localScale = Vector3.one;
            TrackObject(newTitle);

            if (newTitle.TryGetComponent<LocalizedText>(out var loc)) GameObject.Destroy(loc);

            var txt = newTitle.GetComponent<TextMeshProUGUI>();
            txt.fontSizeMin = 32; txt.fontSizeMax = 32;
            txt.text = title;

            return newTitle;
        }

        private static void PopulateEntries(GameObject titleTemplate, bool useTotalTime)
        {
            var runSegments = MapHandler.Instance.segments;
            int localEntryCount = 0;

            foreach (var entry in Plugin.sectionTimes)
            {
                string displayName = entry.segment.ToString();
                if (entry.segment == Segment.Beach) displayName = "Shore";
                else if (entry.segment == Segment.Tropics && runSegments.Any(s => s.biome.ToString() == "Roots")) displayName = "Roots";
                else if (entry.segment == Segment.Alpine && runSegments.Any(s => s.biome.ToString() == "Mesa")) displayName = "Mesa";
                else if (entry.segment == Segment.TheKiln && runSegments.Any(s => s.biome.ToString() == "Volcano")) displayName = "The Kiln";

                float timeToDisplay = useTotalTime ? entry.time : entry.duration;
                TimeSpan t = TimeSpan.FromSeconds(timeToDisplay);
                string formattedTime = string.Format("{0:D1}:{1:D2}:{2:D2}", (int)t.TotalHours, t.Minutes, t.Seconds);

                GameObject row = GameObject.Instantiate(titleTemplate, titleTemplate.transform.parent);
                row.transform.localPosition = titleTemplate.transform.localPosition + entryStartOffset + (entrySpacing * localEntryCount);
                row.transform.localScale = Vector3.one;
                row.name = displayName;
                TrackObject(row);

                var rowText = row.GetComponent<TextMeshProUGUI>();
                rowText.color = new Color(rowText.color.r, rowText.color.g, rowText.color.b, 0.7f);
                rowText.fontSize = 20;
                rowText.fontSizeMin = 20;
                rowText.fontSizeMax = 20;
                rowText.text = entry.died ? $"<s>{displayName} [X]: {formattedTime}</s>" : $"{displayName}: {formattedTime}";

                localEntryCount++;
            }
        }

        private static void TrackObject(GameObject obj)
        {
            if (obj == null) return;
            uiParts.Add(obj);
            originalPositions[obj] = obj.transform.localPosition;
        }
    }
}
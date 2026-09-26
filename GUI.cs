using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace IsThisALoop
{
    public partial class Main
    {
        private Rect windowRect = new Rect(10, 10, 220, 260);

        private void OnGUI()
        {
            GUI.backgroundColor = Color.black;
            GUI.contentColor = Color.white;
            windowRect = GUI.Window(8844, windowRect, DrawGUI, "");
        }

        private void DrawGUI(int windowID)
        {
            GUILayout.Space(5);
            
            if (GUILayout.Button(isEnabled ? "Stop" : "Start"))
            {
                ToggleMod();
            }

            GUILayout.Space(5);
            
            if (GUILayout.Button(collisionEnabled ? "Collision: ON" : "Collision: OFF"))
            {
                collisionEnabled = !collisionEnabled;
            }

            GUILayout.Space(5);
            if (GUILayout.Button(agedClones ? "Aged Clones: ON" : "Aged Clones: OFF"))
            {
                agedClones = !agedClones;
            }

            GUILayout.Space(5);
            if (GUILayout.Button(infiniteClones ? "Infinite Clones: ON" : "Infinite Clones: OFF"))
            {
                infiniteClones = !infiniteClones;
            }

            GUILayout.Space(5);
            GUILayout.Label($"Interval: {interval.ToString("F1")}s");
            interval = GUILayout.HorizontalSlider(interval, 1f, 60f);

            GUILayout.Space(5);
            GUILayout.Label($"Max Clones: {maxClones}");
            maxClones = (int)GUILayout.HorizontalSlider(maxClones, 1f, 20f);

            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }
    }
}

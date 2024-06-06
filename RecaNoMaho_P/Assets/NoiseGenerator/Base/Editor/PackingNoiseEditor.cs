using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RecaNoMaho
{
    [CustomEditor(typeof(PackingNoise))]
    public class PackingNoiseEditor : Editor
    {
        private PackingNoise instance;

        private void OnEnable()
        {
            instance = target as PackingNoise;
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            
            GUILayout.Space(30);
            if (GUILayout.Button("Packing Textures(RGBA)", GUILayout.Height(30)))
            {
                instance.PackingTexturesRgba();
            }
            
            if (GUILayout.Button("Packing Textures(RGB)", GUILayout.Height(30)))
            {
                instance.PackingTexturesRgb();
            }
        }
    }
}


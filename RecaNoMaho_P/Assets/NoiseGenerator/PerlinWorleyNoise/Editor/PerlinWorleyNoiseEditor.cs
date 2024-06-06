using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace RecaNoMaho
{
    [CustomEditor(typeof(PerlinWorleyNoiseGenerator))]
    public class PerlinWorleyNoiseEditor : Editor
    {
        private PerlinWorleyNoiseGenerator instance;

        private void OnEnable()
        {
            instance = target as PerlinWorleyNoiseGenerator;
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            
            GUILayout.Space(30);
            if (GUILayout.Button("Generate", GUILayout.Height(30)))
            {
                instance.Generate();
            }

            GUILayout.Space(30);
            if (GUILayout.Button("SaveToDisk", GUILayout.Height(30)))
            {
                instance.SaveToDisk();
            }
        }
    }
}
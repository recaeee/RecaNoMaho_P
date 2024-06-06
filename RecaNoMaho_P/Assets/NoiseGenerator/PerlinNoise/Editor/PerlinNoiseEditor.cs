using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PerlinNoiseGenerator))]
public class PerlinNoiseEditor : Editor {
    private PerlinNoiseGenerator instance;

    private void OnEnable() {
        instance = target as PerlinNoiseGenerator;
    }

    public override void OnInspectorGUI() {
        base.OnInspectorGUI();

        GUILayout.Space(30);
        if (GUILayout.Button("Generate", GUILayout.Height(30))) {
            instance.Generate();
        }

        GUILayout.Space(30);
        if (GUILayout.Button("SaveToDisk", GUILayout.Height(30))) {
            instance.SaveToDisk();
        }
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PerlinNoise_Test_Evolution : MonoBehaviour {
    public float evolutionSpeed;
    public PerlinNoiseGenerator perlinNoiseGenerator;

    private void FixedUpdate() {
        float time = Time.realtimeSinceStartup;
        if (time % 3 < 1) {
            perlinNoiseGenerator.evolution.x += Time.fixedDeltaTime * evolutionSpeed * 1.0f;
            perlinNoiseGenerator.evolution.y += Time.fixedDeltaTime * evolutionSpeed * 0.75f;
            perlinNoiseGenerator.evolution.z += Time.fixedDeltaTime * evolutionSpeed * 0.5f;
        }
        else if (time % 3 < 2) {
            perlinNoiseGenerator.evolution.x += Time.fixedDeltaTime * evolutionSpeed * 0.75f;
            perlinNoiseGenerator.evolution.y += Time.fixedDeltaTime * evolutionSpeed * 1.0f;
            perlinNoiseGenerator.evolution.z += Time.fixedDeltaTime * evolutionSpeed * 0.5f;
        }
        else {
            perlinNoiseGenerator.evolution.x += Time.fixedDeltaTime * evolutionSpeed * 1.0f;
            perlinNoiseGenerator.evolution.y += Time.fixedDeltaTime * evolutionSpeed * 0.5f;
            perlinNoiseGenerator.evolution.z += Time.fixedDeltaTime * evolutionSpeed * 0.75f;
        }

        perlinNoiseGenerator.Generate();
    }
}
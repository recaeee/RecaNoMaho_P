using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorleyNoise_Test_Evolution : MonoBehaviour {
    public float evolutionSpeed;
    public WorleyNoiseGenerator worleyNoiseGenerator;

    private void FixedUpdate() {
        float time = Time.realtimeSinceStartup;
        if (time % 3 < 1) {
            worleyNoiseGenerator.evolution.x += Time.fixedDeltaTime * evolutionSpeed * 1.0f;
            worleyNoiseGenerator.evolution.y += Time.fixedDeltaTime * evolutionSpeed * 0.75f;
            worleyNoiseGenerator.evolution.z += Time.fixedDeltaTime * evolutionSpeed * 0.5f;
        }
        else if (time % 3 < 2) {
            worleyNoiseGenerator.evolution.x += Time.fixedDeltaTime * evolutionSpeed * 0.75f;
            worleyNoiseGenerator.evolution.y += Time.fixedDeltaTime * evolutionSpeed * 1.0f;
            worleyNoiseGenerator.evolution.z += Time.fixedDeltaTime * evolutionSpeed * 0.5f;
        }
        else {
            worleyNoiseGenerator.evolution.x += Time.fixedDeltaTime * evolutionSpeed * 1.0f;
            worleyNoiseGenerator.evolution.y += Time.fixedDeltaTime * evolutionSpeed * 0.5f;
            worleyNoiseGenerator.evolution.z += Time.fixedDeltaTime * evolutionSpeed * 0.75f;
        }

        worleyNoiseGenerator.Generate();
    }
}
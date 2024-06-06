using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RecaNoMaho
{
    public class PerlinWorleyNoiseGenerator : BaseNoise
    {
        public Texture basePerlinNoise = null;
        public Texture baseWorleyNoise = null;

        public override void Generate()
        {
            int kernal = cs_core.FindKernel("Main");
            cs_core.SetTexture(kernal, "_BasePerlinNoise", basePerlinNoise);
            cs_core.SetTexture(kernal,  "_BaseWorleyNoise", baseWorleyNoise);
            
            base.Generate();
        }
    }
}
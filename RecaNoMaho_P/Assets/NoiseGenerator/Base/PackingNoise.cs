using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RecaNoMaho
{
    public class PackingNoise : MonoBehaviour
    {
        public string savePath = "";
        public Texture textureA;
        public Texture textureB;
        public Texture textureC;
        public Texture textureD;

        public void PackingTexturesRgba()
        {
            ResourcesUtil.Packing4SingleChannelTextureToRgbaTexture(savePath, textureA, textureB, textureC, textureD);
        }
        
        public void PackingTexturesRgb()
        {
            ResourcesUtil.Packing3SingleChannelTextureToRgbTexture(savePath, textureA, textureB, textureC);
        }
    }
}
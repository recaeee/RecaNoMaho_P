using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RecaNoMaho
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Light))]
    public class LightVolumeRenderer : MonoBehaviour
    {
        [Header("体积光基础参数")]
        public bool stepOverride = false;
        [Tooltip("Ray Marching的步进次数")][Range(0, 64)] public int rayMarchingSteps = 8;

        [Tooltip("控制入射光线在经过介质时的衰减")][Range(0f, 1f)] public float inComingLoss = 0;
        [Tooltip("（仅对方向光源生效）")]public float dirLightDistance = 100;
        public bool extinctionOverride = false;
        [Tooltip("体积光的可见距离(影响介质透射率)")][Range(0.01f, 50f)]public float visibilityDistance = 50;
        [Tooltip("吸收系数（非严格按照公式）")] [Range(0, 1)] public float absorption = 0.1f;
        
        [Tooltip("控制光源强度的系数")][Range(0f, 2f)]public float intensityMultiplier = 1;

        [Header("风格化参数")]
        [Tooltip("体积光亮部强度")] [Range(0f, 10f)] public float brightIntensity = 1;
        [Tooltip("体积光暗部强度")] [Range(0f, 10f)] public float darkIntentsity = 1;

        public Light light { get; private set; }
        public Mesh volumeMesh { get; private set; }
        
        //存储前一帧的lightAngle和range，在其改动时更新Mesh
        private float previousAngle, previousRange;
        //Volume Bound Faces
        private List<Vector4> planes = new List<Vector4>(6);

        private bool isSpotLight => light.type == LightType.Spot;
        private bool isDirectionalLight => light.type == LightType.Directional;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireMesh(volumeMesh, 0, transform.position, transform.rotation, transform.lossyScale);
        }

        private void Awake()
        {
            light = GetComponent<Light>();
            volumeMesh = new Mesh();
            Reset();
            previousAngle = light.spotAngle;
            previousRange = light.range;
        }

        private void Update()
        {
            if (IsDirty())
            {
                UpdateMesh();
            }
        }

        public List<Vector4> GetVolumeBoundFaces(Camera camera)
        {
            planes.Clear();
            
            Matrix4x4 lightViewProjection = Matrix4x4.identity;
            if (isSpotLight)
            {
                //光源视角的VP矩阵
                lightViewProjection = Matrix4x4.Perspective(light.spotAngle, 1, 0.03f, light.range)
                                 * Matrix4x4.Scale(new Vector3(1, 1, -1))
                                 * light.transform.worldToLocalMatrix;
                var m0 = lightViewProjection.GetRow(0);
                var m1 = lightViewProjection.GetRow(1);
                var m2 = lightViewProjection.GetRow(2);
                var m3 = lightViewProjection.GetRow(3);
                
                planes.Add(-(m3 + m0));
                planes.Add(-(m3 - m0));
                planes.Add(-(m3 + m1));
                planes.Add(-(m3 - m1));
                // planes.Add( -(m3 + m2)); // ignore near
                planes.Add(-(m3 - m2));
            }
            else if (isDirectionalLight)
            {
                lightViewProjection = camera.projectionMatrix * camera.worldToCameraMatrix; // why camera?
                var m2 = lightViewProjection.GetRow(2);
                var m3 = lightViewProjection.GetRow(3);
                planes.Add(-(m3 + m2)); // near plane only
            }

            return planes;
        }

        private void Reset()
        {
            volumeMesh.vertices = new Vector3[]
            {
                new Vector3(-1, -1, -1),
                new Vector3(-1, 1, -1),
                new Vector3(1, 1, -1),
                new Vector3(1, -1, -1),
                new Vector3(-1, -1, 1),
                new Vector3(-1, 1, 1),
                new Vector3(1, 1, 1),
                new Vector3(1, -1, 1),
            };
            volumeMesh.triangles = new int[]
            {
                0, 1, 2, 0, 2, 3,
                0, 4, 5, 0, 5, 1,
                1, 5, 6, 1, 6, 2,
                2, 6, 7, 2, 7, 3,
                0, 3, 7, 0, 7, 4,
                4, 6, 5, 4, 7, 6,
            };
            volumeMesh.RecalculateNormals();
            UpdateMesh();
        }
        
        private void UpdateMesh()
        {
            if (isSpotLight)
            {
                var tanFov = Mathf.Tan(light.spotAngle / 2 * Mathf.Deg2Rad);
                //模型空间下Spot Light的有效锥体范围
                var verts = new Vector3[]
                {
                    new Vector3(0, 0, 0),
                    new Vector3(-tanFov, -tanFov, 1) * light.range,
                    new Vector3(-tanFov, tanFov, 1) * light.range,
                    new Vector3(tanFov, tanFov, 1) * light.range,
                    new Vector3(tanFov, -tanFov, 1) * light.range,
                };
                volumeMesh.Clear();
                volumeMesh.vertices = verts;
                volumeMesh.triangles = new int[]
                {
                    0, 1, 2,
                    0, 2, 3,
                    0, 3, 4,
                    0, 4, 1,
                    1, 4, 3,
                    1, 3, 2,
                };
                volumeMesh.RecalculateNormals();
                
                previousAngle = light.spotAngle;
                previousRange = light.range;
            }
        }

        private bool IsDirty()
        {
            return !Mathf.Approximately(light.spotAngle, previousAngle) ||
                   !Mathf.Approximately(light.range, previousRange);
        }
        
        public float GetExtinction()
        {
            return Mathf.Log(10f) / visibilityDistance;
        }
    }
}


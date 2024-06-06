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
        [Header("体积光质量")]
        public bool stepOverride = false;
        [Tooltip("Ray Marching的步进次数")][Range(0, 64)] public int rayMarchingSteps = 8;
        [Tooltip("是否采样阴影贴图")] public bool applyShadow = true;

        [Header("参与介质材质")]
        public bool mediaOverride = false;

        [Tooltip("反照率Albedo")] public Color albedo = Color.white;
        [Tooltip("消光系数Extinction")][Min(0.00001f)] public float extinction = 0.3f;
        [Tooltip("各向异性Phase g")] [Range(-1f, 1f)]
        public float phaseG = -0.5f;
        [Tooltip("自发光Emission")][ColorUsage(true, true)] public Color emission = Color.black;
        
        [Header("聚光灯参数")]
        [Tooltip("控制光源强度的系数")][Range(0f, 10f)]public float intensityMultiplier = 1;

        [Header("风格化参数")]
        [Tooltip("体积光暗部强度")] [Range(0f, 1f)] public float shadowIntensity = 1;

        public Light appliedLight { get; private set; }
        public Mesh volumeMesh { get; private set; }
        
        //存储前一帧的lightAngle和range，在其改动时更新Mesh
        private float previousAngle, previousRange;
        //Volume Bound Faces
        private List<Vector4> planes = new List<Vector4>(6);

        private bool isSpotLight => appliedLight.type == LightType.Spot;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireMesh(volumeMesh, 0, transform.position, transform.rotation, transform.lossyScale);
        }

        private void Awake()
        {
            appliedLight = GetComponent<Light>();
            volumeMesh = new Mesh();
            Reset();
            previousAngle = appliedLight.spotAngle;
            previousRange = appliedLight.range;
        }

        private void Update()
        {
            if (IsDirty())
            {
                UpdateMesh();
            }
        }
        
        public Vector4 GetScatteringExtinction()
        {
            // scattering = albedo * extinction = (scattering / extinction) * extinction
            return new Vector4(albedo.r * extinction, albedo.g * extinction, albedo.b * extinction, extinction);
        }

        public Vector4 GetEmissionPhaseG()
        {
            return new Vector4(emission.r, emission.g, emission.b, phaseG);
        }

        public List<Vector4> GetVolumeBoundFaces(Camera camera)
        {
            planes.Clear();
            
            Matrix4x4 lightViewProjection = Matrix4x4.identity;
            if (isSpotLight)
            {
                //光源视角的VP矩阵
                lightViewProjection = Matrix4x4.Perspective(appliedLight.spotAngle, 1, 0.03f, appliedLight.range)
                                 * Matrix4x4.Scale(new Vector3(1, 1, -1))
                                 * appliedLight.transform.worldToLocalMatrix;
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
                var tanFov = Mathf.Tan(appliedLight.spotAngle / 2 * Mathf.Deg2Rad);
                //模型空间下Spot Light的有效锥体范围
                var verts = new Vector3[]
                {
                    new Vector3(0, 0, 0),
                    new Vector3(-tanFov, -tanFov, 1) * appliedLight.range,
                    new Vector3(-tanFov, tanFov, 1) * appliedLight.range,
                    new Vector3(tanFov, tanFov, 1) * appliedLight.range,
                    new Vector3(tanFov, -tanFov, 1) * appliedLight.range,
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
                
                previousAngle = appliedLight.spotAngle;
                previousRange = appliedLight.range;
            }
        }

        private bool IsDirty()
        {
            return !Mathf.Approximately(appliedLight.spotAngle, previousAngle) ||
                   !Mathf.Approximately(appliedLight.range, previousRange);
        }
    }
}


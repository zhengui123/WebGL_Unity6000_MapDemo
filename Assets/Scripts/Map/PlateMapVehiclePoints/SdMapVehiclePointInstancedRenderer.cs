using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// GPU Instancing 绘制车辆点位（MaterialPropertyBlock 逐实例颜色，兼容 WebGL / 桌面）。
/// </summary>
[DisallowMultipleComponent]
[ExecuteAlways]
public class SdMapVehiclePointInstancedRenderer : MonoBehaviour
{
    private const int MaxInstancesPerDraw = 1023;

    [SerializeField] private Transform _mapRoot;
    [SerializeField] private Material _material;
    [SerializeField] private Mesh _mesh;
    [SerializeField] private Quaternion _localRotation = Quaternion.Euler(90f, 0f, 0f);
    [SerializeField] private Vector3 _baseLocalScale = new Vector3(0.01f, 0.01f, 0.01f);
    [SerializeField] private float _pointHeightOffset = 0.002f;
    [SerializeField, Range(0f, 5f)] private float _centerBrightness = 1f;

    private static readonly int InstanceColorAndGlowId = Shader.PropertyToID("_InstanceColorAndGlow");
    private static readonly int FallbackColorAndGlowId = Shader.PropertyToID("_FallbackColorAndGlow");
    private static readonly int CenterBrightnessId = Shader.PropertyToID("_CenterBrightness");

    private Matrix4x4[] _matrices;
    private CarPointGpuInstanceData[] _instanceData;
    private Matrix4x4[] _batchMatrices;
    private CarPointGpuInstanceData[] _batchData;
    private Vector4[] _batchInstanceVectors;
    private int _instanceCount;
    private Material _runtimeMaterial;
    private MaterialPropertyBlock _propertyBlock;

    public int InstanceCount => _instanceCount;

    public float CenterBrightness
    {
        get => _centerBrightness;
        set
        {
            _centerBrightness = Mathf.Clamp(value, 0f, 5f);
            ApplyCenterBrightnessToMaterial();
        }
    }

    public void SetCenterBrightness(float brightness)
    {
        CenterBrightness = brightness;
    }

    public void BindMapRoot(Transform mapRoot)
    {
        if (mapRoot != null)
        {
            _mapRoot = mapRoot;
        }
    }

    public void SyncTransformSettings(float heightOffset, Vector3 localScale)
    {
        _pointHeightOffset = heightOffset;
        _baseLocalScale = localScale;
    }

    private void OnDestroy()
    {
        if (_runtimeMaterial != null)
        {
            DestroyImmediate(_runtimeMaterial);
            _runtimeMaterial = null;
        }
    }

    private void LateUpdate()
    {
        DrawInstances();
    }

    public void SetInstances(
        IReadOnlyList<Matrix4x4> matrices,
        IReadOnlyList<CarPointGpuInstanceData> instanceData)
    {
        _instanceCount = 0;
        if (matrices == null || instanceData == null)
        {
            return;
        }

        int count = Mathf.Min(matrices.Count, instanceData.Count);
        if (count <= 0)
        {
            return;
        }

        EnsureCapacity(count);
        for (int i = 0; i < count; i++)
        {
            _matrices[i] = matrices[i];
            _instanceData[i] = instanceData[i];
        }

        _instanceCount = count;
    }

    public void ClearInstances()
    {
        _instanceCount = 0;
    }

    private void EnsureCapacity(int count)
    {
        if (_matrices == null || _matrices.Length < count)
        {
            _matrices = new Matrix4x4[count];
        }

        if (_instanceData == null || _instanceData.Length < count)
        {
            _instanceData = new CarPointGpuInstanceData[count];
        }
    }

    private void DrawInstances()
    {
        if (_instanceCount <= 0 || !EnsureDrawResources())
        {
            return;
        }

        if (!SystemInfo.supportsInstancing)
        {
            DrawInstancesFallback();
            return;
        }

        int offset = 0;
        while (offset < _instanceCount)
        {
            int batchCount = Mathf.Min(MaxInstancesPerDraw, _instanceCount - offset);
            DrawBatch(offset, batchCount);
            offset += batchCount;
        }
    }

    private void DrawBatch(int offset, int batchCount)
    {
        if (_batchMatrices == null || _batchMatrices.Length < batchCount)
        {
            _batchMatrices = new Matrix4x4[batchCount];
        }

        if (_batchData == null || _batchData.Length < batchCount)
        {
            _batchData = new CarPointGpuInstanceData[batchCount];
        }

        Array.Copy(_matrices, offset, _batchMatrices, 0, batchCount);
        Array.Copy(_instanceData, offset, _batchData, 0, batchCount);

        if (_batchInstanceVectors == null || _batchInstanceVectors.Length != batchCount)
        {
            _batchInstanceVectors = new Vector4[batchCount];
        }

        for (int i = 0; i < batchCount; i++)
        {
            _batchInstanceVectors[i] = _batchData[i].ToInstancingVector();
        }

        _propertyBlock ??= new MaterialPropertyBlock();
        _propertyBlock.Clear();
        _propertyBlock.SetVectorArray(InstanceColorAndGlowId, _batchInstanceVectors);

        Graphics.DrawMeshInstanced(
            _mesh,
            0,
            _runtimeMaterial,
            _batchMatrices,
            batchCount,
            _propertyBlock,
            ShadowCastingMode.Off,
            false,
            gameObject.layer,
            null,
            LightProbeUsage.Off);
    }

    /// <summary>不支持 Instancing 时的降级：逐实例 DrawMesh（WebGL1 等极少见环境）。</summary>
    private void DrawInstancesFallback()
    {
        _propertyBlock ??= new MaterialPropertyBlock();

        for (int i = 0; i < _instanceCount; i++)
        {
            _propertyBlock.Clear();
            _propertyBlock.SetVector(FallbackColorAndGlowId, _instanceData[i].ToInstancingVector());

            Graphics.DrawMesh(
                _mesh,
                _matrices[i],
                _runtimeMaterial,
                gameObject.layer,
                null,
                0,
                _propertyBlock,
                false,
                false);
        }
    }

    private bool EnsureDrawResources()
    {
        if (_mapRoot == null)
        {
            _mapRoot = transform;
        }

        if (_mesh == null)
        {
            _mesh = CreateQuadMesh();
        }

        if (_material == null)
        {
            _material = Resources.Load<Material>("CarPoint/M_CarPointGlowInstanced");
#if UNITY_EDITOR
            if (_material == null)
            {
                _material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/CarPoint/Materials/M_CarPointGlowInstanced.mat");
            }
#endif
        }

        if (_material == null || _mesh == null)
        {
            return false;
        }

        if (_runtimeMaterial == null)
        {
            _runtimeMaterial = Instantiate(_material);
            _runtimeMaterial.enableInstancing = true;
            if (_material.HasProperty(CenterBrightnessId))
            {
                _runtimeMaterial.SetFloat(CenterBrightnessId, _material.GetFloat(CenterBrightnessId));
            }
        }

        EnsureMainTextureBound();
        ApplyCenterBrightnessToMaterial();

        return true;
    }

    private void ApplyCenterBrightnessToMaterial()
    {
        if (_runtimeMaterial != null)
        {
            _runtimeMaterial.SetFloat(CenterBrightnessId, _centerBrightness);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _centerBrightness = Mathf.Max(0f, _centerBrightness);
        if (_runtimeMaterial != null)
        {
            ApplyCenterBrightnessToMaterial();
        }
    }
#endif

    private void EnsureMainTextureBound()
    {
        if (_runtimeMaterial == null || _runtimeMaterial.mainTexture != null)
        {
            return;
        }

        Material glowMat = Resources.Load<Material>("CarPoint/M_CarPointGlow");
#if UNITY_EDITOR
        if (glowMat == null)
        {
            glowMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/CarPoint/Materials/M_CarPointGlow.mat");
        }
#endif
        if (glowMat != null && glowMat.mainTexture != null)
        {
            _runtimeMaterial.mainTexture = glowMat.mainTexture;
            _runtimeMaterial.SetTexture("_MainTex", glowMat.mainTexture);
        }
    }

    private static Mesh CreateQuadMesh()
    {
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        DestroyImmediate(temp);
        return mesh;
    }

    public Matrix4x4 BuildInstanceMatrix(Vector3 localPositionOnMap, float scaleMultiplier = 1f)
    {
        if (_mapRoot == null)
        {
            return Matrix4x4.identity;
        }

        localPositionOnMap.y = _pointHeightOffset;
        Vector3 worldPos = _mapRoot.TransformPoint(localPositionOnMap);
        Quaternion worldRot = _mapRoot.rotation * _localRotation;
        float uniformScale = Mathf.Max(0.01f, scaleMultiplier);
        Vector3 worldScale = Vector3.Scale(_mapRoot.lossyScale, _baseLocalScale) * uniformScale;
        return Matrix4x4.TRS(worldPos, worldRot, worldScale);
    }
}

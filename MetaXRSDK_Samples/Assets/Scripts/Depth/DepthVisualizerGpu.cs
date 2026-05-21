using Meta.XR.BuildingBlocks.AIBlocks;
using UnityEngine;

// GPU-oriented point cloud visualizer.
// Prioritizes runtime performance by sampling the live environment depth texture directly in the shader.
// Best when the goal is to keep the cloud updating every frame with minimal CPU-side work.
[RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
public class DepthVisualizerGpu : MonoBehaviour
{
    protected enum DepthEyeSelection
    {
        Left = 0,
        Right = 1
    }

    private static readonly int PointAlphaId = Shader.PropertyToID("_PointAlpha");
    private static readonly int PointSizeId = Shader.PropertyToID("_PointSize");
    private static readonly int DepthRangeId = Shader.PropertyToID("_DepthRange");
    private static readonly int InverseLocalReprojectionId = Shader.PropertyToID("_InverseLocalReprojection");
    private static readonly int DepthEyeIndexId = Shader.PropertyToID("_DepthEyeIndex");

    [Header("References")]
    [SerializeField] private DepthTextureAccess _depthTextureAccess;
    [SerializeField] private Material _pointCloudMaterial;

    [Header("Rendering")]
    [SerializeField] private DepthEyeSelection _eyeSelection = DepthEyeSelection.Left;
    [SerializeField, Range(0f, 1f)] private float _pointAlpha = 0.9f;
    [SerializeField, Min(1f)] private float _pointSize = 2f;
    [SerializeField, Min(0f)] private float _minDepthMeters = 0.1f;
    [SerializeField, Min(0.01f)] private float _maxDepthMeters = 5f;

    private MeshRenderer _meshRenderer;
    private MeshFilter _meshFilter;
    private Material _runtimeMaterial;
    private Mesh _pointMesh;
    private int _meshTextureSize;

    // When the component is created, validate required references and prepare the renderer/material state.
    protected virtual void Awake()
    {
        if (_depthTextureAccess == null)
        {
            _depthTextureAccess = FindAnyObjectByType<DepthTextureAccess>();
        }

        if (_depthTextureAccess == null)
        {
            Debug.LogError("DepthTextureAccess reference is missing!", this);
            enabled = false;
            return;
        }

        if (_pointCloudMaterial == null)
        {
            Debug.LogError("Point cloud material reference is missing!", this);
            enabled = false;
            return;
        }

        _meshRenderer = GetComponent<MeshRenderer>();
        _meshFilter = GetComponent<MeshFilter>();

        _meshRenderer.enabled = false;
        _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _meshRenderer.receiveShadows = false;

        EnsureMaterial();
        ApplyMaterialProperties();
    }

    // When the component becomes enabled, start listening for CPU depth frame updates.
    protected virtual void OnEnable()
    {
        if (_depthTextureAccess != null)
        {
            _depthTextureAccess.OnDepthTextureUpdateCPU += HandleDepthTextureUpdate;
        }
    }

    // When the component becomes disabled, stop listening for CPU depth frame updates.
    protected virtual void OnDisable()
    {
        if (_depthTextureAccess != null)
        {
            _depthTextureAccess.OnDepthTextureUpdateCPU -= HandleDepthTextureUpdate;
        }
    }

    // When the component is destroyed, release subscriptions and runtime-generated resources.
    protected virtual void OnDestroy()
    {
        if (_depthTextureAccess != null)
        {
            _depthTextureAccess.OnDepthTextureUpdateCPU -= HandleDepthTextureUpdate;
        }

        if (_runtimeMaterial != null)
        {
            Destroy(_runtimeMaterial);
        }

        if (_pointMesh != null)
        {
            Destroy(_pointMesh);
        }
    }

    // When serialized values change in the editor, keep depth limits and render settings in a valid state.
    protected virtual void OnValidate()
    {
        if (_maxDepthMeters < _minDepthMeters)
        {
            _maxDepthMeters = _minDepthMeters;
        }

        ApplyMaterialProperties();
        DepthPointCloudMeshUtility.UpdateBounds(_pointMesh, _maxDepthMeters);
    }

    // When Unity updates the component each frame, request a fresh depth sample for live GPU rendering.
    protected virtual void Update()
    {
        if (_depthTextureAccess == null)
        {
            return;
        }

        _depthTextureAccess.RequestDepthSample();
    }

    // Receives the latest depth frame, prepares the reconstruction matrix for the selected eye,
    // and updates the material so the shader can draw the current point cloud.
    protected virtual void HandleDepthTextureUpdate(DepthTextureAccess.DepthFrameData depthFrameData)
    {
        if (_depthTextureAccess == null || depthFrameData.ViewProjectionMatrix == null || depthFrameData.ViewProjectionMatrix.Length == 0)
        {
            return;
        }

        var textureSize = _depthTextureAccess.TextureSize;
        if (textureSize <= 0)
        {
            return;
        }

        EnsureMaterial();
        if (_runtimeMaterial == null)
        {
            return;
        }

        if (_pointMesh == null || _meshTextureSize != textureSize)
        {
            _pointMesh = DepthPointCloudMeshUtility.BuildPointMesh(_pointMesh, textureSize);
            _meshTextureSize = textureSize;
            _meshFilter.sharedMesh = _pointMesh;
            DepthPointCloudMeshUtility.UpdateBounds(_pointMesh, _maxDepthMeters);
        }

        var eyeIndex = ResolveEyeIndex();
        eyeIndex = Mathf.Clamp(eyeIndex, 0, depthFrameData.ViewProjectionMatrix.Length - 1);

        // In this sample, point reconstruction is intentionally split into two steps:
        // 1. The shader uses the inverse of (ViewProjection * CameraPose) to reconstruct each point in camera-local space.
        // 2. This transform then uses CameraPose to place that local-space point cloud in world space as a whole.
        // If you do not need that separation, using ViewProjectionMatrix[eyeIndex].inverse directly would reconstruct
        // the points in world space immediately, and both this transform update and cameraLocalToWorld would be unnecessary.
        transform.SetPositionAndRotation(depthFrameData.CameraPose.position, depthFrameData.CameraPose.rotation);

        var cameraLocalToWorld = Matrix4x4.TRS(
            depthFrameData.CameraPose.position,
            depthFrameData.CameraPose.rotation,
            Vector3.one);
        var localToClip = depthFrameData.ViewProjectionMatrix[eyeIndex] * cameraLocalToWorld;
        var inverseLocalReprojection = localToClip.inverse;

        _runtimeMaterial.SetMatrix(InverseLocalReprojectionId, inverseLocalReprojection);
        _runtimeMaterial.SetFloat(DepthEyeIndexId, eyeIndex);
        ApplyMaterialProperties();

        _meshRenderer.enabled = true;
    }

    // Creates a runtime material instance from the inspector-assigned template so per-object values
    // can be changed without modifying the shared material asset.
    private void EnsureMaterial()
    {
        if (_runtimeMaterial != null)
        {
            return;
        }

        if (_pointCloudMaterial == null)
        {
            Debug.LogError("Point cloud material reference is missing!", this);
            return;
        }

        _runtimeMaterial = new Material(_pointCloudMaterial)
        {
            name = $"{GetType().Name} Runtime Material"
        };

        _meshRenderer.sharedMaterial = _runtimeMaterial;
    }

    // Pushes the current inspector-controlled rendering parameters into the runtime material.
    private void ApplyMaterialProperties()
    {
        if (_runtimeMaterial == null)
        {
            return;
        }

        _runtimeMaterial.SetFloat(PointAlphaId, _pointAlpha);
        _runtimeMaterial.SetFloat(PointSizeId, _pointSize);
        _runtimeMaterial.SetVector(DepthRangeId, new Vector4(_minDepthMeters, _maxDepthMeters, 0f, 0f));
    }

    // Converts the serialized left/right choice into the corresponding eye slice index.
    private int ResolveEyeIndex()
    {
        return _eyeSelection == DepthEyeSelection.Right ? 1 : 0;
    }
}

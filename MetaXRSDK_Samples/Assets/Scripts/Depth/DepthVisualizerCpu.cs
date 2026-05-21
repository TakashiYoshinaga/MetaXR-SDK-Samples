using Meta.XR.BuildingBlocks.AIBlocks;
using UnityEngine;
using UnityEngine.Rendering;

// CPU-oriented point cloud visualizer.
// Prioritizes control over when depth data is captured by caching DepthTexturePixels on the CPU first.
// Best when the goal is to freeze, pause, or otherwise control update timing even if it costs more than the GPU path.
[RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
public class DepthVisualizerCpu : MonoBehaviour
{
    private enum DepthEyeSelection
    {
        Left = 0,
        Right = 1
    }

    private static readonly int PointAlphaId = Shader.PropertyToID("_PointAlpha");
    private static readonly int PointSizeId = Shader.PropertyToID("_PointSize");
    private static readonly int DepthRangeId = Shader.PropertyToID("_DepthRange");
    private static readonly int InverseLocalReprojectionId = Shader.PropertyToID("_InverseLocalReprojection");
    private static readonly int LinearDepthBufferId = Shader.PropertyToID("_LinearDepthBuffer");

    [Header("References")]
    [SerializeField] private DepthTextureAccess _depthTextureAccess;
    [SerializeField] private Material _pointCloudMaterial;

    [Header("Rendering")]
    [SerializeField] private DepthEyeSelection _eyeSelection = DepthEyeSelection.Left;
    [SerializeField, Range(0f, 1f)] private float _pointAlpha = 0.9f;
    [SerializeField, Min(1f)] private float _pointSize = 2f;
    [SerializeField, Min(0f)] private float _minDepthMeters = 0.1f;
    [SerializeField, Min(0.01f)] private float _maxDepthMeters = 5f;
    [Header("Optional")]
    [SerializeField] private bool _canFreezeUpdateByController = false;
    private MeshRenderer _meshRenderer;
    private MeshFilter _meshFilter;
    private Material _runtimeMaterial;
    private Mesh _pointMesh;
    private ComputeBuffer _linearDepthBuffer;
    private float[] _eyeDepthScratch;
    private int _meshTextureSize;
    private bool _isFrozen = false;

    // When the component is created, validate required references and prepare the renderer/material state.
    private void Awake()
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
        _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _meshRenderer.receiveShadows = false;

        EnsureMaterial();
        ApplyMaterialProperties();
    }

    // When the component becomes enabled, start listening for CPU depth frame updates.
    private void OnEnable()
    {
        if (_depthTextureAccess != null)
        {
            _depthTextureAccess.OnDepthTextureUpdateCPU += HandleDepthTextureUpdate;
        }
    }

    // When the component becomes disabled, stop listening for CPU depth frame updates.
    private void OnDisable()
    {
        if (_depthTextureAccess != null)
        {
            _depthTextureAccess.OnDepthTextureUpdateCPU -= HandleDepthTextureUpdate;
        }
    }

    // When the component is destroyed, release subscriptions and runtime-generated resources.
    private void OnDestroy()
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

        _linearDepthBuffer?.Dispose();
    }

    // When serialized values change in the editor, keep depth limits and render settings in a valid state.
    private void OnValidate()
    {
        if (_maxDepthMeters < _minDepthMeters)
        {
            _maxDepthMeters = _minDepthMeters;
        }

        ApplyMaterialProperties();
        DepthPointCloudMeshUtility.UpdateBounds(_pointMesh, _maxDepthMeters);
    }

    // When Unity updates the component each frame, optionally toggle freezing and request a new CPU depth sample.
    private void Update()
    {
        if (_depthTextureAccess == null)
        {
            return;
        }
        if (_canFreezeUpdateByController && OVRInput.GetDown(OVRInput.RawButton.A))
        {
            _isFrozen = !_isFrozen;
        }
    
        if (_isFrozen)
        {
            return;
        }
        _depthTextureAccess.RequestDepthSample();
    }

    // Receives the latest CPU depth frame, uploads the selected eye's depth values into a buffer,
    // and updates the reconstruction matrix used by the shader to draw the current point cloud.
    private void HandleDepthTextureUpdate(DepthTextureAccess.DepthFrameData depthFrameData)
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
            EnsureDepthBuffer(textureSize * textureSize);
        }

        if (!depthFrameData.DepthTexturePixels.IsCreated || depthFrameData.DepthTexturePixels.Length < textureSize * textureSize * 2)
        {
            return;
        }

        var eyeIndex = ResolveEyeIndex();
        eyeIndex = Mathf.Clamp(eyeIndex, 0, depthFrameData.ViewProjectionMatrix.Length - 1);

        // CPU mode stores one eye's linear depth buffer explicitly, so updates can be paused or replaced
        // independently from the live environment depth texture. The reconstruction is still split the same way:
        // the shader rebuilds camera-local points from the stored depth, then CameraPose places that cloud in world space.
        transform.SetPositionAndRotation(depthFrameData.CameraPose.position, depthFrameData.CameraPose.rotation);

        var cameraLocalToWorld = Matrix4x4.TRS(
            depthFrameData.CameraPose.position,
            depthFrameData.CameraPose.rotation,
            Vector3.one);
        var localToClip = depthFrameData.ViewProjectionMatrix[eyeIndex] * cameraLocalToWorld;
        var inverseLocalReprojection = localToClip.inverse;

        UpdateLinearDepthBuffer(depthFrameData, textureSize, eyeIndex);

        _runtimeMaterial.SetMatrix(InverseLocalReprojectionId, inverseLocalReprojection);
        _runtimeMaterial.SetBuffer(LinearDepthBufferId, _linearDepthBuffer);
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
            name = $"{nameof(DepthVisualizerCpu)} Runtime Material"
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

    // Ensures the GPU buffer used by the CPU path exists at the current point count.
    private void EnsureDepthBuffer(int pointCount)
    {
        if (_linearDepthBuffer != null && _linearDepthBuffer.count == pointCount)
        {
            return;
        }

        _linearDepthBuffer?.Dispose();
        _linearDepthBuffer = new ComputeBuffer(pointCount, sizeof(float), ComputeBufferType.Structured);
        _eyeDepthScratch = new float[pointCount];
    }

    // Copies one eye's packed linear depth values out of DepthTexturePixels and uploads them to the GPU buffer.
    private void UpdateLinearDepthBuffer(DepthTextureAccess.DepthFrameData depthFrameData, int textureSize, int eyeIndex)
    {
        var pointCount = textureSize * textureSize;
        if (_linearDepthBuffer == null || _linearDepthBuffer.count != pointCount)
        {
            EnsureDepthBuffer(pointCount);
        }

        if (_eyeDepthScratch == null || _eyeDepthScratch.Length != pointCount)
        {
            _eyeDepthScratch = new float[pointCount];
        }

        var sourceOffset = eyeIndex * pointCount;
        for (var i = 0; i < pointCount; i++)
        {
            _eyeDepthScratch[i] = depthFrameData.DepthTexturePixels[sourceOffset + i];
        }

        _linearDepthBuffer.SetData(_eyeDepthScratch);
    }

    // Converts the serialized left/right choice into the corresponding eye slice index.
    private int ResolveEyeIndex()
    {
        return _eyeSelection == DepthEyeSelection.Right ? 1 : 0;
    }
}

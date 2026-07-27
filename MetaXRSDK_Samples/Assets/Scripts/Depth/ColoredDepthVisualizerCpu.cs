using Meta.XR;
using Meta.XR.BuildingBlocks.AIBlocks;
using UnityEngine;
using UnityEngine.Rendering;

// ScanApp-specific CPU depth visualizer that projects the passthrough camera image onto the point cloud.
// It intentionally remains separate from DepthVisualizerCpu so the shared depth samples keep their behavior.
[RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
public class ColoredDepthVisualizerCpu : MonoBehaviour
{
    private static readonly int PointAlphaId = Shader.PropertyToID("_PointAlpha");
    private static readonly int PointSizeId = Shader.PropertyToID("_PointSize");
    private static readonly int DepthRangeId = Shader.PropertyToID("_DepthRange");
    private static readonly int InverseLocalReprojectionId = Shader.PropertyToID("_InverseLocalReprojection");
    private static readonly int LinearDepthBufferId = Shader.PropertyToID("_LinearDepthBuffer");
    private static readonly int CameraTextureId = Shader.PropertyToID("_CameraTexture");
    private static readonly int ColorCameraWorldToLocalId = Shader.PropertyToID("_ColorCameraWorldToLocal");
    private static readonly int ColorFocalLengthId = Shader.PropertyToID("_ColorFocalLength");
    private static readonly int ColorPrincipalPointId = Shader.PropertyToID("_ColorPrincipalPoint");
    private static readonly int ColorSensorCropRectId = Shader.PropertyToID("_ColorSensorCropRect");

    [Header("References")]
    [SerializeField] private DepthTextureAccess _depthTextureAccess;
    [SerializeField] private PassthroughCameraAccess _passthroughCameraAccess;
    [SerializeField] private Material _pointCloudMaterial;

    [Header("Rendering")]
    [SerializeField, Range(0f, 1f)] private float _pointAlpha = 0.9f;
    [SerializeField, Min(1f)] private float _pointSize = 2f;
    [SerializeField, Min(0f)] private float _minDepthMeters = 0.1f;
    [SerializeField, Min(0.01f)] private float _maxDepthMeters = 5f;

    [Header("Optional")]
    [SerializeField] private bool _canFreezeUpdateByController = true;
    [SerializeField] private OVRInput.RawButton _freezeButton = OVRInput.RawButton.A;

    private MeshRenderer _meshRenderer;
    private MeshFilter _meshFilter;
    private Material _runtimeMaterial;
    private Mesh _pointMesh;
    private ComputeBuffer _linearDepthBuffer;
    private RenderTexture _frozenCameraTexture;
    private float[] _eyeDepthScratch;
    private int _meshTextureSize;
    private bool _hasValidCameraFrame;
    private bool _hasRenderedDepthFrame;
    private bool _isFrozen;

    private void Awake()
    {
        if (_depthTextureAccess == null)
        {
            _depthTextureAccess = FindAnyObjectByType<DepthTextureAccess>();
        }

        if (_passthroughCameraAccess == null)
        {
            _passthroughCameraAccess = FindAnyObjectByType<PassthroughCameraAccess>();
        }

        if (_depthTextureAccess == null || _passthroughCameraAccess == null || _pointCloudMaterial == null)
        {
            Debug.LogError(
                $"{nameof(ColoredDepthVisualizerCpu)} requires DepthTextureAccess, PassthroughCameraAccess, and a point cloud material.",
                this);
            enabled = false;
            return;
        }

        _meshRenderer = GetComponent<MeshRenderer>();
        _meshFilter = GetComponent<MeshFilter>();

        _meshRenderer.enabled = false;
        _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _meshRenderer.receiveShadows = false;

        EnsureMaterial();
        ApplyRenderingProperties();
    }

    private void OnEnable()
    {
        if (_depthTextureAccess != null)
        {
            _depthTextureAccess.OnDepthTextureUpdateCPU += HandleDepthTextureUpdate;
        }
    }

    private void OnDisable()
    {
        if (_depthTextureAccess != null)
        {
            _depthTextureAccess.OnDepthTextureUpdateCPU -= HandleDepthTextureUpdate;
        }

        if (_meshRenderer != null)
        {
            _meshRenderer.enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (_depthTextureAccess != null)
        {
            _depthTextureAccess.OnDepthTextureUpdateCPU -= HandleDepthTextureUpdate;
        }

        _linearDepthBuffer?.Dispose();
        ReleaseFrozenCameraTexture();

        if (_runtimeMaterial != null)
        {
            Destroy(_runtimeMaterial);
        }

        if (_pointMesh != null)
        {
            Destroy(_pointMesh);
        }
    }

    private void OnValidate()
    {
        if (_maxDepthMeters < _minDepthMeters)
        {
            _maxDepthMeters = _minDepthMeters;
        }

        ApplyRenderingProperties();
        DepthPointCloudMeshUtility.UpdateBounds(_pointMesh, _maxDepthMeters);
    }

    private void Update()
    {
        if (_depthTextureAccess == null || _passthroughCameraAccess == null)
        {
            return;
        }

        if (_canFreezeUpdateByController && OVRInput.GetDown(_freezeButton))
        {
            if (_isFrozen)
            {
                ResumeLiveUpdates();
            }
            else
            {
                TryFreezeCurrentFrame();
            }
        }

        if (_isFrozen)
        {
            return;
        }

        if (!TryApplyLiveCameraFrame())
        {
            _hasValidCameraFrame = false;
            _meshRenderer.enabled = false;
            return;
        }

        _hasValidCameraFrame = true;
        _depthTextureAccess.RequestDepthSample();
    }

    private bool TryApplyLiveCameraFrame()
    {
        if (!_passthroughCameraAccess.IsPlaying)
        {
            return false;
        }

        var cameraTexture = _passthroughCameraAccess.GetTexture();
        if (!TryApplyCameraProjection(cameraTexture, _passthroughCameraAccess.GetCameraPose()))
        {
            return false;
        }

        _runtimeMaterial.SetTexture(CameraTextureId, cameraTexture);
        return true;
    }

    private bool TryApplyCameraProjection(Texture cameraTexture, Pose cameraPose)
    {
        if (_runtimeMaterial == null || cameraTexture == null)
        {
            return false;
        }

        var intrinsics = _passthroughCameraAccess.Intrinsics;
        var sensorResolution = intrinsics.SensorResolution;
        var imageResolution = _passthroughCameraAccess.CurrentResolution;
        if (sensorResolution.x <= 0 || sensorResolution.y <= 0 ||
            imageResolution.x <= 0 || imageResolution.y <= 0 ||
            intrinsics.FocalLength.x <= 0f || intrinsics.FocalLength.y <= 0f)
        {
            return false;
        }

        var scaleFactor = new Vector2(
            imageResolution.x / (float)sensorResolution.x,
            imageResolution.y / (float)sensorResolution.y);
        scaleFactor /= Mathf.Max(scaleFactor.x, scaleFactor.y);

        var cropSize = Vector2.Scale((Vector2)sensorResolution, scaleFactor);
        var cropOrigin = ((Vector2)sensorResolution - cropSize) * 0.5f;
        var worldToCamera = Matrix4x4.TRS(cameraPose.position, cameraPose.rotation, Vector3.one).inverse;

        _runtimeMaterial.SetMatrix(ColorCameraWorldToLocalId, worldToCamera);
        _runtimeMaterial.SetVector(ColorFocalLengthId, intrinsics.FocalLength);
        _runtimeMaterial.SetVector(ColorPrincipalPointId, intrinsics.PrincipalPoint);
        _runtimeMaterial.SetVector(
            ColorSensorCropRectId,
            new Vector4(cropOrigin.x, cropOrigin.y, cropSize.x, cropSize.y));

        return true;
    }

    private void TryFreezeCurrentFrame()
    {
        if (!_hasRenderedDepthFrame || !_passthroughCameraAccess.IsPlaying)
        {
            return;
        }

        var cameraTexture = _passthroughCameraAccess.GetTexture();
        var cameraPose = _passthroughCameraAccess.GetCameraPose();
        if (!TryApplyCameraProjection(cameraTexture, cameraPose))
        {
            return;
        }

        EnsureFrozenCameraTexture(cameraTexture);
        if (_frozenCameraTexture == null)
        {
            return;
        }

        Graphics.Blit(cameraTexture, _frozenCameraTexture);
        _runtimeMaterial.SetTexture(CameraTextureId, _frozenCameraTexture);
        _isFrozen = true;
    }

    private void ResumeLiveUpdates()
    {
        _isFrozen = false;
        _hasValidCameraFrame = false;
    }

    private void HandleDepthTextureUpdate(DepthTextureAccess.DepthFrameData depthFrameData)
    {
        if (_isFrozen || !_hasValidCameraFrame || _depthTextureAccess == null ||
            depthFrameData.ViewProjectionMatrix == null || depthFrameData.ViewProjectionMatrix.Length == 0)
        {
            return;
        }

        var textureSize = _depthTextureAccess.TextureSize;
        if (textureSize <= 0 || !depthFrameData.DepthTexturePixels.IsCreated)
        {
            return;
        }

        var pointCount = textureSize * textureSize;
        if (depthFrameData.DepthTexturePixels.Length < pointCount * 2)
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
            EnsureDepthBuffer(pointCount);
        }

        var eyeIndex = ResolveDepthEyeIndex();
        eyeIndex = Mathf.Clamp(eyeIndex, 0, depthFrameData.ViewProjectionMatrix.Length - 1);

        transform.SetPositionAndRotation(depthFrameData.CameraPose.position, depthFrameData.CameraPose.rotation);

        var cameraLocalToWorld = Matrix4x4.TRS(
            depthFrameData.CameraPose.position,
            depthFrameData.CameraPose.rotation,
            Vector3.one);
        var localToClip = depthFrameData.ViewProjectionMatrix[eyeIndex] * cameraLocalToWorld;

        UpdateLinearDepthBuffer(depthFrameData, pointCount, eyeIndex);

        _runtimeMaterial.SetMatrix(InverseLocalReprojectionId, localToClip.inverse);
        _runtimeMaterial.SetBuffer(LinearDepthBufferId, _linearDepthBuffer);
        ApplyRenderingProperties();

        _hasRenderedDepthFrame = true;
        _meshRenderer.enabled = true;
    }

    private void EnsureMaterial()
    {
        if (_runtimeMaterial != null || _pointCloudMaterial == null || _meshRenderer == null)
        {
            return;
        }

        _runtimeMaterial = new Material(_pointCloudMaterial)
        {
            name = $"{nameof(ColoredDepthVisualizerCpu)} Runtime Material"
        };
        _meshRenderer.sharedMaterial = _runtimeMaterial;
    }

    private void ApplyRenderingProperties()
    {
        if (_runtimeMaterial == null)
        {
            return;
        }

        _runtimeMaterial.SetFloat(PointAlphaId, _pointAlpha);
        _runtimeMaterial.SetFloat(PointSizeId, _pointSize);
        _runtimeMaterial.SetVector(DepthRangeId, new Vector4(_minDepthMeters, _maxDepthMeters, 0f, 0f));
    }

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

    private void UpdateLinearDepthBuffer(
        DepthTextureAccess.DepthFrameData depthFrameData,
        int pointCount,
        int eyeIndex)
    {
        EnsureDepthBuffer(pointCount);

        var sourceOffset = eyeIndex * pointCount;
        for (var i = 0; i < pointCount; i++)
        {
            _eyeDepthScratch[i] = depthFrameData.DepthTexturePixels[sourceOffset + i];
        }

        _linearDepthBuffer.SetData(_eyeDepthScratch);
    }

    private int ResolveDepthEyeIndex()
    {
        return _passthroughCameraAccess.CameraPosition == PassthroughCameraAccess.CameraPositionType.Right ? 1 : 0;
    }

    private void EnsureFrozenCameraTexture(Texture sourceTexture)
    {
        if (_frozenCameraTexture != null &&
            _frozenCameraTexture.width == sourceTexture.width &&
            _frozenCameraTexture.height == sourceTexture.height)
        {
            return;
        }

        ReleaseFrozenCameraTexture();
        _frozenCameraTexture = new RenderTexture(
            sourceTexture.width,
            sourceTexture.height,
            0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Default)
        {
            name = $"{nameof(ColoredDepthVisualizerCpu)} Frozen Camera",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            autoGenerateMips = false
        };
        _frozenCameraTexture.Create();
    }

    private void ReleaseFrozenCameraTexture()
    {
        if (_frozenCameraTexture == null)
        {
            return;
        }

        if (_frozenCameraTexture.IsCreated())
        {
            _frozenCameraTexture.Release();
        }

        Destroy(_frozenCameraTexture);
        _frozenCameraTexture = null;
    }
}

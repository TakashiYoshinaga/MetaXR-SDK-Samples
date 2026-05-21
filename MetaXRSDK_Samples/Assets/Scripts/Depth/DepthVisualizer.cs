using Meta.XR.BuildingBlocks.AIBlocks;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
public class DepthVisualizer : MonoBehaviour
{
    private enum DepthEyeSelection
    {
        Left = 0,
        Right = 1
    }

    private static readonly int PointColorId = Shader.PropertyToID("_PointColor");
    private static readonly int PointSizeId = Shader.PropertyToID("_PointSize");
    private static readonly int DepthRangeId = Shader.PropertyToID("_DepthRange");
    private static readonly int InverseLocalReprojectionId = Shader.PropertyToID("_InverseLocalReprojection");
    private static readonly int DepthEyeIndexId = Shader.PropertyToID("_DepthEyeIndex");

    [Header("References")]
    [SerializeField] private DepthTextureAccess _depthTextureAccess;
    [SerializeField] private Material _pointCloudMaterial;

    [Header("Rendering")]
    [SerializeField] private DepthEyeSelection _eyeSelection = DepthEyeSelection.Left;
    [SerializeField] private Color _pointColor = new(0.15f, 0.85f, 1f, 0.9f);
    [SerializeField, Min(1f)] private float _pointSize = 2f;
    [SerializeField, Min(0f)] private float _minDepthMeters = 0.1f;
    [SerializeField, Min(0.01f)] private float _maxDepthMeters = 5f;

    private MeshRenderer _meshRenderer;
    private MeshFilter _meshFilter;
    private Material _runtimeMaterial;
    private Mesh _pointMesh;
    private int _meshTextureSize;

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
    }

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
    }

    private void OnValidate()
    {
        if (_maxDepthMeters < _minDepthMeters)
        {
            _maxDepthMeters = _minDepthMeters;
        }

        ApplyMaterialProperties();
        UpdateMeshBounds();
    }

    private void Update()
    {
        if (_depthTextureAccess == null)
        {
            return;
        }

        _depthTextureAccess.RequestDepthSample();
    }

    private void HandleDepthTextureUpdate(DepthTextureAccess.DepthFrameData depthFrameData)
    {
        if (_depthTextureAccess == null)
        {
            return;
        }

        if (depthFrameData.ViewProjectionMatrix == null || depthFrameData.ViewProjectionMatrix.Length == 0)
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
            BuildPointMesh(textureSize);
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
            name = $"{nameof(DepthVisualizer)} Runtime Material"
        };

        _meshRenderer.sharedMaterial = _runtimeMaterial;
    }

    private void ApplyMaterialProperties()
    {
        if (_runtimeMaterial == null)
        {
            return;
        }

        _runtimeMaterial.SetColor(PointColorId, _pointColor);
        _runtimeMaterial.SetFloat(PointSizeId, _pointSize);
        _runtimeMaterial.SetVector(DepthRangeId, new Vector4(_minDepthMeters, _maxDepthMeters, 0f, 0f));
    }

    private void BuildPointMesh(int textureSize)
    {
        if (_pointMesh != null)
        {
            Destroy(_pointMesh);
        }

        var pointCount = textureSize * textureSize;
        var positions = new Vector3[pointCount];
        var uvs = new Vector2[pointCount];
        var indices = new int[pointCount];
        var inverseTextureSize = 1f / textureSize;

        var index = 0;
        for (var y = 0; y < textureSize; y++)
        {
            var v = (y + 0.5f) * inverseTextureSize;
            for (var x = 0; x < textureSize; x++)
            {
                positions[index] = Vector3.zero;
                uvs[index] = new Vector2((x + 0.5f) * inverseTextureSize, v);
                indices[index] = index;
                index++;
            }
        }

        _pointMesh = new Mesh
        {
            name = $"DepthPointCloud_{textureSize}"
        };
        _pointMesh.indexFormat = IndexFormat.UInt32;
        _pointMesh.vertices = positions;
        _pointMesh.uv = uvs;
        _pointMesh.SetIndices(indices, MeshTopology.Points, 0, calculateBounds: false);
        _pointMesh.UploadMeshData(false);

        _meshTextureSize = textureSize;
        _meshFilter.sharedMesh = _pointMesh;
        UpdateMeshBounds();
    }

    private void UpdateMeshBounds()
    {
        if (_pointMesh == null)
        {
            return;
        }

        var extent = Mathf.Max(1f, _maxDepthMeters);
        _pointMesh.bounds = new Bounds(Vector3.zero, Vector3.one * extent * 4f);
    }

    private int ResolveEyeIndex()
    {
        return _eyeSelection == DepthEyeSelection.Right ? 1 : 0;
    }
}

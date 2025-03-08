using Meta.XR.MRUtilityKit;
using UnityEngine;
/// Copy right 2025 Takashi Yoshinaga
/// <summary>
/// Demonstrates how to extract and display a miniature version of the room mesh
/// captured by Meta's Mixed Reality Utility Kit.
/// </summary>
public class RoomMeshSample : MonoBehaviour
{
    [SerializeField] private GameObject _miniatureRoomPrefab;
    [SerializeField] private EffectMesh _effectMesh;
    [SerializeField] private Vector3 _miniatureRoomOffset = new Vector3(0, -0.3f, 0.4f);
    [SerializeField] private float _miniatureRoomScale = 0.1f;
    [SerializeField] private float _colliderScale = 1.0f;

    private GameObject _miniatureRoomObject;
    private Mesh _transformedMesh; // Tracks the current mesh to manage memory properly
    private bool _isFullScaleRoomVisible = true;

    /// <summary>
    /// Extracts the room mesh from the EffectMesh component and creates a miniature version.
    /// This can be called when you want to create or update the miniature room representation.
    /// </summary>
    public void ExtractMesh()
    {
        // Filter to only process the global mesh
        var filter = new LabelFilter(MRUKAnchor.SceneLabels.GLOBAL_MESH);
        var meshes = _effectMesh.EffectMeshObjects;
        
        foreach (var kv in meshes)
        {
            bool filterByLabel = filter.PassesFilter(kv.Key.Label);
            if (kv.Value.effectMeshGO && filterByLabel)
            {    
                // Get the original mesh from the effect mesh game object
                Mesh originalMesh = kv.Value.effectMeshGO.GetComponent<MeshFilter>().mesh;
                
                // Clean up previous mesh to prevent memory leaks
                if (_transformedMesh != null)
                {
                    Destroy(_transformedMesh);
                }
                
                // Create a new mesh with vertices transformed to world space
                _transformedMesh = CreateTransformedMesh(
                    originalMesh, 
                    kv.Value.effectMeshGO.transform
                );
                
                // Calculate position in front of the camera
                var camera = Camera.main;
                var cameraForward = camera.transform.forward;
                cameraForward.y = 0; // Remove vertical component to keep it level
                cameraForward.Normalize();
                
                // Position the miniature room in front of the user at the specified offset
                var placingPosition = camera.transform.position + 
                                      cameraForward * _miniatureRoomOffset.z + 
                                      new Vector3(0, _miniatureRoomOffset.y, 0);

                // Create or update the miniature room object
                if (_miniatureRoomObject == null)
                {
                    // Instantiate the miniature room prefab at the calculated position
                    _miniatureRoomObject = Instantiate(_miniatureRoomPrefab, placingPosition, Quaternion.identity);
                    _miniatureRoomObject.GetComponent<MeshFilter>().mesh = _transformedMesh;
                    _miniatureRoomObject.transform.localScale = new Vector3(_miniatureRoomScale, _miniatureRoomScale, _miniatureRoomScale);
                    
                    // Update BoxCollider to match the mesh bounds
                    UpdateBoxCollider(_miniatureRoomObject, _transformedMesh);
                }
                else
                {
                    // Update existing miniature room position and mesh
                    _miniatureRoomObject.transform.position = placingPosition;
                    _miniatureRoomObject.transform.rotation = Quaternion.identity;
                    _miniatureRoomObject.GetComponent<MeshFilter>().mesh = _transformedMesh;
                    
                    // Update BoxCollider to match the new mesh bounds
                    UpdateBoxCollider(_miniatureRoomObject, _transformedMesh);
                }
                
                // We only need to process the first valid mesh
                //break;
            }
        }
    }


    /// <summary>
    /// Toggles the visibility of the full-scale room mesh between visible and hidden.
    /// </summary>
    public void ToggleFullScaleRoomVisibility()
    {
        // Toggle the visibility state
        _isFullScaleRoomVisible = !_isFullScaleRoomVisible;
        
        // Apply the visibility setting to the effect mesh using the filter
        var filter = new LabelFilter(MRUKAnchor.SceneLabels.GLOBAL_MESH);
        _effectMesh.ToggleEffectMeshVisibility(_isFullScaleRoomVisible, filter);
    }
    
    /// <summary>
    /// Creates a new mesh with vertices transformed from local space to world space.
    /// </summary>
    /// <param name="sourceMesh">The original mesh in local space</param>
    /// <param name="sourceTransform">The transform to apply to the vertices</param>
    /// <returns>A new mesh with vertices in world space</returns>
    private Mesh CreateTransformedMesh(Mesh sourceMesh, Transform sourceTransform)
    {
        Mesh worldSpaceMesh = new Mesh();
        
        // Copy mesh name and add a suffix
        worldSpaceMesh.name = sourceMesh.name + "_WorldSpace";
        
        // Transform vertices from local to world space
        Vector3[] localVertices = sourceMesh.vertices;
        Vector3[] worldVertices = new Vector3[localVertices.Length];
        
        for (int i = 0; i < localVertices.Length; i++)
        {
            worldVertices[i] = sourceTransform.TransformPoint(localVertices[i]);
        }
        
        // Transform normals from local to world space
        Vector3[] localNormals = sourceMesh.normals;
        Vector3[] worldNormals = new Vector3[localNormals.Length];
        
        if (localNormals != null && localNormals.Length > 0)
        {
            for (int i = 0; i < localNormals.Length; i++)
            {
                worldNormals[i] = sourceTransform.TransformDirection(localNormals[i]).normalized;
            }
        }
        
        // Copy triangles (indices don't change during transformation)
        int[] triangles = sourceMesh.triangles;
        
        // Assign transformed data to the new mesh
        worldSpaceMesh.vertices = worldVertices;
        worldSpaceMesh.triangles = triangles;
        
        if (localNormals != null && localNormals.Length > 0)
        {
            worldSpaceMesh.normals = worldNormals;
        }
        else
        {
            // Calculate normals if none were provided in the source mesh
            worldSpaceMesh.RecalculateNormals();
        }
        
        // Update the bounds of the new mesh
        worldSpaceMesh.RecalculateBounds();
        
        return worldSpaceMesh;
    }
    
    /// <summary>
    /// Updates the BoxCollider of an object to match the bounds of a mesh,
    /// accounting for the object's scale.
    /// </summary>
    /// <param name="obj">The GameObject with the BoxCollider</param>
    /// <param name="mesh">The mesh to base the collider size on</param>
    private void UpdateBoxCollider(GameObject obj, Mesh mesh)
    {
        BoxCollider boxCollider = obj.GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            boxCollider = obj.AddComponent<BoxCollider>();
        }

        // Get the mesh bounds
        Bounds meshBounds = mesh.bounds;

        // Apply the mesh bounds to the collider
        // Since the collider is in local space, we need to account for the object's scale
        boxCollider.center = meshBounds.center;
        
        // The scale factor is already applied to the object's transform,
        // so we don't need to apply it to the collider size
        boxCollider.size = meshBounds.size*_colliderScale;
    }

    
    /// <summary>
    /// Cleans up resources when the component is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        if (_transformedMesh != null)
        {
            Destroy(_transformedMesh);
            _transformedMesh = null;
        }
    }
}

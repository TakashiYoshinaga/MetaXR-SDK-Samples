
using UnityEngine;
using UnityEngine.UI;
public class RoomMeshUI : MonoBehaviour
{   
    [SerializeField] private Button _visibilityButton;
    [SerializeField] private Button _extractButton;
    [SerializeField] private RoomMeshSample _roomMeshSample;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //Toggle the visibility of the full scale room
        _visibilityButton.onClick.AddListener(() =>
        {
            _roomMeshSample.ToggleFullScaleRoomVisibility();
        });
        //Extract mesh and hide the button after extraction
        _extractButton.onClick.AddListener(() =>
        {
            _roomMeshSample.ExtractMesh();
            _extractButton.gameObject.SetActive(false);
        });
    }

}

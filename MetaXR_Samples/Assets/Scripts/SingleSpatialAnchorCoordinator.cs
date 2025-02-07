using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SingleSpatialAnchorCoordinator : MonoBehaviour
{
    [SerializeField] Button _createAnchorButton;
    [SerializeField] Button _loadAnchorButton;
    [SerializeField] Button _deleteAnchorButton;
    [SerializeField] AnchorManager _anchorManager;
    [SerializeField] string _saveAnchorKey = "anchor";
    
    // Start is called before the first frame update
    void Awake()
    {
        _createAnchorButton.onClick.AddListener(CreateAnchor);
        _loadAnchorButton.onClick.AddListener(LoadAnchor);
        _deleteAnchorButton.onClick.AddListener(DeleteAnchor);
        SetSaveAnchorKey(_saveAnchorKey);
    }
    public void SetSaveAnchorKey(string saveAnchorKey){
        _saveAnchorKey = saveAnchorKey;
        _anchorManager.SetSaveAnchorKey(saveAnchorKey);
    }

    public void CreateAnchor()
    {
        _anchorManager.CreateAnchor();
    }

    public void LoadAnchor()
    {
        _anchorManager.LoadAnchor();
    }
    
    public void DeleteAnchor()
    {
        _anchorManager.DeleteAnchor();
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SpatialAnchorCoordinator : MonoBehaviour
{
    [SerializeField] Button _createAnchorButton;
    [SerializeField] Button _loadAnchorButton;
    [SerializeField] Button _deleteAnchorButton;
    [SerializeField] AnchorManager _anchorManager;
    
    // Start is called before the first frame update
    void Start()
    {
        _createAnchorButton.onClick.AddListener(_anchorManager.CreateAnchor);
        _loadAnchorButton.onClick.AddListener(_anchorManager.LoadAnchor);
        _deleteAnchorButton.onClick.AddListener(_anchorManager.DeleteAnchor);
    }
}

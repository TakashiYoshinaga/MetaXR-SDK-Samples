using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MultiSpatialAnchorCoordinator : MonoBehaviour
{
    [SerializeField] Button _globalCreateAnchorButton;
    [SerializeField] Button _globalLoadAnchorButton;
    [SerializeField] Button _globalDeleteAnchorButton;
    [SerializeField] SingleSpatialAnchorCoordinator[] _anchorCoordinators;
    [SerializeField] string _baseSaveAnchorKey = "anchor";
    
    // Start is called before the first frame update
    void Start()
    {
        SetSaveAnchorKey();
        AddListeners();
    }

    void SetSaveAnchorKey(){
        for(int i = 0; i < _anchorCoordinators.Length; i++){
            //Set unique key for each object to save the anchor
            _anchorCoordinators[i].SetSaveAnchorKey(_baseSaveAnchorKey + i);
        }
    }

    void AddListeners(){
        for(int i = 0; i < _anchorCoordinators.Length; i++){
            _globalCreateAnchorButton.onClick.AddListener(_anchorCoordinators[i].CreateAnchor);
            _globalLoadAnchorButton.onClick.AddListener(_anchorCoordinators[i].LoadAnchor);
            _globalDeleteAnchorButton.onClick.AddListener(_anchorCoordinators[i].DeleteAnchor);
        }
    }
    
}

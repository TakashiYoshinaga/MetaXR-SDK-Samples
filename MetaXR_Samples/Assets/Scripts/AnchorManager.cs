using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class AnchorManager : MonoBehaviour
{
    //Set unique key for each object to save the anchor
    [SerializeField] string _saveAnchorKey = "anchor";
    //Anchor linked to this object
    OVRSpatialAnchor _anchor;
    //UUID of the anchor
    System.Guid _uuid;

    public async void CreateAnchor(){
        bool result = await CreateAnchorAsync();
        if(result){
            SaveAnchorAsync();
        }
    }
    
    public void LoadAnchor(){
        LoadAnchorAsync();
    }
    public void DeleteAnchor(){
        DeleteAnchorAsync();
    }

    private async Task<bool> CreateAnchorAsync(){
        Debug.Log("*****Creating anchor");
        //Destroy the previous anchor if it exists
        if(_anchor){
           Destroy(_anchor);
           _anchor = null;
        }
        //Create a new anchor
        _anchor = gameObject.AddComponent<OVRSpatialAnchor>();
        if(!await _anchor.WhenLocalizedAsync()){
            Debug.Log("*****No anchor found");
            return false;
        }else{
            _uuid = _anchor.Uuid;
            Debug.Log("*****Anchor created with UUID: " + _uuid);
            return true;
        }
    }

    private async void SaveAnchorAsync(){
        if(!_anchor){
            Debug.Log("*****No anchor to save");
            return;
        }
        //Save the anchor
        var result = await _anchor.SaveAnchorAsync();
        //Check if the anchor was saved
        if(result.Success){
            //Save the UUID of the anchor to the Device
            PlayerPrefs.SetString(_saveAnchorKey,_uuid.ToString());
            Debug.Log("*****Anchor saved with UUID: " + _uuid);
        }else{
            Debug.Log("*****Failed to save anchor: " + result.Status);
        }
    }
    private async void LoadAnchorAsync(){
        var uuid = PlayerPrefs.GetString(_saveAnchorKey,"");
        if(string.IsNullOrEmpty(uuid)){
            Debug.Log("*****No anchor to load");
            return;
        }
        var uuids = new System.Guid[1]{new System.Guid(uuid)};
        var unboundAnchors = new List<OVRSpatialAnchor.UnboundAnchor>();
        var result = await OVRSpatialAnchor.LoadUnboundAnchorsAsync(uuids,unboundAnchors);
        if(result.Success){
            Debug.Log("*****Anchor loaded with UUID: " + uuid);
            //For this sample, we only load one anchor
            var unboundAnchor = unboundAnchors[0];
            //Localize the anchor
            var localizationResult = await unboundAnchor.LocalizeAsync();
            //Check if the anchor was localized
            if(localizationResult){
                Debug.Log("*****Anchor localized with UUID: " + uuid);
                //Get the pose of the anchor
                Pose pose;
                bool hasPose = unboundAnchor.TryGetPose(out pose);
                if(!hasPose){
                    Debug.Log("*****Failed to get pose");
                    return;
                }
                //Set the position and rotation of the object to the pose of the anchor
                _uuid = new System.Guid(uuid);
                transform.SetPositionAndRotation(pose.position,pose.rotation);
                if(!_anchor){
                    _anchor = gameObject.AddComponent<OVRSpatialAnchor>();
                }
                unboundAnchor.BindTo(_anchor);
            }else{
                Debug.Log("*****Failed to localize anchor");
            }

        }else{
            Debug.Log("*****Failed to load anchor: " + result.Status);
        }
    }
    private async void DeleteAnchorAsync(){
        if(!_anchor){
            Debug.Log("*****No anchor to delete");
            return;
        }
        var result = await _anchor.EraseAnchorAsync();
        if(result.Success){
            Debug.Log("*****Anchor deleted with UUID: " + _uuid);
            PlayerPrefs.DeleteKey(_saveAnchorKey);
            Destroy(_anchor);
            _anchor = null;
        }else{
            Debug.Log("*****Failed to delete anchor: " + result.Status);
        }
    }
}

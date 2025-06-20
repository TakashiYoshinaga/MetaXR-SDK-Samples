using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class TwoHandsScaleController : MonoBehaviour
{
    [SerializeField]
    private XRGrabInteractable grabInteractable;

    void Start()
    {
        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();

        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnSelectEntered);
            grabInteractable.selectExited.AddListener(OnSelectExited);
        }
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        CheckGrabCount();
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        CheckGrabCount();
    }

    private void CheckGrabCount()
    {
        int grabCount = grabInteractable.interactorsSelecting.Count;
        
        if (grabCount >= 2)
        {
            Debug.Log("Grabbed with both hands!");
            OnTwoHandGrab();
        }
        else if (grabCount == 1)
        {
            Debug.Log("Grabbed with one hand");
            OnSingleHandGrab();
        }
        else
        {
            Debug.Log("Not grabbed");
            OnNoGrab();
        }
    }

    private void OnTwoHandGrab()
    {
        // Two-hand grab transform control
        // Enable scaling, adjust rotation control, etc.
        grabInteractable.trackRotation = false; // Disable rotation tracking during two-hand operation
        grabInteractable.trackPosition = false; // Disable position tracking if needed
    }

    private void OnSingleHandGrab()
    {
        // Single-hand grab transform control
        // Currently no specific behavior needed
    }

    private void OnNoGrab()
    {
        // No grab transform control
        // Restore to default state
        grabInteractable.trackRotation = true; // Enable rotation tracking
        grabInteractable.trackPosition = true; // Enable position tracking
    }

    void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
            grabInteractable.selectExited.RemoveListener(OnSelectExited);
        }
    }
}

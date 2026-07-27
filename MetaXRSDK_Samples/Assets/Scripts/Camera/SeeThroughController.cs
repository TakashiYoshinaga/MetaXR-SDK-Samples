using UnityEngine;

public class SeeThroughController : MonoBehaviour
{
    [SerializeField] private Camera seeThroughCamera;
    [SerializeField] private float alphaChangeSpeed = 0.5f;

    private float _cameraAlpha = 0.5f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _cameraAlpha = seeThroughCamera.backgroundColor.a;
    }

    // Update is called once per frame
    void Update()
    {
        Vector2 rightThumbstick = OVRInput.Get(OVRInput.RawAxis2D.RThumbstick);
        _cameraAlpha = Mathf.Clamp01(
            _cameraAlpha + rightThumbstick.y * alphaChangeSpeed * Time.deltaTime);

        Color backgroundColor = seeThroughCamera.backgroundColor;
        backgroundColor.a = _cameraAlpha;
        seeThroughCamera.backgroundColor = backgroundColor;
    }
}

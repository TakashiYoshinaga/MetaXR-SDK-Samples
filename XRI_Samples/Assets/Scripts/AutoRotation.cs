using UnityEngine;

public class AutoRotation : MonoBehaviour
{
    [SerializeField] private Vector3 _rotationSpeed = new Vector3(0, 50, 0); // Degrees per second
    private bool _isRotating = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (_isRotating)
        {
            // Rotate the object around its local Y axis at the specified speed
            transform.Rotate(_rotationSpeed * Time.deltaTime, Space.Self);
        }
    }
    public void SwitchRotation()
    {
        _isRotating = !_isRotating;
    }
}

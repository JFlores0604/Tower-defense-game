using UnityEngine;

// ============================================================================
//  CameraController.cs — 3D top-down camera.
//   WASD / Arrow keys = pan     Mouse wheel = zoom     T = toggle straight-down view
// ============================================================================
public class CameraController : MonoBehaviour
{
    public Vector3 focus = new Vector3(0f, 0f, -0.5f);
    public float distance = 27f;
    public float pitch = 58f;        // degrees down from horizontal
    public float panSpeed = 14f;
    public float minDistance = 10f, maxDistance = 40f;
    public bool straightDown;

    float currentPitch;

    void Start()
    {
        currentPitch = pitch;
        var cam = GetComponent<Camera>();
        cam.fieldOfView = 50f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 200f;
        Apply();
    }

    void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        Vector3 move = Vector3.zero;
        if (GameInput.Held(KeyCode.W) || GameInput.Held(KeyCode.UpArrow)) move.z += 1f;
        if (GameInput.Held(KeyCode.S) || GameInput.Held(KeyCode.DownArrow)) move.z -= 1f;
        if (GameInput.Held(KeyCode.D) || GameInput.Held(KeyCode.RightArrow)) move.x += 1f;
        if (GameInput.Held(KeyCode.A) || GameInput.Held(KeyCode.LeftArrow)) move.x -= 1f;
        focus += move.normalized * panSpeed * dt * (distance / 27f);
        focus.x = Mathf.Clamp(focus.x, -18f, 18f);
        focus.z = Mathf.Clamp(focus.z, -11f, 11f);

        float scroll = GameInput.ConsumeScroll();
        distance = Mathf.Clamp(distance + scroll * 1.2f, minDistance, maxDistance);

        float targetPitch = straightDown ? 89f : pitch;
        currentPitch = Mathf.MoveTowards(currentPitch, targetPitch, 120f * dt);
        Apply();
    }

    void Apply()
    {
        Quaternion rot = Quaternion.Euler(currentPitch, 0f, 0f);
        transform.rotation = rot;
        transform.position = focus - rot * Vector3.forward * distance;
    }
}

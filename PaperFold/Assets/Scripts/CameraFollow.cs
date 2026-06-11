using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothTime = 0.18f;
    public float normalSize = 6f;
    public float zoomedOutSize = 14f;
    public float lookAhead = 2f;

    Camera cam;
    Vector3 velocity;
    float targetSize;

    void Awake()
    {
        cam = GetComponent<Camera>();
        targetSize = normalSize;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // O zooms out to plan a route ("see the jump"), I returns to normal
        if (Input.GetKeyDown(KeyCode.O)) targetSize = zoomedOutSize;
        if (Input.GetKeyDown(KeyCode.I)) targetSize = normalSize;
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, 6f * Time.deltaTime);

        var body = target.GetComponent<Rigidbody2D>();
        float ahead = body != null ? Mathf.Clamp(body.linearVelocity.x * 0.25f, -lookAhead, lookAhead) : 0f;
        Vector3 goal = new Vector3(target.position.x + ahead, target.position.y + 1.5f, -10f);
        transform.position = Vector3.SmoothDamp(transform.position, goal, ref velocity, smoothTime);
    }
}

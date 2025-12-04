using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollowFraming2D : MonoBehaviour
{
    public Transform target;
    [Tooltip("Viewport anchor where the target should appear (0..1). X=0.1 =>10% from left, Y=0.1 =>10% from bottom.")]
    public Vector2 viewportAnchor = new Vector2(0.1f, 0.10f);

    [Tooltip("Smoothing time applied to camera movement.")]
    public float smoothTime = 0.25f;

    [Tooltip("Optional extra world offset added after framing (useful for fine tuning).")]
    public Vector3 worldOffset = Vector3.zero;

    [Tooltip("If true, anchor the target's BOTTOM to the vertical viewport anchor instead of its pivot.")]
    public bool anchorToTargetBottom = true;

    [Header("Constraints")]
    [Tooltip("Lock vertical movement. When true the camera will not move up/down.")]
    public bool lockVertical = true;

    private Camera cam;
    private Vector3 velocity;
    private float fixedY; // stored Y when vertical lock is enabled

    void Awake()
    {
        cam = GetComponent<Camera>();
        fixedY = transform.position.y;
    }

    void LateUpdate()
    {
        if (target == null || cam == null) return;

        // Determine desired position based on projection
        Vector3 desired;
        if (!cam.orthographic)
        {
            // Fallback: simple smooth follow, center target with offset
            desired = new Vector3(target.position.x, target.position.y, transform.position.z) + worldOffset;
        }
        else
        {
            // Compute camera position so that the target (or its bottom) appears at the given viewport anchor for an orthographic camera
            float ortho = cam.orthographicSize;
            float aspect = cam.aspect;
            // Offset from camera center to the desired viewport anchor in world units
            Vector2 worldOffsetFromCenter = new Vector2(
            (viewportAnchor.x - 0.5f) * 2f * ortho * aspect,
            (viewportAnchor.y - 0.5f) * 2f * ortho
            );

            // Determine which point on the target should be framed: pivot or bottom
            Vector3 anchorPoint = target.position;
            if (anchorToTargetBottom)
            {
                float halfHeight = 0f;
                var sr = target.GetComponentInChildren<SpriteRenderer>();
                if (sr != null) halfHeight = sr.bounds.extents.y;
                else
                {
                    var r = target.GetComponentInChildren<Renderer>();
                    if (r != null) halfHeight = r.bounds.extents.y;
                }
                anchorPoint.y -= halfHeight; // use bottom of the target
            }

            desired = new Vector3(
            anchorPoint.x - worldOffsetFromCenter.x,
            anchorPoint.y - worldOffsetFromCenter.y,
            transform.position.z // keep current Z
            ) + worldOffset;
        }

        // Apply vertical lock if requested
        if (lockVertical)
        {
            desired.y = fixedY;
            // prevent y velocity accumulation during smoothing
            velocity.y = 0f;
        }

        Vector3 newPos = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
        if (lockVertical)
        {
            newPos.y = fixedY;
        }
        transform.position = newPos;
    }
}

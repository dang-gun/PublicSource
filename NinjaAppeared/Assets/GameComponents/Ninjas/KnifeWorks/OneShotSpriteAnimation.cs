using UnityEngine;

// Plays a provided sprite sequence once, then optionally destroys itself
public class OneShotSpriteAnimation : MonoBehaviour
{
    public Sprite[] sprites;
    public float framesPerSecond = 24f;
    public bool loop = false;
    public bool autoDestroy = true;

    private SpriteRenderer sr;
    private int frameIndex;
    private float frameTimer;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            sr = gameObject.AddComponent<SpriteRenderer>();
        }
        // Apply global VFX scale (affects only this effect object)
        transform.localScale = Vector3.one * GameConstants.KnifeWorkVFX_Scale;
    }

    void OnEnable()
    {
        frameIndex = 0;
        frameTimer = 0f;
        if (sprites != null && sprites.Length > 0)
        {
            sr.sprite = sprites[0];
        }
        // Apply a vivid random color tint for the slash VFX each time it is enabled
        // Hue [0,1], Saturation [0.75,1], Value [0.9,1] for bright colors, Alpha1
        sr.color = Random.ColorHSV(0f, 1f, 0.75f, 1f, 0.9f, 1f, 1f, 1f);
    }

    void Update()
    {
        if (sprites == null || sprites.Length == 0) return;
        if (framesPerSecond <= 0f) return;

        frameTimer += Time.deltaTime;
        float frameTime = 1f / framesPerSecond;
        if (frameTimer >= frameTime)
        {
            int advance = Mathf.FloorToInt(frameTimer / frameTime);
            frameTimer -= advance * frameTime;
            frameIndex += advance;

            if (frameIndex >= sprites.Length)
            {
                if (loop)
                {
                    frameIndex %= sprites.Length;
                }
                else
                {
                    // finished
                    frameIndex = sprites.Length - 1;
                    sr.sprite = sprites[frameIndex];
                    if (autoDestroy)
                    {
                        Destroy(gameObject);
                    }
                    else
                    {
                        enabled = false;
                    }
                    return;
                }
            }

            sr.sprite = sprites[frameIndex];
        }
    }
}

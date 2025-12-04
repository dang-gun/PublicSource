using System.Collections.Generic;
using UnityEngine;

// Repeats a source background tile so the camera always sees background.
[ExecuteAlways]
public class BackgroundLooper : MonoBehaviour
{
    [Tooltip("Camera to follow for tiling coverage. If null, uses Camera.main")] public Camera targetCamera;
    [Tooltip("Source tile to repeat. If null, will try to find a child named 'Background_Loop1'.")] public Transform sourceTile;
    [Tooltip("Tile horizontally")] public bool tileX = true;
    [Tooltip("Tile vertically")] public bool tileY = false; // only horizontal tiling
    [Tooltip("Extra tiles of margin beyond the view to avoid gaps.")] public int marginTiles = 1;

    private Vector2 tileSize; // world units
    private Transform tilesRoot;
    private List<Transform> tiles = new List<Transform>();
    private int cols, rows;

    void OnEnable()
    {
        EnsureSetup();
    }

    void Start()
    {
        EnsureSetup();
    }

    void EnsureSetup()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (sourceTile == null)
        {
            var child = transform.Find("Background_Loop1");
            if (child != null) sourceTile = child;
        }
        if (sourceTile == null || targetCamera == null) return;

        // Measure tile size from renderer bounds
        var r = sourceTile.GetComponentInChildren<Renderer>();
        if (r == null)
        {
            Debug.LogWarning("BackgroundLooper: Source tile has no Renderer to measure size.");
            return;
        }
        tileSize = r.bounds.size;
        if (tileSize.x <= 0f) tileSize.x = 1f;
        if (tileSize.y <= 0f) tileSize.y = 1f;

        if (tilesRoot == null)
        {
            var rootGo = new GameObject("_BG_Tiles");
            rootGo.transform.SetParent(transform, false);
            tilesRoot = rootGo.transform;
        }

        BuildInitialGrid();
        // Hide the template tile renderer to avoid double draw (keep as reference)
        SetRenderersEnabled(sourceTile.gameObject, false);
    }

    void BuildInitialGrid()
    {
        if (targetCamera == null) return;
        float viewH = targetCamera.orthographic ? targetCamera.orthographicSize * 2f : 20f;
        float viewW = targetCamera.orthographic ? viewH * targetCamera.aspect : 30f;

        cols = tileX ? Mathf.CeilToInt(viewW / tileSize.x) + (marginTiles * 2 + 1) : 1;
        rows = 1; // vertical tiling disabled
        int total = cols * rows;

        // Clear old
        foreach (var t in tiles)
        {
            if (t != null)
            {
                if (Application.isPlaying) Destroy(t.gameObject); else DestroyImmediate(t.gameObject);
            }
        }
        tiles.Clear();

        // Prepare prototype properties
        var protoRenderer = sourceTile.GetComponentInChildren<Renderer>();
        int sortingLayerID = protoRenderer != null ? protoRenderer.sortingLayerID : 0;
        int sortingOrder = protoRenderer != null ? protoRenderer.sortingOrder : 0;

        for (int i = 0; i < total; i++)
        {
            var go = Instantiate(sourceTile.gameObject, tilesRoot);
            go.name = $"Tile_{i}";
            SetRenderersEnabled(go, true);
            var rend = go.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                rend.sortingLayerID = sortingLayerID;
                rend.sortingOrder = sortingOrder;
            }
            tiles.Add(go.transform);
        }
    }

    void LateUpdate()
    {
        if (targetCamera == null || sourceTile == null || tilesRoot == null || tiles.Count == 0) return;

        // Calculate visible bounds in world space
        float viewH = targetCamera.orthographic ? targetCamera.orthographicSize * 2f : 20f;
        float viewW = targetCamera.orthographic ? viewH * targetCamera.aspect : 30f;
        Vector3 camPos = targetCamera.transform.position;
        float minX = camPos.x - viewW * 0.5f;

        // Compute starting tile index with margin
        int startCol = Mathf.FloorToInt(minX / tileSize.x) - marginTiles;

        // Position tiles in a horizontal strip at source Y, covering the camera
        int index = 0;
        float y = sourceTile.position.y;
        for (int c = 0; c < cols; c++)
        {
            if (index >= tiles.Count) break;
            int colIndex = startCol + c;
            float x = colIndex * tileSize.x + tileSize.x * 0.5f;
            var t = tiles[index++];
            Vector3 pos = new Vector3(x, y, sourceTile.position.z);
            t.position = pos;
        }
    }

    private void SetRenderersEnabled(GameObject go, bool enabled)
    {
        var srs = go.GetComponentsInChildren<Renderer>(true);
        foreach (var s in srs) s.enabled = enabled;
    }
}

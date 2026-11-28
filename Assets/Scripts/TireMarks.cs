using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

internal sealed class TireMarks : IDisposable
{
    private const int SegmentCount = 768;
    private const float HalfWidth = 0.105f;
    private const float MinSegmentLength = 0.1f;
    private const float MaxSegmentLength = 2f;

    internal struct Track
    {
        public bool HasPoint;
        public bool HasEdge;
        public Vector3 Position;
        public Vector3 Edge;
        public byte Alpha;
    }

    private readonly Vector3[] vertices = new Vector3[SegmentCount * 4];
    private readonly Color32[] colors = new Color32[SegmentCount * 4];
    private readonly GameObject worldObject;
    private readonly Mesh mesh;
    private readonly Material material;
    private int nextSegment;
    private bool dirty;

    public TireMarks(Scene scene)
    {
        Shader shader = Resources.Load<Shader>("TireMarks");
        if (shader == null)
            return;

        worldObject = new GameObject("Tire marks", typeof(MeshFilter), typeof(MeshRenderer));
        SceneManager.MoveGameObjectToScene(worldObject, scene);
        material = new Material(shader);
        mesh = new Mesh { name = "Tire marks" };
        mesh.MarkDynamic();
        mesh.vertices = vertices;
        mesh.colors32 = colors;

        var uv = new Vector2[vertices.Length];
        var triangles = new int[SegmentCount * 6];
        for (int i = 0; i < SegmentCount; i++)
        {
            int v = i * 4;
            int t = i * 6;
            uv[v] = uv[v + 2] = Vector2.zero;
            uv[v + 1] = uv[v + 3] = Vector2.right;
            triangles[t] = v;
            triangles[t + 1] = v + 2;
            triangles[t + 2] = v + 1;
            triangles[t + 3] = v + 1;
            triangles[t + 4] = v + 2;
            triangles[t + 5] = v + 3;
        }
        mesh.uv = uv;
        mesh.triangles = triangles;

        worldObject.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = worldObject.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    public void Update(ref Track track, WheelHit hit, float slip)
    {
        if (mesh == null || slip < 0.16f)
        {
            track = default;
            return;
        }

        Vector3 position = hit.point + hit.normal * 0.016f;
        byte alpha = (byte)Mathf.RoundToInt(180f * Mathf.InverseLerp(0.1f, 0.95f, slip));
        Vector3 travel = position - track.Position;
        if (!track.HasPoint || travel.sqrMagnitude > MaxSegmentLength * MaxSegmentLength)
        {
            track = new Track { HasPoint = true, Position = position, Alpha = alpha };
            return;
        }
        if (travel.sqrMagnitude < MinSegmentLength * MinSegmentLength)
            return;

        Vector3 side = Vector3.Cross(hit.normal, travel).normalized * HalfWidth;
        Vector3 previousSide = track.HasEdge && Vector3.Dot(track.Edge, side) > 0f ? track.Edge : side;
        int index = nextSegment * 4;
        vertices[index] = track.Position - previousSide;
        vertices[index + 1] = track.Position + previousSide;
        vertices[index + 2] = position - side;
        vertices[index + 3] = position + side;
        colors[index] = colors[index + 1] = new Color32(28, 25, 24, track.Alpha);
        colors[index + 2] = colors[index + 3] = new Color32(28, 25, 24, alpha);
        track = new Track { HasPoint = true, HasEdge = true, Position = position, Edge = side, Alpha = alpha };
        nextSegment = (nextSegment + 1) % SegmentCount;
        dirty = true;
    }

    public void Flush()
    {
        if (!dirty)
            return;
        mesh.vertices = vertices;
        mesh.colors32 = colors;
        mesh.RecalculateBounds();
        dirty = false;
    }

    public void Dispose()
    {
        UnityEngine.Object.Destroy(worldObject);
        UnityEngine.Object.Destroy(mesh);
        UnityEngine.Object.Destroy(material);
    }
}

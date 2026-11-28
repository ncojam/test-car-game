using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

internal sealed class TireMarks : IDisposable
{
    private const int SegmentsPerChunk = 512;
    private const int MaxActiveChunks = 12;
    private const int MaxBakedChunks = 80;
    private const int MaxBakedChunksPerCell = 8;
    private const float CellSize = 24f;
    private const float BakeAfterSeconds = 6f;

    private static readonly int[] Triangles = BuildTriangles();
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

    private readonly Dictionary<Vector2Int, Chunk> active = new Dictionary<Vector2Int, Chunk>();
    private readonly LinkedList<Chunk> baked = new LinkedList<Chunk>();
    private readonly List<Vector2Int> readyToBake = new List<Vector2Int>(MaxActiveChunks);
    private readonly GameObject worldObject;
    private readonly Material material;

    public TireMarks(Scene scene)
    {
        Shader shader = Resources.Load<Shader>("TireMarks");
        if (shader == null)
            return;

        worldObject = new GameObject("Tire marks");
        SceneManager.MoveGameObjectToScene(worldObject, scene);
        material = new Material(shader);
    }

    public void Update(ref Track track, WheelHit hit, float slip)
    {
        if (material == null || slip < 0.16f)
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
        Vector3 midpoint = (track.Position + position) * 0.5f;
        Vector2Int cell = new Vector2Int(Mathf.FloorToInt(midpoint.x / CellSize), Mathf.FloorToInt(midpoint.z / CellSize));
        Chunk chunk = GetChunk(cell);
        float now = Time.time;
        chunk.Add(track.Position - previousSide, track.Position + previousSide,
            position - side, position + side, track.Alpha, alpha, now);
        track = new Track { HasPoint = true, HasEdge = true, Position = position, Edge = side, Alpha = alpha };
        if (chunk.IsFull)
            Bake(cell);
    }

    public void Flush()
    {
        float now = Time.time;
        foreach (var entry in active)
        {
            if (now - entry.Value.LastWrite >= BakeAfterSeconds)
                readyToBake.Add(entry.Key);
            else
                entry.Value.Flush();
        }

        foreach (Vector2Int cell in readyToBake)
            Bake(cell);
        readyToBake.Clear();
    }

    private Chunk GetChunk(Vector2Int cell)
    {
        if (active.TryGetValue(cell, out Chunk chunk))
            return chunk;

        if (active.Count >= MaxActiveChunks)
        {
            Vector2Int oldest = default;
            float oldestWrite = float.PositiveInfinity;
            foreach (var entry in active)
            {
                if (entry.Value.LastWrite < oldestWrite)
                {
                    oldestWrite = entry.Value.LastWrite;
                    oldest = entry.Key;
                }
            }
            Bake(oldest);
        }

        chunk = new Chunk(cell, worldObject.transform, material);
        active.Add(cell, chunk);
        return chunk;
    }

    private void Bake(Vector2Int cell)
    {
        Chunk chunk = active[cell];
        active.Remove(cell);
        chunk.Bake();

        LinkedListNode<Chunk> oldestInCell = null;
        int countInCell = 0;
        for (LinkedListNode<Chunk> node = baked.First; node != null; node = node.Next)
        {
            if (node.Value.Cell != cell)
                continue;
            if (oldestInCell == null)
                oldestInCell = node;
            countInCell++;
        }

        if (countInCell >= MaxBakedChunksPerCell)
            RemoveBaked(oldestInCell);
        if (baked.Count >= MaxBakedChunks)
            RemoveBaked(baked.First);
        baked.AddLast(chunk);
    }

    private void RemoveBaked(LinkedListNode<Chunk> node)
    {
        node.Value.Dispose();
        baked.Remove(node);
    }

    public void Dispose()
    {
        foreach (Chunk chunk in active.Values)
            chunk.Dispose();
        foreach (Chunk chunk in baked)
            chunk.Dispose();
        UnityEngine.Object.Destroy(worldObject);
        UnityEngine.Object.Destroy(material);
    }

    private static int[] BuildTriangles()
    {
        var triangles = new int[SegmentsPerChunk * 6];
        for (int i = 0; i < SegmentsPerChunk; i++)
        {
            int v = i * 4;
            int t = i * 6;
            triangles[t] = v;
            triangles[t + 1] = v + 2;
            triangles[t + 2] = v + 1;
            triangles[t + 3] = v + 1;
            triangles[t + 4] = v + 2;
            triangles[t + 5] = v + 3;
        }
        return triangles;
    }

    private sealed class Chunk
    {
        public Vector2Int Cell { get; }
        public float LastWrite { get; private set; }
        public bool IsFull => vertices.Count == SegmentsPerChunk * 4;

        private readonly GameObject sceneObject;
        private readonly Mesh mesh;
        private List<Vector3> vertices = new List<Vector3>(SegmentsPerChunk * 4);
        private List<Color32> colors = new List<Color32>(SegmentsPerChunk * 4);
        private List<Vector2> coordinates = new List<Vector2>(SegmentsPerChunk * 4);
        private bool dirty;

        public Chunk(Vector2Int cell, Transform parent, Material material)
        {
            Cell = cell;
            sceneObject = new GameObject("Tire mark chunk", typeof(MeshFilter), typeof(MeshRenderer));
            sceneObject.transform.SetParent(parent, false);
            mesh = new Mesh { name = "Tire mark chunk" };
            mesh.MarkDynamic();
            sceneObject.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = sceneObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        public void Add(Vector3 startLeft, Vector3 startRight, Vector3 endLeft, Vector3 endRight,
            byte startAlpha, byte endAlpha, float now)
        {
            AddEdge(startLeft, startRight, startAlpha);
            AddEdge(endLeft, endRight, endAlpha);
            LastWrite = now;
            dirty = true;
        }

        private void AddEdge(Vector3 left, Vector3 right, byte alpha)
        {
            var color = new Color32(28, 25, 24, alpha);
            vertices.Add(left);
            vertices.Add(right);
            colors.Add(color);
            colors.Add(color);
            coordinates.Add(Vector2.zero);
            coordinates.Add(Vector2.right);
        }

        public void Flush()
        {
            if (!dirty)
                return;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(0, coordinates);
            mesh.SetTriangles(Triangles, 0, vertices.Count / 4 * 6, 0);
            dirty = false;
        }

        public void Bake()
        {
            Flush();
            mesh.UploadMeshData(true);
            vertices = null;
            colors = null;
            coordinates = null;
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(sceneObject);
            UnityEngine.Object.Destroy(mesh);
        }
    }
}

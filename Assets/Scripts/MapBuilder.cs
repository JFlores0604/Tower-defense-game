using System.Collections.Generic;
using UnityEngine;

/// Marks scenery / towers that block tower placement.
public class Blocker : MonoBehaviour { }

// ============================================================================
//  MapBuilder.cs — builds the board: grass, the stone path (procedural mesh),
//  the spawn gate, the temple you defend, and brick-built scenery.
// ============================================================================
public static class MapBuilder
{
    public const float BoardMinX = -16f, BoardMaxX = 16f, BoardMinZ = -10f, BoardMaxZ = 10f;
    public static Vector3 TemplePosition;

    public static void Build(Transform root, EnemyPath path)
    {
        var grassTex = Resources.Load<Texture2D>("Textures/grass");
        var pathTex = Resources.Load<Texture2D>("Textures/path");

        // ---------- Ground ----------
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.SetParent(root, false);
        ground.transform.localPosition = new Vector3(0f, -0.5f, 0f);
        ground.transform.localScale = new Vector3(42f, 1f, BoardMaxZ - BoardMinZ + 4f);
        ground.GetComponent<Renderer>().sharedMaterial =
            BlockBuilder.Mat(grassTex ? Color.white : new Color(0.35f, 0.65f, 0.3f), 0f, 0.1f, grassTex, new Vector2(10f, 6f));

        // Raised border wall made of bricks
        var wallColor = new Color(0.55f, 0.52f, 0.48f);
        for (float x = -20f; x <= 20f; x += 2f)
        {
            BlockBuilder.Brick(root, new Vector3(x, 0f, BoardMaxZ + 1.75f), 4, 1, 0.5f, wallColor);
            BlockBuilder.Brick(root, new Vector3(x, 0f, BoardMinZ - 1.75f), 4, 1, 0.5f, wallColor);
        }

        // ---------- Path (procedural ribbon mesh) ----------
        BuildRibbon(root, path, EnemyPath.HalfWidth + 0.3f, 0.01f, BlockBuilder.Mat(new Color(0.3f, 0.27f, 0.24f)), "PathEdge", 1f);
        BuildRibbon(root, path, EnemyPath.HalfWidth, 0.02f,
            BlockBuilder.Mat(pathTex ? Color.white : new Color(0.7f, 0.65f, 0.55f), 0f, 0.2f, pathTex), "Path", 2.2f);

        // ---------- Spawn gate (start) ----------
        Vector3 start = path.PositionAt(1.5f);
        Vector3 startDir = path.TangentAt(1.5f);
        BuildGate(root, start, startDir);

        // ---------- Temple (end) ----------
        TemplePosition = path.PositionAt(path.TotalLength);
        BuildTemple(root, TemplePosition + new Vector3(0.5f, 0f, 0f));

        // ---------- Scenery ----------
        var rng = new System.Random(42);
        var placed = new List<Vector3>();
        int attempts = 0;
        while (placed.Count < 46 && attempts < 3000)
        {
            attempts++;
            float x = Mathf.Lerp(BoardMinX + 0.5f, BoardMaxX - 0.5f, (float)rng.NextDouble());
            float z = Mathf.Lerp(BoardMinZ + 0.5f, BoardMaxZ - 0.5f, (float)rng.NextDouble());
            var p = new Vector3(x, 0f, z);
            if (path.DistanceTo(p) < EnemyPath.HalfWidth + 1.6f) continue;
            if (Vector3.Distance(p, TemplePosition) < 4f || Vector3.Distance(p, start) < 3f) continue;
            bool tooClose = false;
            foreach (var q in placed) if (Vector3.Distance(p, q) < 2.6f) { tooClose = true; break; }
            if (tooClose) continue;
            placed.Add(p);

            int kind = rng.Next(0, 10);
            if (kind < 4) Tree(root, p, new Color(0.2f, 0.55f, 0.22f), rng);
            else if (kind < 6) Tree(root, p, new Color(1f, 0.65f, 0.8f), rng);   // blossom tree
            else if (kind < 8) Rock(root, p, rng);
            else if (kind < 9) Lantern(root, p);
            else Bamboo(root, p, rng);
        }
    }

    static void BuildRibbon(Transform root, EnemyPath path, float halfWidth, float y, Material mat, string name, float uvLen)
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        float along = 0f;
        for (int i = 0; i < path.points.Count; i++)
        {
            Vector3 p = path.points[i];
            Vector3 fwd = i < path.points.Count - 1 ? path.points[i + 1] - p : p - path.points[i - 1];
            fwd.y = 0f; fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            if (i > 0) along += Vector3.Distance(path.points[i - 1], p);
            verts.Add(p - right * halfWidth + Vector3.up * y);
            verts.Add(p + right * halfWidth + Vector3.up * y);
            uvs.Add(new Vector2(0f, along / uvLen));
            uvs.Add(new Vector2(1f, along / uvLen));
            if (i < path.points.Count - 1)
            {
                int b = i * 2;
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                tris.Add(b + 1); tris.Add(b + 2); tris.Add(b + 3);
            }
        }
        var mesh = new Mesh { name = name + "Mesh" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static void BuildGate(Transform root, Vector3 pos, Vector3 dir)
    {
        var g = new GameObject("SpawnGate");
        g.transform.SetParent(root, false);
        g.transform.position = pos;
        g.transform.rotation = Quaternion.LookRotation(dir);
        var red = new Color(0.8f, 0.12f, 0.1f);
        var black = new Color(0.1f, 0.1f, 0.1f);
        BlockBuilder.Block(g.transform, new Vector3(-1.6f, 1.6f, 0f), new Vector3(0.4f, 3.2f, 0.4f), red);
        BlockBuilder.Block(g.transform, new Vector3(1.6f, 1.6f, 0f), new Vector3(0.4f, 3.2f, 0.4f), red);
        BlockBuilder.Block(g.transform, new Vector3(0f, 2.6f, 0f), new Vector3(3.6f, 0.25f, 0.3f), red);
        BlockBuilder.Block(g.transform, new Vector3(0f, 3.3f, 0f), new Vector3(4.6f, 0.35f, 0.5f), black);
        BlockBuilder.Block(g.transform, new Vector3(0f, 2.95f, 0f), new Vector3(0.3f, 0.5f, 0.3f), red);
        // glowing portal plane
        BlockBuilder.Block(g.transform, new Vector3(0f, 1.25f, 0f), new Vector3(2.8f, 2.4f, 0.05f), new Color(0.5f, 0.1f, 0.7f), 2.5f, false, "Portal");
    }

    static void BuildTemple(Transform root, Vector3 pos)
    {
        var t = new GameObject("Temple");
        t.transform.SetParent(root, false);
        t.transform.position = pos;
        t.AddComponent<Blocker>();
        var box = t.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 1.5f, 0f); box.size = new Vector3(4.5f, 3f, 4.5f);

        var stone = new Color(0.75f, 0.72f, 0.65f);
        var wall = new Color(0.95f, 0.9f, 0.78f);
        var pillar = new Color(0.75f, 0.1f, 0.1f);
        var roof = new Color(0.12f, 0.35f, 0.3f);
        var gold = new Color(1f, 0.8f, 0.2f);

        BlockBuilder.Block(t.transform, new Vector3(0f, 0.2f, 0f), new Vector3(5f, 0.4f, 5f), stone);
        BlockBuilder.Block(t.transform, new Vector3(0f, 0.5f, 0f), new Vector3(4.2f, 0.2f, 4.2f), stone);
        float y = 0.6f;
        float size = 3.4f;
        for (int tier = 0; tier < 3; tier++)
        {
            float h = 1.1f - tier * 0.15f;
            BlockBuilder.Block(t.transform, new Vector3(0f, y + h * 0.5f, 0f), new Vector3(size * 0.8f, h, size * 0.8f), wall);
            for (int c = 0; c < 4; c++)
            {
                float sx = (c % 2 == 0 ? -1 : 1) * size * 0.42f, sz = (c < 2 ? -1 : 1) * size * 0.42f;
                BlockBuilder.Block(t.transform, new Vector3(sx, y + h * 0.5f, sz), new Vector3(0.25f, h, 0.25f), pillar);
            }
            y += h;
            BlockBuilder.Block(t.transform, new Vector3(0f, y + 0.12f, 0f), new Vector3(size + 0.8f, 0.25f, size + 0.8f), roof);
            BlockBuilder.Block(t.transform, new Vector3(0f, y + 0.32f, 0f), new Vector3(size * 0.75f, 0.2f, size * 0.75f), roof);
            y += 0.42f;
            size *= 0.72f;
        }
        BlockBuilder.Cyl(t.transform, new Vector3(0f, y + 0.4f, 0f), new Vector3(0.18f, 0.4f, 0.18f), gold, 1.2f);
        BlockBuilder.Block(t.transform, new Vector3(0f, y + 0.95f, 0f), new Vector3(0.35f, 0.35f, 0.35f), gold, 2f);
        // door
        BlockBuilder.Block(t.transform, new Vector3(-1.37f, 1.1f, 0f), new Vector3(0.05f, 0.9f, 0.7f), new Color(0.25f, 0.12f, 0.05f));
    }

    static GameObject DecorRoot(Transform root, string name, Vector3 p, float radius, float height)
    {
        var d = new GameObject(name);
        d.transform.SetParent(root, false);
        d.transform.position = p;
        d.AddComponent<Blocker>();
        var c = d.AddComponent<BoxCollider>();
        c.center = new Vector3(0f, height * 0.5f, 0f);
        c.size = new Vector3(radius * 2f, height, radius * 2f);
        return d;
    }

    static void Tree(Transform root, Vector3 p, Color leaves, System.Random rng)
    {
        var d = DecorRoot(root, "Tree", p, 0.6f, 2.5f);
        float s = 0.85f + (float)rng.NextDouble() * 0.4f;
        d.transform.localScale = Vector3.one * s;
        d.transform.rotation = Quaternion.Euler(0f, rng.Next(0, 4) * 90f, 0f);
        BlockBuilder.Block(d.transform, new Vector3(0f, 0.6f, 0f), new Vector3(0.4f, 1.2f, 0.4f), new Color(0.4f, 0.25f, 0.12f));
        BlockBuilder.Block(d.transform, new Vector3(0f, 1.5f, 0f), new Vector3(1.6f, 0.6f, 1.6f), leaves);
        BlockBuilder.Block(d.transform, new Vector3(0f, 2.05f, 0f), new Vector3(1.1f, 0.5f, 1.1f), BlockBuilder.Darker(leaves, 0.9f));
        BlockBuilder.Block(d.transform, new Vector3(0f, 2.5f, 0f), new Vector3(0.6f, 0.4f, 0.6f), leaves);
    }

    static void Rock(Transform root, Vector3 p, System.Random rng)
    {
        var d = DecorRoot(root, "Rock", p, 0.6f, 0.8f);
        d.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 90f, 0f);
        var grey = new Color(0.5f, 0.5f, 0.52f);
        BlockBuilder.Block(d.transform, new Vector3(0f, 0.3f, 0f), new Vector3(1.1f, 0.6f, 0.9f), grey);
        BlockBuilder.Block(d.transform, new Vector3(0.2f, 0.7f, 0.1f), new Vector3(0.6f, 0.35f, 0.5f), BlockBuilder.Darker(grey, 0.85f));
        BlockBuilder.Brick(d.transform, new Vector3(-0.55f, 0f, 0.45f), 1, 1, 0.25f, new Color(0.3f, 0.6f, 0.25f));
    }

    static void Lantern(Transform root, Vector3 p)
    {
        var d = DecorRoot(root, "Lantern", p, 0.4f, 1.6f);
        var stone = new Color(0.6f, 0.6f, 0.58f);
        BlockBuilder.Block(d.transform, new Vector3(0f, 0.1f, 0f), new Vector3(0.7f, 0.2f, 0.7f), stone);
        BlockBuilder.Block(d.transform, new Vector3(0f, 0.6f, 0f), new Vector3(0.2f, 0.8f, 0.2f), stone);
        BlockBuilder.Block(d.transform, new Vector3(0f, 1.15f, 0f), new Vector3(0.45f, 0.35f, 0.45f), new Color(1f, 0.75f, 0.3f), 3f);
        BlockBuilder.Block(d.transform, new Vector3(0f, 1.45f, 0f), new Vector3(0.75f, 0.15f, 0.75f), stone);
        var lightGo = new GameObject("LanternLight");
        lightGo.transform.SetParent(d.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        var l = lightGo.AddComponent<Light>();
        l.type = LightType.Point; l.range = 4f; l.intensity = 1.5f; l.color = new Color(1f, 0.7f, 0.35f);
    }

    static void Bamboo(Transform root, Vector3 p, System.Random rng)
    {
        var d = DecorRoot(root, "Bamboo", p, 0.6f, 2.5f);
        var green = new Color(0.45f, 0.7f, 0.25f);
        for (int i = 0; i < 4; i++)
        {
            float h = 1.6f + (float)rng.NextDouble() * 1.2f;
            var pos = new Vector3(((float)rng.NextDouble() - 0.5f) * 0.9f, h * 0.5f, ((float)rng.NextDouble() - 0.5f) * 0.9f);
            BlockBuilder.Block(d.transform, pos, new Vector3(0.15f, h, 0.15f), green);
            BlockBuilder.Block(d.transform, pos + Vector3.up * (h * 0.1f), new Vector3(0.18f, 0.05f, 0.18f), BlockBuilder.Darker(green));
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  BlockBuilder.cs — helpers to build brick-style models out of blocks,
//  plus a material cache that works in both URP and the Built-in pipeline.
// ============================================================================
public static class BlockBuilder
{
    static Shader litShader;
    static Mesh cubeMesh, cylMesh;

    static Mesh PrimMesh(PrimitiveType t)
    {
        var go = GameObject.CreatePrimitive(t);
        var m = go.GetComponent<MeshFilter>().sharedMesh;
        Object.Destroy(go);
        return m;
    }
    public static Mesh CubeMesh => cubeMesh != null ? cubeMesh : (cubeMesh = PrimMesh(PrimitiveType.Cube));
    public static Mesh CylinderMesh => cylMesh != null ? cylMesh : (cylMesh = PrimMesh(PrimitiveType.Cylinder));

    /// Call once at startup so no primitive is ever created inside a physics callback.
    public static void Warmup() { var a = CubeMesh; var b = CylinderMesh; var c = LitShader; }

    /// A renderable object with no collider (cheap, and safe to create anywhere).
    public static GameObject MeshObject(Transform parent, Mesh mesh, Material mat, string name)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }
    static Shader lineShader;
    static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

    public static void ClearCache() { cache.Clear(); }

    static Shader LitShader
    {
        get
        {
            if (litShader == null)
            {
                // Grab whatever shader the render pipeline gives a default cube (URP Lit or Standard).
                var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var r = probe.GetComponent<Renderer>();
                if (r != null && r.sharedMaterial != null) litShader = r.sharedMaterial.shader;
                Object.Destroy(probe);
                if (litShader == null) litShader = Shader.Find("Universal Render Pipeline/Lit");
                if (litShader == null) litShader = Shader.Find("Standard");
            }
            return litShader;
        }
    }

    /// Solid coloured material. emission > 0 makes it glow.
    public static Material Mat(Color c, float emission = 0f, float smooth = 0.25f, Texture tex = null, Vector2? tiling = null)
    {
        string key = c.ToString() + "|" + emission + "|" + smooth + "|" + (tex ? tex.name : "") + "|" + tiling;
        if (cache.TryGetValue(key, out var m) && m != null) return m;

        m = new Material(LitShader);
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        if (tex != null)
        {
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            if (tiling.HasValue)
            {
                if (m.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", tiling.Value);
                if (m.HasProperty("_MainTex")) m.SetTextureScale("_MainTex", tiling.Value);
            }
        }
        if (emission > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * emission);
        }
        cache[key] = m;
        return m;
    }

    /// Material for LineRenderers / TrailRenderers (vertex-coloured, unlit).
    public static Material LineMat()
    {
        if (cache.TryGetValue("__line", out var m) && m != null) return m;
        if (lineShader == null) lineShader = Shader.Find("Sprites/Default");
        if (lineShader == null) lineShader = Shader.Find("Universal Render Pipeline/Unlit");
        m = lineShader != null ? new Material(lineShader) : Mat(Color.white, 1f);
        cache["__line"] = m;
        return m;
    }

    /// Adds a block (cube) child. Position/scale are local to `parent`.
    public static GameObject Block(Transform parent, Vector3 localPos, Vector3 scale, Color color,
                                   float emission = 0f, bool keepCollider = false, string name = "Block")
    {
        var go = MeshObject(parent, CubeMesh, Mat(color, emission), name);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        if (keepCollider) go.AddComponent<BoxCollider>();
        return go;
    }

    /// Adds a cylinder child (used for brick studs, pillars, lanterns).
    public static GameObject Cyl(Transform parent, Vector3 localPos, Vector3 scale, Color color,
                                 float emission = 0f, string name = "Cylinder")
    {
        var go = MeshObject(parent, CylinderMesh, Mat(color, emission), name);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        return go;
    }

    /// A toy brick: a block with round studs on top.
    public static GameObject Brick(Transform parent, Vector3 localPos, int studsX, int studsZ, float height, Color color,
                                   float unit = 0.5f, string name = "Brick")
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPos;
        float w = studsX * unit, d = studsZ * unit;
        Block(root.transform, new Vector3(0, height * 0.5f, 0), new Vector3(w, height, d), color);
        for (int x = 0; x < studsX; x++)
            for (int z = 0; z < studsZ; z++)
            {
                float px = -w * 0.5f + unit * (x + 0.5f);
                float pz = -d * 0.5f + unit * (z + 0.5f);
                Cyl(root.transform, new Vector3(px, height + unit * 0.1f, pz),
                    new Vector3(unit * 0.6f, unit * 0.1f, unit * 0.6f), color, 0f, "Stud");
            }
        return root;
    }

    public static Color Darker(Color c, float f = 0.6f) => new Color(c.r * f, c.g * f, c.b * f, c.a);
}

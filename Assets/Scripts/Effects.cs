using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  Effects.cs — visual juice: physics brick explosions, lightning arcs,
//  shockwaves, floating "+gold" / "LEVEL UP!" text, and sound playback.
// ============================================================================
public static class Effects
{
    public class FloatText
    {
        public Vector3 worldPos;
        public string text;
        public Color color;
        public float age, life, size;
    }
    public static readonly List<FloatText> Texts = new List<FloatText>();
    public static Transform Root;   // parent for spawned FX

    public static void Clear() { Texts.Clear(); }

    public static void Text(Vector3 pos, string text, Color color, float life = 1.2f, float size = 1f)
    {
        Texts.Add(new FloatText { worldPos = pos, text = text, color = color, life = life, size = size });
    }

    public static void Tick(float dt)
    {
        for (int i = Texts.Count - 1; i >= 0; i--)
        {
            var t = Texts[i];
            t.age += dt;
            t.worldPos += Vector3.up * dt * 1.2f;
            if (t.age >= t.life) Texts.RemoveAt(i);
        }
    }

    /// Enemy shatters into physics-driven bricks that bounce on the ground then shrink away.
    public static void BrickExplosion(Vector3 pos, Color a, Color b, int count, float size)
    {
        for (int i = 0; i < count; i++)
        {
            var go = BlockBuilder.MeshObject(Root, BlockBuilder.CubeMesh, BlockBuilder.Mat(i % 3 == 0 ? b : a), "Debris");
            go.transform.position = pos + Random.insideUnitSphere * 0.3f * size;
            go.transform.rotation = Random.rotation;
            float s = Random.Range(0.12f, 0.25f) * size;
            go.transform.localScale = new Vector3(s, s * 0.6f, s);
            go.AddComponent<BoxCollider>();
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.1f;
            rb.AddForce(new Vector3(Random.Range(-1f, 1f), Random.Range(1.5f, 3f), Random.Range(-1f, 1f)) * 2.2f, ForceMode.VelocityChange);
            rb.AddTorque(Random.insideUnitSphere * 10f, ForceMode.VelocityChange);
            go.AddComponent<ShrinkAndDie>().delay = Random.Range(1.2f, 2f);
        }
    }

    /// Small burst of glowing cubes when a projectile hits.
    public static void HitSpark(Vector3 pos, Color c, int count = 5)
    {
        for (int i = 0; i < count; i++)
        {
            var go = BlockBuilder.MeshObject(Root, BlockBuilder.CubeMesh, BlockBuilder.Mat(c, 3f), "Spark");
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.1f;
            var f = go.AddComponent<Fly>();
            f.velocity = Random.onUnitSphere * Random.Range(2f, 4f) + Vector3.up * 1.5f;
            f.life = 0.35f;
        }
    }

    /// Expanding flat ring used for the earth splash.
    public static void Shockwave(Vector3 pos, float radius, Color c)
    {
        var go = BlockBuilder.MeshObject(Root, BlockBuilder.CylinderMesh, BlockBuilder.Mat(c, 1.5f), "Shockwave");
        go.transform.position = new Vector3(pos.x, 0.08f, pos.z);
        var g = go.AddComponent<GrowRing>();
        g.targetRadius = radius;
    }

    /// Jagged lightning line between two points.
    public static void Bolt(Vector3 from, Vector3 to, Color c)
    {
        var go = new GameObject("Bolt");
        if (Root) go.transform.SetParent(Root, true);
        var lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = BlockBuilder.LineMat();
        lr.startColor = Color.white; lr.endColor = c;
        lr.startWidth = 0.14f; lr.endWidth = 0.06f;
        const int segs = 8;
        lr.positionCount = segs + 1;
        for (int i = 0; i <= segs; i++)
        {
            Vector3 p = Vector3.Lerp(from, to, i / (float)segs);
            if (i > 0 && i < segs) p += Random.insideUnitSphere * 0.3f;
            lr.SetPosition(i, p);
        }
        Object.Destroy(go, 0.15f);
    }

    // ------------------------------------------------------------- audio
    static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    public static AudioSource Sfx;
    public static float SfxVolume = 0.55f;
    static readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();

    public static void Play(string name, float volume = 1f, float pitchJitter = 0.08f)
    {
        if (Sfx == null) return;
        // avoid machine-gun stacking of the same sound in one instant
        if (lastPlayed.TryGetValue(name, out var t) && Time.unscaledTime - t < 0.04f) return;
        lastPlayed[name] = Time.unscaledTime;
        if (!clips.TryGetValue(name, out var clip))
        {
            clip = Resources.Load<AudioClip>("Audio/" + name);
            clips[name] = clip;
        }
        if (clip == null) return;
        Sfx.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        Sfx.PlayOneShot(clip, volume * SfxVolume);
    }
}

public class ShrinkAndDie : MonoBehaviour
{
    public float delay = 1.5f;
    float t;
    Vector3 start;
    void Start() { start = transform.localScale; }
    void Update()
    {
        t += Time.deltaTime;
        if (t > delay)
        {
            float k = 1f - (t - delay) / 0.4f;
            if (k <= 0f) { Destroy(gameObject); return; }
            transform.localScale = start * k;
        }
    }
}

public class Fly : MonoBehaviour
{
    public Vector3 velocity;
    public float life = 0.4f;
    float t;
    Vector3 s0;
    void Start() { s0 = transform.localScale; }
    void Update()
    {
        t += Time.deltaTime;
        velocity += Physics.gravity * Time.deltaTime * 0.5f;
        transform.position += velocity * Time.deltaTime;
        transform.localScale = s0 * Mathf.Max(0f, 1f - t / life);
        if (t >= life) Destroy(gameObject);
    }
}

public class GrowRing : MonoBehaviour
{
    public float targetRadius = 2f;
    float t;
    void Update()
    {
        t += Time.deltaTime / 0.3f;
        float r = Mathf.Lerp(0.2f, targetRadius, t) * 2f;
        transform.localScale = new Vector3(r, 0.02f * (1f - t) + 0.005f, r);
        if (t >= 1f) Destroy(gameObject);
    }
}

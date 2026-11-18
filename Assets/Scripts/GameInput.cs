using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  GameInput.cs — tiny input layer fed by IMGUI events (see GameHUD.OnGUI).
//  Using IMGUI events means the game works no matter which input backend
//  (old Input Manager or the new Input System package) the project uses.
// ============================================================================
public static class GameInput
{
    static readonly HashSet<KeyCode> held = new HashSet<KeyCode>();

    /// Mouse position in SCREEN pixels (origin bottom-left, like Input.mousePosition).
    public static Vector2 MouseScreen;
    public static float Scroll;               // accumulated wheel delta since last consume

    public static bool Held(KeyCode k) => held.Contains(k);

    /// Returns true the first time a key goes down (ignores OS key-repeat).
    public static bool Press(KeyCode k)
    {
        if (held.Contains(k)) return false;
        held.Add(k);
        return true;
    }

    public static void Release(KeyCode k) { held.Remove(k); }
    public static void ReleaseAll() { held.Clear(); }

    public static float ConsumeScroll()
    {
        float s = Scroll;
        Scroll = 0f;
        return s;
    }
}

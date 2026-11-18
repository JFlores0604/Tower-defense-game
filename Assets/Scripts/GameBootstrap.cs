using UnityEngine;

// ============================================================================
//  GameBootstrap.cs — just press Play in ANY scene (e.g. SampleScene) and the
//  whole game builds itself. If you'd rather tune values in the Inspector,
//  add a GameManager component to an object in your scene and this will
//  use that one instead of creating its own.
// ============================================================================
public static class GameBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Object.FindAnyObjectByType<GameManager>() != null) return;
        new GameObject("GameManager").AddComponent<GameManager>();
    }
}

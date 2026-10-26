using UnityEngine;

namespace SkiView
{
    /// <summary>Pass 18: the core sound pack (James's manifest.json minus _raw/_tools and the per-lake beds), filled by the
    /// editor builder into Resources/SkiViewAudio/Bank.asset. Loop points come from the manifest (loop_start/loop_end samples,
    /// the files' LOOPSTART tags); every loop in this pack runs the whole file today.</summary>
    public class SkiAudioBank : ScriptableObject
    {
        [System.Serializable] public class Entry { public string id; public AudioClip clip; public float lufs; public int loopStart, loopEnd; public bool loop; }
        public Entry[] entries = new Entry[0];
        public Entry Get(string id) { foreach (var e in entries) if (e.id == id) return e; return null; }
    }
}

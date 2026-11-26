using System.Linq;
using UnityEngine;

namespace OnlyUp
{
    /// <summary>O'yindagi barcha tovushlar ro'yxati.</summary>
    public class SoundBank : ScriptableObject
    {
        public AudioClip[] steps;
        public AudioClip jump, land, bounce, grab, climb, splash, click, victory;
        public AudioClip wind, sea, slide, music;

        public AudioClip[] All => (steps ?? new AudioClip[0])
            .Concat(new[] { jump, land, bounce, grab, climb, splash, click, victory, wind, sea, slide, music })
            .ToArray();

        public bool IsComplete => steps != null && steps.Length > 0 && All.All(clip => clip != null);
    }
}

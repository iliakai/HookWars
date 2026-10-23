using UnityEngine;

// Optional presentation: listens to the actual gameplay release, never to input.
[DefaultExecutionOrder(1100)]
[DisallowMultipleComponent, RequireComponent(typeof(HookAbility), typeof(AudioSource))]
public sealed class HookFlightAudio : MonoBehaviour
{
    [SerializeField] private AudioClip flightClip;
    [SerializeField] private Vector2 pitchRange = new Vector2(.93f, 1.07f);
    [SerializeField] private Vector2 volumeRange = new Vector2(.6f, .75f);
    private HookAbility hook;
    private AudioSource source;
    private System.Random variation;
    private bool paused;
    public int PlaybackCount { get; private set; }
    private void OnEnable()
    {
        hook = GetComponent<HookAbility>(); source = GetComponent<AudioSource>();
        variation ??= new System.Random(System.Guid.NewGuid().GetHashCode());
        source.playOnAwake = false; source.loop = false; source.spatialBlend = 0;
        source.dopplerLevel = 0; source.ignoreListenerPause = false;
        hook.HookReleased += Play;
    }
    private float Choose(Vector2 range, float minimum, float maximum)
    {
        float low = Mathf.Clamp(Mathf.Min(range.x, range.y), minimum, maximum);
        float high = Mathf.Clamp(Mathf.Max(range.x, range.y), minimum, maximum);
        return Mathf.Lerp(low, high, (float)variation.NextDouble());
    }
    private void Play()
    {
        if (flightClip == null || !isActiveAndEnabled || hook.ActiveHook == null) return;
        source.Stop(); paused = false;
        source.clip = flightClip;
        source.pitch = Choose(pitchRange, .1f, 3);
        source.volume = Choose(volumeRange, 0, 1);
        source.Play(); PlaybackCount++;
    }
    private void LateUpdate()
    {
        if (hook.ActiveHook == null) { source.Stop(); paused = false; return; }
        if (MatchPause.BlocksGameplay)
        {
            if (!paused && source.isPlaying) { source.Pause(); paused = true; }
        }
        else if (paused) { source.UnPause(); paused = false; }
    }
    private void OnDisable()
    {
        if (hook != null) hook.HookReleased -= Play;
        if (source != null) source.Stop();
        paused = false;
    }
}

using System.Threading;
using UnityEngine;

[RequireComponent(typeof(CarController))]
[DisallowMultipleComponent]
public sealed class EngineAudio : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

    private CarController car;
    private AudioSource source;
    private AudioClip clip;
    private EngineSynthesizer synthesizer;
    private float rpm;
    private float load;
    private float gain;

    private void Awake()
    {
        car = GetComponent<CarController>();
        int rate = Mathf.Max(24000, AudioSettings.outputSampleRate);
        synthesizer = new EngineSynthesizer(rate, CarController.RedlineRpm);
        clip = AudioClip.Create("Procedural engine", rate * 2, 1, rate, true, ReadAudio);

        var emitter = new GameObject("Engine sound");
        emitter.transform.SetParent(transform, false);
        emitter.transform.localPosition = new Vector3(0f, 0.75f, 1.45f);
        source = emitter.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.clip = clip;
        source.loop = true;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.minDistance = 8f;
        source.maxDistance = 75f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.volume = volume;
    }

    private void OnEnable()
    {
        UpdateControls();
        source.Play();
    }

    private void Update()
    {
        UpdateControls();
    }

    private void UpdateControls()
    {
        Volatile.Write(ref rpm, car.EngineRpm);
        Volatile.Write(ref load, car.EngineLoad);
        Volatile.Write(ref gain, car.IsDriving ? (GameManager.paused ? 0.18f : 1f) : 0f);
    }

    private void OnDisable()
    {
        source.Stop();
    }

    private void OnDestroy()
    {
        if (source != null)
            Destroy(source.gameObject);
        if (clip != null)
            Destroy(clip);
    }

    private void ReadAudio(float[] samples)
    {
        synthesizer.Render(samples, Volatile.Read(ref rpm), Volatile.Read(ref load), Volatile.Read(ref gain));
    }
}

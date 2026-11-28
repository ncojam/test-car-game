using UnityEngine;

[RequireComponent(typeof(CarController))]
[DisallowMultipleComponent]
public sealed class EngineAudio : MonoBehaviour
{
    [Header("Семпл кика")]
    [Tooltip("У семпла в настройках импорта Unity должен стоять Load Type = Decompress On Load.")]
    [SerializeField] private AudioClip kickSample;

    [Header("Настройка цилиндров и нот")]
    [Tooltip("Сдвиг в полутонах для каждого такта зажигания (например, V8: 0 и -2 для левого/правого ряда).")]
    [SerializeField] private float[] firingOrderSemitones = { 0f, -2f, -2f, 0f, -2f, 0f, 0f, -2f };
    [SerializeField, Range(-24f, 24f)] private float basePitchSemitones = 0f;
    [SerializeField, Range(0f, 12f)] private float rpmPitchRiseSemitones = 2.5f;
    [Tooltip("Вспышек за 1 оборот коленвала (4 для V8, 6 для V12, 2 для рядной четверки).")]
    [SerializeField, Range(1f, 8f)] private float firingsPerRevolution = 4f;

    [Header("Обработка и громкость")]
    [SerializeField, Range(0f, 1f)] private float volume = 0.85f;
    [SerializeField, Range(0.5f, 5f)] private float distortionDrive = 1.6f;
    [SerializeField, Range(0f, 1f)] private float supercharger = 0.16f;

    [SerializeField, Range(0f, 1f)] private float exhaustResonance = 0.65f;

    private CarController car;
    private AudioSource source;
    private AudioClip carrier;
    private EngineAudioOutput output;

    private void Awake()
    {
        car = GetComponent<CarController>();
        int rate = AudioSettings.outputSampleRate;

        float[] kickPcm = ExtractMonoPcm(kickSample, out int kickRate);
        var synthesizer = new EngineSynthesizer(
            rate,
            CarController.RedlineRpm,
            kickPcm,
            kickRate,
            firingOrderSemitones,
            basePitchSemitones,
            rpmPitchRiseSemitones,
            distortionDrive,
            supercharger,
            firingsPerRevolution,
            exhaustResonance);

        // A silent non-streaming carrier keeps a spatial AudioSource on the DSP graph.
        // The filter replaces its samples immediately before playback.
        carrier = AudioClip.Create("Engine DSP carrier", 1024, 1, rate, false);

        var emitter = new GameObject("Engine sound");
        emitter.transform.SetParent(transform, false);
        emitter.transform.localPosition = new Vector3(0f, 0.75f, 1.45f);
        source = emitter.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.clip = carrier;
        source.loop = true;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.minDistance = 8f;
        source.maxDistance = 75f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.volume = volume;
        output = emitter.AddComponent<EngineAudioOutput>();
        output.Initialize(synthesizer);
    }

    private static float[] ExtractMonoPcm(AudioClip sample, out int sampleRate)
    {
        if (sample == null)
        {
            sampleRate = 44100;
            return System.Array.Empty<float>();
        }

        if (sample.loadState == AudioDataLoadState.Unloaded)
            sample.LoadAudioData();

        sampleRate = sample.frequency;
        int channels = sample.channels;
        float[] raw = new float[sample.samples * channels];
        if (!sample.GetData(raw, 0))
        {
            Debug.LogWarning($"Engine kick '{sample.name}' could not be read. Use Decompress On Load.");
            return System.Array.Empty<float>();
        }

        if (channels == 1)
            return raw;

        float[] mono = new float[sample.samples];
        for (int i = 0; i < sample.samples; i++)
        {
            float sum = 0f;
            int offset = i * channels;
            for (int c = 0; c < channels; c++)
                sum += raw[offset + c];
            mono[i] = sum / channels;
        }
        return mono;
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
        output.SetControls(car.EngineRpm, car.EngineLoad, car.IsDriving ? (GameManager.paused ? 0.18f : 1f) : 0f);
    }

    private void OnDisable()
    {
        source.Stop();
    }

    private void OnDestroy()
    {
        if (source != null)
            Destroy(source.gameObject);
        if (carrier != null)
            Destroy(carrier);
    }
}

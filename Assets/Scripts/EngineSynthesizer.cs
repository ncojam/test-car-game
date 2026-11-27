using System;

internal sealed class EngineSynthesizer
{
    private const int MaxVoices = 16;

    private struct Voice
    {
        public bool Active;
        public double Position;
        public double Step;
        public float Gain;
    }

    private readonly float outputRate;
    private readonly float limiterRpm;
    private readonly float controlsBlend;
    private readonly float gainBlend;
    private readonly float blowerMix;
    private readonly float drive;

    private readonly float[] sampleData;
    private readonly float sampleRateRatio;
    private readonly float[] pitchRatios;
    private readonly Voice[] voices = new Voice[MaxVoices];

    private int nextVoice;
    private int cylinder;
    private double phase;
    private double blowerPhase;
    private float limiterPhase;
    private float smoothedRpm = 900f;
    private float smoothedLoad;
    private float smoothedGain;
    private float rpmPitchFactor;
    private float firingsPerRev;
    private uint random = 0xc1a83f45;

    public EngineSynthesizer(
        int outputSampleRate,
        float redlineRpm,
        float[] kickSamples,
        int kickSampleRate,
        float[] semitonePattern,
        float basePitchSemitones,
        float rpmPitchShift,
        float distortionDrive,
        float supercharger,
        float firingsPerRevolution)
    {
        outputRate = outputSampleRate;
        limiterRpm = redlineRpm - 35f;
        controlsBlend = 1f - Decay(0.02f);
        gainBlend = 1f - Decay(0.03f);
        blowerMix = supercharger;
        drive = Math.Max(0.1f, distortionDrive);
        rpmPitchFactor = rpmPitchShift;
        firingsPerRev = Math.Max(0.5f, firingsPerRevolution);

        sampleData = kickSamples ?? Array.Empty<float>();
        sampleRateRatio = kickSampleRate > 0 ? (float)kickSampleRate / outputSampleRate : 1f;

        if (semitonePattern == null || semitonePattern.Length == 0)
            semitonePattern = new[] { 0f, -2f, -2f, 0f, -2f, 0f, 0f, -2f };

        pitchRatios = new float[semitonePattern.Length];
        for (int i = 0; i < semitonePattern.Length; i++)
            pitchRatios[i] = (float)Math.Pow(2d, (semitonePattern[i] + basePitchSemitones) / 12d);
    }

    public void Render(float[] samples, float rpm, float load, float gain)
    {
        rpm = Math.Max(650f, rpm);
        bool hasSample = sampleData.Length > 1;

        for (int i = 0; i < samples.Length; i++)
        {
            smoothedRpm += (rpm - smoothedRpm) * controlsBlend;
            smoothedLoad += (load - smoothedLoad) * controlsBlend;
            smoothedGain += (gain - smoothedGain) * gainBlend;

            if (smoothedRpm >= limiterRpm && smoothedLoad > 0.65f)
            {
                limiterPhase += 13f / outputRate;
                if (limiterPhase >= 1f)
                    limiterPhase -= 1f;
            }
            else
            {
                limiterPhase = 0f;
            }

            // Частота зажигания: (RPM / 60) * количество вспышек за оборот
            double firingStep = (smoothedRpm / 60d) * firingsPerRev / outputRate;
            phase += firingStep;
            if (phase >= 1d)
            {
                phase -= 1d;
                if (hasSample && limiterPhase <= 0.60f)
                {
                    float rpmNorm = Math.Min(1f, smoothedRpm / limiterRpm);
                    float rpmPitch = (float)Math.Pow(2d, (rpmNorm * rpmPitchFactor) / 12d);
                    float jitter = 0.985f + 0.03f * WhiteNoise01();

                    voices[nextVoice] = new Voice
                    {
                        Active = true,
                        Position = 0d,
                        Step = sampleRateRatio * pitchRatios[cylinder] * rpmPitch * jitter,
                        Gain = 0.72f + 0.38f * smoothedLoad
                    };
                    nextVoice = (nextVoice + 1) % MaxVoices;
                }
                cylinder = (cylinder + 1) % pitchRatios.Length;
            }

            float mixed = 0f;
            if (hasSample)
            {
                int lastIndex = sampleData.Length - 1;
                for (int v = 0; v < MaxVoices; v++)
                {
                    if (!voices[v].Active)
                        continue;

                    double pos = voices[v].Position;
                    int idx = (int)pos;
                    if (idx >= lastIndex)
                    {
                        voices[v].Active = false;
                        continue;
                    }

                    float frac = (float)(pos - idx);
                    float s = sampleData[idx] + (sampleData[idx + 1] - sampleData[idx]) * frac;
                    mixed += s * voices[v].Gain;
                    voices[v].Position = pos + voices[v].Step;
                }
            }

            // Свист нагнетателя (Supercharger)
            float brightness = Math.Min(1f, smoothedRpm / limiterRpm);
            blowerPhase += smoothedRpm * (14d / 60d) / outputRate;
            if (blowerPhase >= 1d)
                blowerPhase -= 1d;
            float x = (float)blowerPhase * 2f - 1f;
            float whine = 4f * x * (1f - Math.Abs(x)) * brightness * brightness * smoothedLoad * blowerMix;

            // Мягкий дисторшн (насыщение, как в FL Studio)
            float driven = (mixed * 0.55f + whine) * drive;
            float saturated = (float)Math.Tanh(driven);
            samples[i] = smoothedGain * saturated;
        }
    }

    private float Decay(float seconds) => (float)Math.Exp(-1d / (outputRate * seconds));

    private float WhiteNoise01()
    {
        random ^= random << 13;
        random ^= random >> 17;
        random ^= random << 5;
        return (random >> 8) * (1f / 16777216f);
    }
}
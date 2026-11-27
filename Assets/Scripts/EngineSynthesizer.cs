using System;

internal sealed class EngineSynthesizer
{
    // Relative pulse strengths in firing order. A four-stroke six fires three times per crank revolution.
    private static readonly float[] Cylinders = { 1.00f, 0.91f, 1.04f, 0.96f, 1.02f, 0.90f };

    private readonly float rate;
    private readonly float limiterRpm;
    private readonly float controlsBlend;
    private readonly float gainBlend;
    private readonly float attackDecay;
    private readonly float pressureDecay;
    private readonly float valveDecay;
    private Resonance exhaust;
    private Resonance exhaustHarmonic;
    private Resonance intake;
    private Resonance valve;
    private double phase;
    private float limiterPhase;
    private int cylinder;
    private float fastPressure;
    private float slowPressure;
    private float valveEnvelope;
    private float smoothedRpm = 900f;
    private float smoothedLoad;
    private float smoothedGain;
    private float noiseFloor;
    private uint random = 0xc1a83f45;

    public EngineSynthesizer(int sampleRate, float redlineRpm)
    {
        rate = sampleRate;
        limiterRpm = redlineRpm - 35f;
        controlsBlend = 1f - Decay(0.024f);
        gainBlend = 1f - Decay(0.035f);
        attackDecay = Decay(0.00024f);
        pressureDecay = Decay(0.0015f);
        valveDecay = Decay(0.0009f);
        exhaust = new Resonance(rate, 112f, 0.95f);
        exhaustHarmonic = new Resonance(rate, 290f, 1.02f);
        intake = new Resonance(rate, 780f, 1.15f);
        valve = new Resonance(rate, 2180f, 0.85f);
    }

    public void Render(float[] samples, float rpm, float load, float gain)
    {
        rpm = Math.Max(700f, rpm);
        for (int i = 0; i < samples.Length; i++)
        {
            smoothedRpm += (rpm - smoothedRpm) * controlsBlend;
            smoothedLoad += (load - smoothedLoad) * controlsBlend;
            smoothedGain += (gain - smoothedGain) * gainBlend;
            float noise = WhiteNoise();

            if (smoothedRpm >= limiterRpm && smoothedLoad > 0.65f)
            {
                limiterPhase += 13f / rate;
                if (limiterPhase >= 1f)
                    limiterPhase -= 1f;
            }
            else
            {
                limiterPhase = 0f;
            }

            // Advance by the firing frequency: RPM / 60 revolutions per second, three firings per revolution.
            float firingStep = smoothedRpm * 0.05f / rate;
            phase += firingStep;
            if (phase >= 1d)
            {
                phase -= 1d;
                float pulse = Cylinders[cylinder] * (0.86f + 0.12f * noise + 0.24f * smoothedLoad);
                cylinder = (cylinder + 1) % Cylinders.Length;
                if (limiterPhase > 0.60f)
                    pulse *= 0.16f;

                // Age the pressure pulse by the fractional time elapsed since this firing.
                float fraction = (float)phase / firingStep;
                fastPressure += pulse * (1f - fraction * (1f - attackDecay));
                slowPressure += pulse * (1f - fraction * (1f - pressureDecay));
                valveEnvelope += pulse;
            }

            fastPressure *= attackDecay;
            slowPressure *= pressureDecay;
            valveEnvelope *= valveDecay;
            float pressure = slowPressure - fastPressure;
            noiseFloor += (noise - noiseFloor) * 0.14f;

            float exhaustSound = 0.8f * exhaust.Run(pressure) + 0.46f * exhaustHarmonic.Run(pressure);
            float intakeSound = intake.Run(pressure + noiseFloor * valveEnvelope * 0.32f);
            float mechanical = valve.Run(noise * valveEnvelope);
            float mixed = exhaustSound * (0.94f + smoothedLoad * 0.46f)
                + intakeSound * (0.52f + smoothedLoad * 1.15f) + mechanical * 0.12f;
            samples[i] = smoothedGain * mixed / (1f + 0.74f * Math.Abs(mixed));
        }
    }

    private float Decay(float seconds)
    {
        return (float)Math.Exp(-1d / (rate * seconds));
    }

    private float WhiteNoise()
    {
        random ^= random << 13;
        random ^= random >> 17;
        random ^= random << 5;
        return (random >> 8) * (1f / 8388608f) - 1f;
    }

    private struct Resonance
    {
        private readonly float b0;
        private readonly float a1;
        private readonly float a2;
        private float first;
        private float second;

        public Resonance(float sampleRate, float hz, float q)
        {
            double angle = 2d * Math.PI * hz / sampleRate;
            double alpha = Math.Sin(angle) / (2d * q);
            double a0 = 1d + alpha;
            b0 = (float)(alpha / a0);
            a1 = (float)(-2d * Math.Cos(angle) / a0);
            a2 = (float)((1d - alpha) / a0);
            first = second = 0f;
        }

        public float Run(float input)
        {
            float output = b0 * input + first;
            first = second - a1 * output;
            second = -b0 * input - a2 * output;
            return output;
        }
    }
}

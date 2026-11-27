using System;

internal sealed class EngineSynthesizer
{
    // Eight firings per two revolutions. Signed strengths select the two exhaust banks.
    private static readonly float[] Cylinders = { 1.00f, -0.94f, -1.02f, 0.97f, -1.03f, 0.92f, 1.04f, -0.96f };

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
    private Resonance bankTone;
    private float bankFast;
    private float bankSlow;
    private Resonance valve;
    private readonly float blowerMix;
    private double blowerPhase;
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

    public EngineSynthesizer(int sampleRate, float redlineRpm, float supercharger = 0.16f)
    {
        rate = sampleRate;
        blowerMix = supercharger;
        limiterRpm = redlineRpm - 35f;
        controlsBlend = 1f - Decay(0.024f);
        gainBlend = 1f - Decay(0.035f);
        attackDecay = Decay(0.00024f);
        pressureDecay = Decay(0.0015f);
        valveDecay = Decay(0.0009f);
        exhaust = new Resonance(rate, 112f, 0.95f);
        exhaustHarmonic = new Resonance(rate, 290f, 1.02f);
        bankTone = new Resonance(rate, 173f, 1.35f);
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

            // Advance by the firing frequency: RPM / 60 revolutions per second, four firings per revolution.
            float firingStep = smoothedRpm / (15f * rate);
            phase += firingStep;
            if (phase >= 1d)
            {
                phase -= 1d;
                float bank = Cylinders[cylinder];
                float pulse = Math.Abs(bank) * (0.86f + 0.12f * noise + 0.24f * smoothedLoad);
                cylinder = (cylinder + 1) % Cylinders.Length;
                if (limiterPhase > 0.60f)
                    pulse *= 0.16f;

                // Age the pressure pulse by the fractional time elapsed since this firing.
                float fraction = (float)phase / firingStep;
                fastPressure += pulse * (1f - fraction * (1f - attackDecay));
                slowPressure += pulse * (1f - fraction * (1f - pressureDecay));
                float bankPulse = bank < 0f ? -pulse : pulse;
                bankFast += bankPulse;
                bankSlow += bankPulse;
                valveEnvelope += pulse;
            }

            fastPressure *= attackDecay;
            slowPressure *= pressureDecay;
            valveEnvelope *= valveDecay;
            bankFast *= attackDecay;
            bankSlow *= pressureDecay;
            float pressure = slowPressure - fastPressure;
            noiseFloor += (noise - noiseFloor) * 0.14f;

            float exhaustSound = 0.8f * exhaust.Run(pressure) + 0.46f * exhaustHarmonic.Run(pressure);
            float intakeSound = intake.Run(pressure + noiseFloor * valveEnvelope * 0.32f);
            float mechanical = valve.Run(noise * valveEnvelope);
            float brightness = Math.Min(1f, smoothedRpm / limiterRpm);
            float bankSound = bankTone.Run(bankSlow - bankFast);
            // A geared blower tracks crank speed; its upper tone emerges under load.
            blowerPhase += smoothedRpm * (14d / 60d) / rate;
            if (blowerPhase >= 1d)
                blowerPhase -= 1d;
            float x = (float)blowerPhase * 2f - 1f;
            float whine = 4f * x * (1f - Math.Abs(x)) * brightness * brightness * smoothedLoad * blowerMix;
            float mixed = whine + 0.40f * bankSound + exhaustSound * (0.94f + smoothedLoad * 0.46f)
                + intakeSound * (0.38f + smoothedLoad * 0.85f + brightness * 0.52f) + mechanical * 0.12f;
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

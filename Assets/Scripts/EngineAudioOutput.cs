using System.Threading;
using UnityEngine;

public sealed class EngineAudioOutput : MonoBehaviour
{
    private EngineSynthesizer synthesizer;
    private float rpm;
    private float load;
    private float gain;

    internal void Initialize(EngineSynthesizer engine)
    {
        Volatile.Write(ref synthesizer, engine);
    }

    internal void SetControls(float engineRpm, float engineLoad, float engineGain)
    {
        Volatile.Write(ref rpm, engineRpm);
        Volatile.Write(ref load, engineLoad);
        Volatile.Write(ref gain, engineGain);
    }

    private void OnAudioFilterRead(float[] samples, int channels)
    {
        Volatile.Read(ref synthesizer)?.Render(samples, channels, Volatile.Read(ref rpm),
            Volatile.Read(ref load), Volatile.Read(ref gain));
    }
}

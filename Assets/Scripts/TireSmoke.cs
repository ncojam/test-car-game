using UnityEngine;
using UnityEngine.Rendering;

internal sealed class TireSmoke
{
    private readonly ParticleSystem particles;
    private float pending;

    public TireSmoke(Transform wheel, Material material)
    {
        if (material == null)
            return;

        GameObject emitter = new GameObject("Tire Smoke");
        emitter.transform.SetParent(wheel, false);
        particles = emitter.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = particles.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 2.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.55f, 1.05f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
        main.startSpeed = 0f;
        main.maxParticles = 600;

        var emission = particles.emission;
        emission.enabled = false;
        var shape = particles.shape;
        shape.enabled = false;

        var color = particles.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(0.88f, 0.88f, 0.88f), 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.86f, 0.12f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;

        var size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.65f, 1f, 3.6f));

        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        particles.Play();
    }

    public void Update(WheelHit contact, float amount, float step)
    {
        if (particles == null || amount <= 0f)
        {
            pending = 0f;
            return;
        }

        pending += Mathf.Sqrt(amount) * 205f * step;
        int count = Mathf.FloorToInt(pending);
        pending -= count;

        for (int i = 0; i < count; i++)
        {
            Vector3 spread = Random.insideUnitSphere * 0.22f;
            particles.Emit(new ParticleSystem.EmitParams
            {
                position = contact.point + contact.normal * 0.16f + spread,
                velocity = contact.normal * Random.Range(0.75f, 1.55f)
                    - contact.forwardDir * Random.Range(0.35f, 1.15f) + spread * 1.4f,
            }, 1);
        }
    }
}
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
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 1.35f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.38f, 0.64f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
        main.startSpeed = 0f;
        main.maxParticles = 100;

        var emission = particles.emission;
        emission.enabled = false;
        var shape = particles.shape;
        shape.enabled = false;

        var color = particles.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(0.85f, 0.85f, 0.85f), 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.58f, 0.16f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;

        var size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.55f, 1f, 2.7f));

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

        pending += amount * 42f * step;
        int count = Mathf.FloorToInt(pending);
        pending -= count;
        for (int i = 0; i < count; i++)
        {
            Vector3 spread = Random.insideUnitSphere * 0.13f;
            particles.Emit(new ParticleSystem.EmitParams
            {
                position = contact.point + contact.normal * 0.15f + spread,
                velocity = contact.normal * Random.Range(0.45f, 0.9f)
                    - contact.forwardDir * Random.Range(0.15f, 0.65f) + spread,
            }, 1);
        }
    }
}

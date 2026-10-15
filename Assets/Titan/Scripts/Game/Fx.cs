using System.Collections.Generic;
using UnityEngine;

namespace Titan.Game
{
    /// <summary>Procedural sounds (no audio files), camera shake and simple cube particles.</summary>
    public sealed class Fx : MonoBehaviour
    {
        AudioSource source;
        AudioClip shot, hit, step, crash, roar, fanfare, lose, deploy;
        float lastShot, lastHit;

        Camera cam;
        Vector3 cameraBase;
        float shakeLeft, shakeDuration, shakeAmplitude;

        sealed class Particle
        {
            public Transform T;
            public Vector3 Velocity;
            public float Life, MaxLife, Size;
        }

        readonly List<Particle> particles = new List<Particle>();
        Mesh cubeMesh;

        public void Init(Camera camera)
        {
            cam = camera;
            cameraBase = cam.transform.position;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;

            shot = Synth("shot", 0.09f, (t, k) => Square(Mathf.Lerp(1100f, 450f, k), t) * (1f - k) * 0.35f);
            hit = Synth("hit", 0.07f, (t, k) => Noise() * (1f - k) * 0.5f);
            deploy = Synth("deploy", 0.12f, (t, k) => Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(300f, 700f, k) * t) * (1f - k) * 0.5f);
            step = Synth("step", 0.28f, (t, k) => (Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.9f + Noise() * 0.15f) * Mathf.Exp(-t * 14f));
            crash = Synth("crash", 0.8f, (t, k) => (Noise() * 0.7f + Mathf.Sin(2f * Mathf.PI * 45f * t) * 0.6f) * Mathf.Exp(-t * 5f));
            roar = Synth("roar", 1.0f, (t, k) => (Saw(Mathf.Lerp(110f, 55f, k), t) * 0.5f + Noise() * 0.25f) * Mathf.Sin(Mathf.PI * k) * 0.6f);
            fanfare = Synth("fanfare", 0.9f, (t, k) =>
            {
                float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
                int i = Mathf.Min(3, (int)(k * 4f));
                float local = k * 4f - i;
                return Square(notes[i], t) * 0.25f * (1f - local * 0.5f);
            });
            lose = Synth("lose", 1.0f, (t, k) => Saw(Mathf.Lerp(220f, 70f, k), t) * (1f - k) * 0.4f);

            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeMesh = probe.GetComponent<MeshFilter>().sharedMesh;
            Destroy(probe);
        }

        public void SetCameraBase(Vector3 position) => cameraBase = position;

        public void Shot()
        {
            if (Time.time - lastShot < 0.04f) return;
            lastShot = Time.time;
            source.PlayOneShot(shot, 0.35f);
        }

        public void Hit()
        {
            if (Time.time - lastHit < 0.04f) return;
            lastHit = Time.time;
            source.PlayOneShot(hit, 0.45f);
        }

        public void Deploy() => source.PlayOneShot(deploy, 0.6f);
        public void Step(float strength) { source.PlayOneShot(step, 0.5f + 0.4f * strength); Shake(0.04f + 0.08f * strength, 0.2f); }
        public void Crash(int size) { source.PlayOneShot(crash, 0.9f); Shake(Mathf.Clamp(0.12f + size * 0.006f, 0.12f, 0.5f), 0.45f); }
        public void Roar() => source.PlayOneShot(roar, 0.8f);
        public void Fanfare() => source.PlayOneShot(fanfare, 0.7f);
        public void Lose() { source.PlayOneShot(lose, 0.8f); Shake(0.4f, 0.6f); }

        public void Shake(float amplitude, float duration)
        {
            float current = shakeAmplitude * (shakeLeft / Mathf.Max(0.0001f, shakeDuration));
            if (amplitude < current) return;
            shakeAmplitude = amplitude;
            shakeDuration = duration;
            shakeLeft = duration;
        }

        public void Burst(Vector3 position, Material material, int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Spark");
                go.AddComponent<MeshFilter>().sharedMesh = cubeMesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
                var t = go.transform;
                t.position = position;
                var dir = Random.onUnitSphere;
                dir.z = -Mathf.Abs(dir.z); // toward the player
                dir.y = Mathf.Abs(dir.y) + 0.3f;
                float life = Random.Range(0.35f, 0.6f);
                float size = Random.Range(0.12f, 0.25f);
                t.localScale = Vector3.one * size;
                particles.Add(new Particle { T = t, Velocity = dir.normalized * speed * Random.Range(0.6f, 1.2f), Life = life, MaxLife = life, Size = size });
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var p = particles[i];
                p.Life -= dt;
                if (p.Life <= 0f || p.T == null)
                {
                    if (p.T != null) Destroy(p.T.gameObject);
                    particles.RemoveAt(i);
                    continue;
                }
                p.Velocity += Vector3.down * 18f * dt;
                p.T.position += p.Velocity * dt;
                p.T.Rotate(500f * dt, 350f * dt, 0f);
                p.T.localScale = Vector3.one * p.Size * (p.Life / p.MaxLife);
            }
        }

        void LateUpdate()
        {
            if (cam == null) return;
            if (shakeLeft > 0f)
            {
                shakeLeft -= Time.deltaTime;
                float k = Mathf.Clamp01(shakeLeft / shakeDuration);
                cam.transform.position = cameraBase + (Vector3)Random.insideUnitCircle * shakeAmplitude * k;
            }
            else
            {
                cam.transform.position = cameraBase;
            }
        }

        // ---------------------------------------------------------------- synthesis

        static System.Random noise = new System.Random(1);
        static float Noise() => (float)(noise.NextDouble() * 2.0 - 1.0);
        static float Square(float f, float t) => Mathf.Sin(2f * Mathf.PI * f * t) >= 0f ? 1f : -1f;
        static float Saw(float f, float t) => 2f * (t * f - Mathf.Floor(t * f + 0.5f));

        static AudioClip Synth(string name, float duration, System.Func<float, float, float> sample)
        {
            const int rate = 44100;
            int n = Mathf.CeilToInt(rate * duration);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float attack = Mathf.Min(1f, t * 300f);
                data[i] = Mathf.Clamp(sample(t, t / duration) * attack, -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}

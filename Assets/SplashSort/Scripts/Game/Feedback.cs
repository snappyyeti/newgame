using System.Collections.Generic;
using UnityEngine;

namespace SplashSort.Game
{
    /// <summary>
    /// The minimum feedback required even in grey: procedural sounds (the splash rises a whole tone per cascade step),
    /// screen shake and particles. No audio files needed: the tones are generated at start.
    /// </summary>
    public sealed class Feedback : MonoBehaviour
    {
        AudioSource splashSource;
        AudioSource sfxSource;
        AudioClip splashClip;
        AudioClip hopClip;
        AudioClip landClip;

        Camera cam;
        Vector3 cameraBase;
        float shakeLeft;
        float shakeDuration;
        float shakeAmplitude;

        sealed class Particle
        {
            public Transform T;
            public Vector3 Velocity;
            public float Life;
            public float MaxLife;
            public float Size;
        }

        readonly List<Particle> particles = new List<Particle>();
        Mesh cubeMesh;

        public void Init(Camera camera)
        {
            cam = camera;
            cameraBase = cam.transform.position;
            splashSource = gameObject.AddComponent<AudioSource>();
            sfxSource = gameObject.AddComponent<AudioSource>();
            splashSource.playOnAwake = sfxSource.playOnAwake = false;
            splashClip = Tone("splash", 523.25f, 0.35f, 9f, true);
            hopClip = Tone("hop", 880f, 0.06f, 60f, false);
            landClip = Tone("land", 260f, 0.12f, 30f, false);

            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeMesh = probe.GetComponent<MeshFilter>().sharedMesh;
            Destroy(probe);
        }

        /// <summary>Call after the camera is re-fitted so the shake returns to the right place.</summary>
        public void SetCameraBase(Vector3 position)
        {
            cameraBase = position;
        }

        /// <summary>Cascade step 1 plays C5, each further step is a whole tone (2 semitones) higher.</summary>
        public void Splash(int step)
        {
            splashSource.pitch = Mathf.Pow(2f, 2f * (step - 1) / 12f);
            splashSource.PlayOneShot(splashClip, 0.9f);
            Shake(0.10f + 0.05f * step, 0.30f);
        }

        public void Hop()
        {
            sfxSource.PlayOneShot(hopClip, 0.25f);
        }

        public void Land()
        {
            sfxSource.PlayOneShot(landClip, 0.5f);
        }

        public void Shake(float amplitude, float duration)
        {
            if (amplitude < shakeAmplitude * (shakeLeft / Mathf.Max(0.0001f, shakeDuration))) return;
            shakeAmplitude = amplitude;
            shakeDuration = duration;
            shakeLeft = duration;
        }

        /// <summary>A burst of small colored cubes with gravity.</summary>
        public void Burst(Vector3 position, Material material, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Drop");
                go.AddComponent<MeshFilter>().sharedMesh = cubeMesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
                var t = go.transform;
                t.position = position;
                var dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 1.5f + 0.6f;
                float life = Random.Range(0.45f, 0.75f);
                float size = Random.Range(0.07f, 0.14f);
                t.localScale = Vector3.one * size;
                particles.Add(new Particle { T = t, Velocity = dir.normalized * Random.Range(2.5f, 4.5f), Life = life, MaxLife = life, Size = size });
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
                p.Velocity += Vector3.down * 12f * dt;
                p.T.position += p.Velocity * dt;
                p.T.Rotate(400f * dt, 300f * dt, 0f);
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

        static AudioClip Tone(string name, float frequency, float duration, float decay, bool sparkle)
        {
            const int rate = 44100;
            int n = Mathf.CeilToInt(rate * duration);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Exp(-t * decay) * Mathf.Min(1f, t * 400f);
                float s = 0.6f * Mathf.Sin(2f * Mathf.PI * frequency * t) + 0.2f * Mathf.Sin(2f * Mathf.PI * frequency * 2f * t);
                if (sparkle) s += 0.15f * Mathf.Sin(2f * Mathf.PI * frequency * 3f * t) * Mathf.Exp(-t * 20f);
                data[i] = s * env;
            }
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}

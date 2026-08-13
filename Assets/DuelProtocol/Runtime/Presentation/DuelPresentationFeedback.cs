using System;
using System.Globalization;
using UnityEngine;

namespace DuelProtocol.Presentation
{
    [DisallowMultipleComponent]
    public sealed class DuelCarrierTimerDisplay : MonoBehaviour
    {
        private TextMesh _label;
        private GameObject _displayRoot;
        private Camera _camera;

        public bool IsVisible => _displayRoot != null && _displayRoot.activeSelf;
        public string DisplayText => _label != null ? _label.text : string.Empty;

        private void Awake()
        {
            EnsureInitialized();
            SetRemaining(0f, false);
        }

        private void LateUpdate()
        {
            if (!IsVisible) return;
            if (_camera == null) _camera = Camera.main;
            if (_camera != null) _displayRoot.transform.rotation = _camera.transform.rotation;
        }

        public void SetRemaining(float remainingSeconds, bool visible)
        {
            EnsureInitialized();
            _displayRoot.SetActive(visible);
            if (visible)
            {
                _label.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "CORE {0:0.0}s",
                    Mathf.Max(0f, remainingSeconds));
            }
        }

        private void EnsureInitialized()
        {
            if (_displayRoot != null) return;
            _displayRoot = new GameObject("CoreHoldTimer");
            _displayRoot.transform.SetParent(transform, false);
            _displayRoot.transform.localPosition = Vector3.up * 2.45f;

            _label = _displayRoot.AddComponent<TextMesh>();
            _label.anchor = TextAnchor.MiddleCenter;
            _label.alignment = TextAlignment.Center;
            _label.fontSize = 64;
            _label.characterSize = 0.045f;
            _label.fontStyle = FontStyle.Bold;
            _label.color = new Color(1f, 0.82f, 0.12f, 1f);
            _label.text = string.Empty;
        }
    }

    [DisallowMultipleComponent]
    public sealed class DuelCombatFeedback : MonoBehaviour
    {
        private static readonly Color AttackColor = new Color(0.15f, 0.9f, 1f, 1f);
        private static readonly Color HitColor = new Color(1f, 0.2f, 0.08f, 1f);
        private static readonly Color StunColor = new Color(1f, 0.82f, 0.12f, 1f);

        private Renderer[] _characterRenderers = Array.Empty<Renderer>();
        private DuelParticleBurstPool _attackPool;
        private DuelParticleBurstPool _hitPool;
        private ParticleSystem _stunParticles;
        private Transform _stunTransform;
        private MaterialPropertyBlock _propertyBlock;
        private int _lastAbilitySequence;
        private int _lastHitSequence;
        private float _flashSeconds;
        private Color _flashColor;
        private bool _initialized;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Update()
        {
            if (_flashSeconds > 0f)
            {
                _flashSeconds = Mathf.Max(0f, _flashSeconds - Time.deltaTime);
                ApplyFlash(_flashSeconds > 0f ? _flashColor : Color.clear);
            }

            if (!DuelPlayerPreferences.Current.ReducedMotion &&
                _stunTransform != null && _stunParticles != null && _stunParticles.isPlaying)
            {
                _stunTransform.Rotate(0f, 150f * Time.deltaTime, 0f, Space.Self);
            }
        }

        public void ResetPresentation(int abilitySequence = 0, int hitSequence = 0)
        {
            EnsureInitialized();
            _lastAbilitySequence = abilitySequence;
            _lastHitSequence = hitSequence;
            _flashSeconds = 0f;
            ApplyFlash(Color.clear);
            SetStunned(false);
            _initialized = true;
        }

        public void Present(
            int abilitySequence,
            int hitSequence,
            bool stunned,
            Vector2 facing,
            Vector2 inheritedVelocity = default)
        {
            EnsureInitialized();
            if (!_initialized)
            {
                ResetPresentation(abilitySequence, hitSequence);
            }

            if (abilitySequence > _lastAbilitySequence)
            {
                PlayAttack(facing, inheritedVelocity);
            }
            if (hitSequence > _lastHitSequence)
            {
                PlayHit();
            }

            // Never replay an effect when Fusion rolls a predicted value back.
            _lastAbilitySequence = Mathf.Max(_lastAbilitySequence, abilitySequence);
            _lastHitSequence = Mathf.Max(_lastHitSequence, hitSequence);
            SetStunned(stunned);
        }

        public void PlayAttack(Vector2 facing, Vector2 inheritedVelocity = default)
        {
            EnsureInitialized();
            var direction = facing.sqrMagnitude > 0.001f
                ? new Vector3(facing.x, 0f, facing.y).normalized
                : transform.forward;
            var rotation = Quaternion.LookRotation(direction, Vector3.up);
            if (!DuelPlayerPreferences.Current.ReducedMotion)
            {
                _attackPool.Play(
                    transform.position + Vector3.up * 0.85f,
                    rotation,
                    new Vector3(inheritedVelocity.x, 0f, inheritedVelocity.y));
            }
            StartFlash(AttackColor, 0.12f);
            DuelGameAudio.Instance.PlayPulse(transform.position);
        }

        public void PlayHit()
        {
            EnsureInitialized();
            if (!DuelPlayerPreferences.Current.ReducedMotion)
            {
                _hitPool.Play(transform.position + Vector3.up * 0.9f, Quaternion.identity);
            }
            StartFlash(HitColor, 0.18f);
            DuelGameAudio.Instance.PlayHit(transform.position);
        }

        public void SetStunned(bool stunned)
        {
            EnsureInitialized();
            if (stunned)
            {
                if (!_stunParticles.isPlaying) _stunParticles.Play(true);
            }
            else if (_stunParticles.isPlaying)
            {
                _stunParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void EnsureInitialized()
        {
            if (_attackPool != null) return;

            _characterRenderers = GetComponentsInChildren<Renderer>(true);
            _propertyBlock = new MaterialPropertyBlock();
            _attackPool = new DuelParticleBurstPool(
                transform,
                "AttackPulsePool",
                AttackColor,
                ParticleSystemShapeType.Cone,
                28,
                0.32f,
                5.5f,
                0.13f,
                26f,
                0.08f);
            _hitPool = new DuelParticleBurstPool(
                transform,
                "HitBurstPool",
                HitColor,
                ParticleSystemShapeType.Sphere,
                22,
                0.38f,
                3.8f,
                0.14f,
                360f,
                0.15f);
            CreateStunParticles();
        }

        private void CreateStunParticles()
        {
            var stunObject = new GameObject("StunIndicator");
            _stunTransform = stunObject.transform;
            _stunTransform.SetParent(transform, false);
            _stunTransform.localPosition = Vector3.up * 2f;
            _stunParticles = stunObject.AddComponent<ParticleSystem>();
            _stunParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _stunParticles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 0.6f;
            main.startLifetime = 0.65f;
            main.startSpeed = 0f;
            main.startSize = 0.12f;
            main.startColor = StunColor;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 16;

            var emission = _stunParticles.emission;
            emission.rateOverTime = 12f;

            var shape = _stunParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.58f;
            shape.radiusThickness = 0.05f;

            DuelParticleBurstPool.ConfigureRenderer(_stunParticles, StunColor);
        }

        private void StartFlash(Color color, float seconds)
        {
            _flashColor = color;
            _flashSeconds = Mathf.Max(_flashSeconds, seconds);
            ApplyFlash(color);
        }

        private void ApplyFlash(Color color)
        {
            foreach (var characterRenderer in _characterRenderers)
            {
                if (characterRenderer == null) continue;
                if (color.a <= 0f)
                {
                    characterRenderer.SetPropertyBlock(null);
                    continue;
                }

                characterRenderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor("_BaseColor", color);
                _propertyBlock.SetColor("_Color", color);
                characterRenderer.SetPropertyBlock(_propertyBlock);
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class DuelCoreFeedback : MonoBehaviour
    {
        private static readonly Color RespawnColor = new Color(1f, 0.78f, 0.08f, 1f);
        private Renderer[] _coreRenderers = Array.Empty<Renderer>();
        private Collider[] _coreColliders = Array.Empty<Collider>();
        private DuelParticleBurstPool _respawnPool;
        private bool _initialized;
        private bool _respawning;

        public bool IsRespawning => _respawning;

        private void Awake()
        {
            EnsureInitialized();
        }

        public void SetRespawning(bool respawning, bool playCompletionEffect = true)
        {
            EnsureInitialized();
            var completed = _initialized && _respawning && !respawning;
            _respawning = respawning;
            _initialized = true;

            foreach (var coreRenderer in _coreRenderers)
            {
                if (coreRenderer != null) coreRenderer.enabled = !respawning;
            }
            foreach (var coreCollider in _coreColliders)
            {
                if (coreCollider != null) coreCollider.enabled = !respawning;
            }

            if (completed && playCompletionEffect && !DuelPlayerPreferences.Current.ReducedMotion)
            {
                _respawnPool.Play(transform.position, Quaternion.Euler(90f, 0f, 0f));
                DuelGameAudio.Instance.PlayCore(transform.position);
            }
        }

        public static Vector3 GetCarrierOrbitPosition(
            Vector3 carrierPosition,
            float timeSeconds,
            int carrierIndex,
            float radius = 1.15f,
            float height = 1.25f,
            float degreesPerSecond = 150f)
        {
            var phase = carrierIndex * Mathf.PI;
            var angle = timeSeconds * degreesPerSecond * Mathf.Deg2Rad + phase;
            return carrierPosition + new Vector3(
                Mathf.Cos(angle) * radius,
                height,
                Mathf.Sin(angle) * radius);
        }

        private void EnsureInitialized()
        {
            if (_respawnPool != null) return;
            _coreRenderers = GetComponentsInChildren<Renderer>(true);
            _coreColliders = GetComponentsInChildren<Collider>(true);
            _respawnPool = new DuelParticleBurstPool(
                transform,
                "CoreRespawnPool",
                RespawnColor,
                ParticleSystemShapeType.Circle,
                36,
                0.65f,
                2.2f,
                0.16f,
                0f,
                0.85f);
        }
    }

    internal sealed class DuelParticleBurstPool
    {
        private const int PoolSize = 3;
        private static Material _particleMaterial;
        private readonly ParticleSystem[] _systems = new ParticleSystem[PoolSize];
        private int _nextIndex;

        public DuelParticleBurstPool(
            Transform parent,
            string name,
            Color color,
            ParticleSystemShapeType shapeType,
            short particleCount,
            float lifetime,
            float speed,
            float size,
            float coneAngle,
            float radius)
        {
            for (var i = 0; i < PoolSize; i++)
            {
                var effectObject = new GameObject($"{name}_{i + 1}");
                effectObject.transform.SetParent(parent, false);
                var particles = effectObject.AddComponent<ParticleSystem>();
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                var main = particles.main;
                main.playOnAwake = false;
                main.loop = false;
                main.duration = Mathf.Max(0.1f, lifetime);
                main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.7f, lifetime);
                main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.75f, speed);
                main.startSize = new ParticleSystem.MinMaxCurve(size * 0.7f, size);
                main.startColor = color;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = Mathf.Max(64, particleCount * 2);

                var emission = particles.emission;
                emission.enabled = true;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, particleCount) });

                var shape = particles.shape;
                shape.enabled = true;
                shape.shapeType = shapeType;
                shape.radius = radius;
                if (shapeType == ParticleSystemShapeType.Cone)
                {
                    shape.angle = coneAngle;
                    shape.radiusThickness = 1f;
                }
                else if (shapeType == ParticleSystemShapeType.Sphere)
                {
                    shape.radiusThickness = 1f;
                }
                else if (shapeType == ParticleSystemShapeType.Circle)
                {
                    shape.radiusThickness = 0.1f;
                }

                ConfigureRenderer(particles, color);
                _systems[i] = particles;
            }
        }

        public void Play(
            Vector3 position,
            Quaternion rotation,
            Vector3 inheritedVelocity = default)
        {
            var particles = _systems[_nextIndex];
            _nextIndex = (_nextIndex + 1) % _systems.Length;
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.transform.SetPositionAndRotation(position, rotation);
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = inheritedVelocity.x;
            velocity.y = inheritedVelocity.y;
            velocity.z = inheritedVelocity.z;
            particles.Play(true);
        }

        public static void ConfigureRenderer(ParticleSystem particles, Color color)
        {
            var particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.alignment = ParticleSystemRenderSpace.View;
            particleRenderer.sharedMaterial = GetParticleMaterial(color);
        }

        private static Material GetParticleMaterial(Color color)
        {
            if (_particleMaterial != null) return _particleMaterial;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                         Shader.Find("Particles/Standard Unlit") ??
                         Shader.Find("Unlit/Color");
            if (shader == null) return null;
            _particleMaterial = new Material(shader)
            {
                name = "Duel Runtime Particle Material",
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = 3000
            };
            if (_particleMaterial.HasProperty("_BaseColor")) _particleMaterial.SetColor("_BaseColor", Color.white);
            if (_particleMaterial.HasProperty("_Color")) _particleMaterial.SetColor("_Color", Color.white);
            return _particleMaterial;
        }
    }
}

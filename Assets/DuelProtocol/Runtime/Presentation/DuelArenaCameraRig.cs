using UnityEngine;

namespace DuelProtocol.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class DuelArenaCameraRig : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float arenaHalfExtent = 9f;
        [SerializeField, Min(0f)] private float framingPadding = 1f;
        [SerializeField, Min(1f)] private float height = 16f;
        [SerializeField, Min(1f)] private float distance = 14f;
        [SerializeField, Range(35f, 85f)] private float fieldOfView = 52f;
        private Camera _camera;

        public static float CalculateOrthographicSize(
            float halfExtent,
            float aspect,
            float padding = 1f)
        {
            var paddedExtent = Mathf.Max(1f, halfExtent + Mathf.Max(0f, padding));
            return Mathf.Max(paddedExtent, paddedExtent / Mathf.Max(0.1f, aspect));
        }

        public static DuelArenaCameraRig Ensure(Camera camera, float halfExtent = 9f)
        {
            if (camera == null)
            {
                return null;
            }
            var rig = camera.GetComponent<DuelArenaCameraRig>() ??
                      camera.gameObject.AddComponent<DuelArenaCameraRig>();
            if (FindAnyObjectByType<AudioListener>() == null)
            {
                camera.gameObject.AddComponent<AudioListener>();
            }
            rig.arenaHalfExtent = Mathf.Max(1f, halfExtent);
            rig.ApplyFraming();
            return rig;
        }

        private void Awake()
        {
            ApplyFraming();
        }

        private void LateUpdate()
        {
            ApplyFraming();
        }

        public void ApplyFraming()
        {
            _camera ??= GetComponent<Camera>();
            if (_camera == null)
            {
                return;
            }
            _camera.orthographic = false;
            _camera.fieldOfView = _camera.aspect < 1f ? Mathf.Min(78f, fieldOfView + 17f) : fieldOfView;
            _camera.nearClipPlane = .1f;
            _camera.farClipPlane = 160f;
            var position = new Vector3(0f, height, -distance);
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(Vector3.zero - position, Vector3.up));
        }
    }
}

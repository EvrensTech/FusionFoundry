using Fusion;
using UnityEngine;

namespace FusionFoundry.Samples.BasicHostClient
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public class LocalPlayerCameraBinder : MonoBehaviour
    {
        private NetworkObject _networkObject;
        private ThirdPersonCameraRig _claimedRig;

        public bool IsCameraBound =>
            _claimedRig != null &&
            ReferenceEquals(_claimedRig.Owner, this);

        protected virtual void Awake()
        {
            _networkObject = GetComponent<NetworkObject>();
        }

        protected virtual void OnEnable()
        {
            RefreshBinding();
        }

        protected virtual void Update()
        {
            RefreshBinding();
        }

        protected virtual void OnDisable()
        {
            ReleaseCamera();
        }

        protected virtual void OnDestroy()
        {
            ReleaseCamera();
        }

        public void RefreshBinding()
        {
            if (!HasLocalInputAuthority())
            {
                ReleaseCamera();
                return;
            }

            if (IsCameraBound)
            {
                return;
            }

            _claimedRig = ResolveCameraRig();
            if (_claimedRig != null && !_claimedRig.Claim(this, transform))
            {
                _claimedRig = null;
            }
        }

        protected virtual bool HasLocalInputAuthority()
        {
            return _networkObject != null && _networkObject.HasInputAuthority;
        }

        protected virtual ThirdPersonCameraRig ResolveCameraRig()
        {
            return FindAnyObjectByType<ThirdPersonCameraRig>();
        }

        private void ReleaseCamera()
        {
            if (_claimedRig == null)
            {
                return;
            }

            _claimedRig.Release(this);
            _claimedRig = null;
        }
    }
}

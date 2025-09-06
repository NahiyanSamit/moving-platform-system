using UnityEngine;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NahiyanSamit.MovingPlatformSystem
{

    [RequireComponent(typeof(Rigidbody))]
    public class RotatingPlatform : MonoBehaviour
    {
        [Header("Rotation Settings")]
        [SerializeField] private List<Vector3> relativeRotationWaypoints = new List<Vector3>();
        [SerializeField] private float rotationSpeed = 90f;
        [SerializeField] private float waitTimeAtWaypoint = 0.0f;

        [SerializeField] private LoopType loopType = LoopType.Loop;
        [SerializeField] private bool rotateOnStart = true;

        private readonly List<Quaternion> _absoluteRotations = new List<Quaternion>();
        private int _currentIndex;
        private int _direction = 1;
        private bool _isRotating;

        private Rigidbody _rb;
        private CancellationTokenSource _cts;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _cts = new CancellationTokenSource();
            BuildAbsoluteRotations();
        }

        private void Start()
        {
            if (rotateOnStart)
            {
                _isRotating = true;
                RotateToNextAsync(_cts.Token).Forget();
            }
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        private void BuildAbsoluteRotations()
        {
            _absoluteRotations.Clear();
            var start = transform.rotation;
            _absoluteRotations.Add(start);
            var accum = start;
            foreach (var deltaEuler in relativeRotationWaypoints)
            {
                accum = accum * Quaternion.Euler(deltaEuler);
                _absoluteRotations.Add(accum);
            }
        }

        // Public API
        public void StartRotating()
        {
            if (_isRotating) return;
            _isRotating = true;
            _cts = new CancellationTokenSource();
            RotateToNextAsync(_cts.Token).Forget();
        }

        public void StopRotating()
        {
            _isRotating = false;
            _cts?.Cancel();
        }

        // Compatibility aliases so generic helpers can call the same names
        public void StartMoving() => StartRotating();
        public void StopMoving() => StopRotating();

        private async UniTask RotateToNextAsync(CancellationToken token)
        {
            const float angleEpsilon = 0.1f; // degrees

            while (_isRotating && !token.IsCancellationRequested)
            {
                if (_absoluteRotations.Count <= 1)
                {
                    await UniTask.Yield(token);
                    continue;
                }

                Quaternion target = _absoluteRotations[_currentIndex];

                // Rotate towards target using fixed timestep for physics consistency
                while (_isRotating && !token.IsCancellationRequested)
                {
                    float remaining = Quaternion.Angle(transform.rotation, target);
                    if (remaining <= angleEpsilon) break;

                    float step = rotationSpeed * Time.fixedDeltaTime;
                    Quaternion next = Quaternion.RotateTowards(transform.rotation, target, step);

                    if (_rb != null)
                        _rb.MoveRotation(next);
                    else
                        transform.rotation = next;

                    await UniTask.WaitForFixedUpdate(token);
                }

                // Optional dwell at waypoint
                if (waitTimeAtWaypoint > 0f)
                    await UniTask.Delay((int)(waitTimeAtWaypoint * 1000f), cancellationToken: token);

                // Advance index according to loop type
                switch (loopType)
                {
                    case LoopType.None:
                        _currentIndex++;
                        if (_currentIndex >= _absoluteRotations.Count)
                            _isRotating = false;
                        break;

                    case LoopType.Loop:
                        _currentIndex = (_currentIndex + 1) % _absoluteRotations.Count;
                        break;

                    case LoopType.PingPong:
                        _currentIndex += _direction;
                        if (_currentIndex >= _absoluteRotations.Count || _currentIndex < 0)
                        {
                            _direction *= -1;
                            _currentIndex += _direction;
                        }
                        break;
                }

                await UniTask.Yield(token);
            }
        }
    }
}

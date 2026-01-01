using UnityEngine;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NahiyanSamit.MovingPlatformSystem
{
    public enum LoopType
    {
        None,
        Loop,
        PingPong
    }

    [RequireComponent(typeof(Rigidbody))]
    public class MovingPlatform : MonoBehaviour
    {
        [Header("Platform Settings")]
        [SerializeField] private List<Vector3> relativeWaypoints;
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float waitTimeAtWaypoint = 1f;
        [SerializeField] private LoopType loopType; 
        [SerializeField] private bool moveOnStart;

        [Header("Collision Settings")]
        [Tooltip("Layers that should move with the platform")]
        [SerializeField] private LayerMask passengerMask = -1; // Default to Everything

        private List<Vector3> _absoluteWaypoints;
        private int _currentWaypointIndex;
        private int _direction = 1;
        private bool _isMoving;

        private Rigidbody _rb;
        private CancellationTokenSource _cts;

        // TRACKING PASSENGERS
        private HashSet<Transform> _passengers = new HashSet<Transform>();
        private Dictionary<Transform, CharacterController> _passengerControllers = new Dictionary<Transform, CharacterController>();
        private Dictionary<Transform, Rigidbody> _passengerRigidbodies = new Dictionary<Transform, Rigidbody>();

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = true; 
            _rb.useGravity = false;
            _rb.interpolation = RigidbodyInterpolation.Interpolate; 

            _absoluteWaypoints = new List<Vector3> { transform.position };
            _cts = new CancellationTokenSource();
        }

        private void Start()
        {
            // Convert relative waypoints to absolute positions
            if (relativeWaypoints != null)
            {
                foreach (var waypoint in relativeWaypoints)
                {
                    Vector3 previousWaypoint = _absoluteWaypoints[_absoluteWaypoints.Count - 1];
                    _absoluteWaypoints.Add(previousWaypoint + waypoint);
                }
            }

            if (moveOnStart)
            {
                StartMoving();
            }
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
        
        public void StartMoving()
        {
            if (!_isMoving)
            {
                _isMoving = true;
                _cts = new CancellationTokenSource();
                MoveToNextWaypointAsync(_cts.Token).Forget();
            }
        }

        public void StopMoving()
        {
            _isMoving = false;
            _cts?.Cancel();
        }

        private async UniTask MoveToNextWaypointAsync(CancellationToken token)
        {
            while (_isMoving && !token.IsCancellationRequested)
            {
                if (_absoluteWaypoints.Count <= 1)
                {
                    await UniTask.Yield(token);
                    continue;
                }

                Vector3 targetPosition = _absoluteWaypoints[_currentWaypointIndex];

                // Movement Loop
                while (Vector3.Distance(transform.position, targetPosition) > 0.01f && !token.IsCancellationRequested)
                {
                    // Calculate the step for this frame
                    Vector3 currentPos = transform.position;
                    Vector3 newPos = Vector3.MoveTowards(currentPos, targetPosition, moveSpeed * Time.fixedDeltaTime);
                    Vector3 moveDelta = newPos - currentPos;

                    // 1. Move the Platform
                    _rb.MovePosition(newPos);

                    // 2. Move Passengers by the same amount
                    MovePassengers(moveDelta);

                    await UniTask.WaitForFixedUpdate(token);
                }

                // Snap to exact target to prevent drift
                Vector3 finalSnapDelta = targetPosition - transform.position;
                _rb.MovePosition(targetPosition);
                MovePassengers(finalSnapDelta);

                await UniTask.Delay((int)(waitTimeAtWaypoint * 1000), cancellationToken: token);

                UpdateWaypointIndex();
                
                // Yield to ensure loop doesn't hang if delay is 0
                await UniTask.Yield(token);
            }
        }

        private void UpdateWaypointIndex()
        {
            switch (loopType)
            {
                case LoopType.None:
                    _currentWaypointIndex++;
                    if (_currentWaypointIndex >= _absoluteWaypoints.Count)
                        _isMoving = false;
                    break;

                case LoopType.Loop:
                    _currentWaypointIndex++;
                    if (_currentWaypointIndex >= _absoluteWaypoints.Count)
                        _currentWaypointIndex = 0;
                    break;

                case LoopType.PingPong:
                    _currentWaypointIndex += _direction;
                    if (_currentWaypointIndex >= _absoluteWaypoints.Count || _currentWaypointIndex < 0)
                    {
                        _direction *= -1;
                        _currentWaypointIndex += _direction;
                        // Clamp to prevent out of bounds on double bounce
                        _currentWaypointIndex = Mathf.Clamp(_currentWaypointIndex, 0, _absoluteWaypoints.Count - 1);
                    }
                    break;
            }
        }

        // --- PASSENGER SYSTEM ---

        private void MovePassengers(Vector3 delta)
        {
            // Clean up list just in case objects were destroyed
            _passengers.RemoveWhere(t => t == null);

            foreach (Transform passenger in _passengers)
            {
                // Option A: If it has a CharacterController (Standard Unity Player)
                if (_passengerControllers.TryGetValue(passenger, out CharacterController cc))
                {
                    // enable movement and move (use minimal step offset to force update)
                    cc.Move(delta);
                }
                // Option B: If it has a Rigidbody (Physics Object)
                else if (_passengerRigidbodies.TryGetValue(passenger, out Rigidbody rb))
                {
                    rb.MovePosition(rb.position + delta);
                }
                // Option C: Basic Transform (Simple objects)
                else
                {
                    passenger.position += delta;
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            HandlePassengerEnter(collision.transform);
        }

        private void OnCollisionExit(Collision collision)
        {
            HandlePassengerExit(collision.transform);
        }

        // Optional: Support Trigger based characters if needed
        private void OnTriggerEnter(Collider other)
        {
             // Only add if not using Rigidbody (to avoid double adding)
             if(other.attachedRigidbody == null) HandlePassengerEnter(other.transform);
        }
        
        private void OnTriggerExit(Collider other)
        {
             if(other.attachedRigidbody == null) HandlePassengerExit(other.transform);
        }

        private void HandlePassengerEnter(Transform t)
        {
            // Check layer mask
            if (((1 << t.gameObject.layer) & passengerMask) == 0) return;

            // Avoid adding the platform itself or parents
            if (t == transform || t.IsChildOf(transform)) return;

            if (_passengers.Add(t))
            {
                // Cache components for performance
                var cc = t.GetComponent<CharacterController>();
                if (cc != null) _passengerControllers[t] = cc;

                var rb = t.GetComponent<Rigidbody>();
                if (rb != null) _passengerRigidbodies[t] = rb;
            }
        }

        private void HandlePassengerExit(Transform t)
        {
            if (_passengers.Remove(t))
            {
                _passengerControllers.Remove(t);
                _passengerRigidbodies.Remove(t);
            }
        }
    }
}
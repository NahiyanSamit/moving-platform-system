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

        private List<Vector3> _absoluteWaypoints;
        private int _currentWaypointIndex;
        private int _direction = 1;
        private bool _isMoving;
        
        private Vector3 _lastPosition;
        private HashSet<Transform> _objectsOnPlatform = new HashSet<Transform>();

        private Rigidbody _rb;
        private CancellationTokenSource _cts;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = true; 
            _rb.useGravity = false;
            _rb.interpolation = RigidbodyInterpolation.None; 

            _lastPosition = transform.position;
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

                await MoveToWaypointAsync(_absoluteWaypoints[_currentWaypointIndex], token);
                await UniTask.Delay((int)(waitTimeAtWaypoint * 1000), cancellationToken: token);
                UpdateWaypointIndex();
            }
        }

        private async UniTask MoveToWaypointAsync(Vector3 targetPosition, CancellationToken token)
        {
            const float arrivalThreshold = 0.0001f; // sqrMagnitude threshold for 0.01f distance
    
            while ((transform.position - targetPosition).sqrMagnitude > arrivalThreshold && !token.IsCancellationRequested)
            {
                Vector3 newPos = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.fixedDeltaTime);
                Vector3 delta = newPos - _lastPosition;
        
                _rb.transform.position = newPos;
                MoveObjectsOnPlatform(delta);
        
                _lastPosition = newPos;
                await UniTask.WaitForFixedUpdate(token);
            }
        }

        private void MoveObjectsOnPlatform(Vector3 delta)
        {
            if (_objectsOnPlatform.Count == 0) return;
    
            foreach (var obj in _objectsOnPlatform)
            {
                if (obj != null)
                    obj.position += delta;
            }
        }

        private void OnCollisionStay(Collision collision)
        {
            foreach (var contact in collision.contacts)
            {
                if (Vector3.Dot(contact.normal, Vector3.up) < 0.5f)
                {
                    _objectsOnPlatform.Add(collision.transform);
                    return;
                }
            }
        }

        private void OnCollisionExit(Collision collision)
        {
            _objectsOnPlatform.Remove(collision.transform);
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
    }
}

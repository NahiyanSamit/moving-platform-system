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
    
    
    public class MovingPlatform : MonoBehaviour
    {
        [Header("Platform Settings")]
        [SerializeField] private List<Vector3> relativeWaypoints;
        [SerializeField] private float moveSpeed;
        [SerializeField] private float waitTimeAtWaypoint;
        [SerializeField] private LoopType loopType; 
        [SerializeField] private bool moveOnStart;

        private List<Vector3> _absoluteWaypoints;
        private int _currentWaypointIndex;
        private int _direction = 1;
        private bool _isMoving;

        private Rigidbody _rb;
        private CancellationTokenSource _cts;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _absoluteWaypoints = new List<Vector3> { transform.position };
            _cts = new CancellationTokenSource();
        }

        private void Start()
        {
            // Convert relative waypoints to absolute positions
            foreach (var waypoint in relativeWaypoints)
            {
                Vector3 previousWaypoint = _absoluteWaypoints[_absoluteWaypoints.Count - 1];
                _absoluteWaypoints.Add(previousWaypoint + waypoint);
            }

            if (moveOnStart)
            {
                _isMoving = true;
                MoveToNextWaypointAsync(_cts.Token).Forget();
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
            _cts.Cancel();
        }

        private async UniTask MoveToNextWaypointAsync(CancellationToken token)
        {
            while (_isMoving && !token.IsCancellationRequested)
            {
                if (_absoluteWaypoints.Count <= 1)
                {
                    await UniTask.Yield(); // prevent freeze if no waypoints
                    continue;
                }

                Vector3 targetPosition = _absoluteWaypoints[_currentWaypointIndex];

                while (Vector3.Distance(transform.position, targetPosition) > 0.1f && !token.IsCancellationRequested)
                {
                    Vector3 direction = (targetPosition - transform.position).normalized;
                    _rb.MovePosition(transform.position + direction * moveSpeed * Time.fixedDeltaTime);
                    await UniTask.WaitForFixedUpdate(token);
                }

                await UniTask.Delay((int)(waitTimeAtWaypoint * 1000), cancellationToken: token);

                // Update waypoint index based on loop type
                switch (loopType)
                {
                    case LoopType.None:
                        _currentWaypointIndex++;
                        if (_currentWaypointIndex >= _absoluteWaypoints.Count)
                        {
                            _isMoving = false;
                        }
                        break;

                    case LoopType.Loop:
                        _currentWaypointIndex++;
                        if (_currentWaypointIndex >= _absoluteWaypoints.Count)
                        {
                            _currentWaypointIndex = 0;
                        }
                        break;

                    case LoopType.PingPong:
                        _currentWaypointIndex += _direction;
                        if (_currentWaypointIndex >= _absoluteWaypoints.Count || _currentWaypointIndex < 0)
                        {
                            _direction *= -1; // Reverse direction
                            _currentWaypointIndex += _direction;
                        }
                        break;
                }
                // Always yield at least once per outer loop iteration
                await UniTask.Yield(token);
            }
        }
    }
}

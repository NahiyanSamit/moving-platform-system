# MovingPlatform Script Documentation

## Overview
The `MovingPlatform` and `RotatingPlatform` script provides a flexible and robust system for creating moving platforms in Unity. It supports multiple waypoints, customizable movement speed, wait times, and looping behaviors. The platform can be started and stopped at runtime, making it suitable for dynamic gameplay scenarios.

---

## Features
- Move a platform along a series of waypoints
- Supports three loop types: None, Loop, PingPong
- Adjustable speed and wait time at each waypoint
- Start/stop movement via code or events
- Uses UniTask for efficient async movement

---

## Inspector Fields
- **relativeWaypoints** (`List<Vector3>`):
  - The list of waypoints relative to the platform's starting position.
- **moveSpeed** (`float`):
  - The speed at which the platform moves between waypoints.
- **waitTimeAtWaypoint** (`float`):
  - The time (in seconds) the platform waits at each waypoint before moving to the next.
- **loopType** (`LoopType`):
  - Determines how the platform moves through waypoints:
    - `None`: Stops at the last waypoint.
    - `Loop`: Loops back to the first waypoint after the last.
    - `PingPong`: Moves back and forth between waypoints.
- **moveOnStart** (`bool`):
  - If true, the platform starts moving automatically when the scene starts.

---

## Public Methods
- **StartMoving()**
  - Starts the platform's movement if it is not already moving.
- **StopMoving()**
  - Stops the platform's movement immediately.
- **StartRotating()//StartMoving()**
  - Starts the platform's rotation if it is not already rotating.
- **StopRotating()//StartMoving()**
  - Stops the platform's rotation immediately.

---

## Usage Example
1. Attach the `MovingPlatform` or `RotatingPlatform` script to a GameObject with a Rigidbody.
2. Configure the waypoints, speed, wait time, and loop type in the Inspector.
3. To control the platform from another script:

```csharp
public class PlatformController : MonoBehaviour
{
    public MovingPlatform platform;

    public void ActivatePlatform()
    {
        platform.StartMoving();
    }

    public void DeactivatePlatform()
    {
        platform.StopMoving();
    }
}
```

```csharp
public class PlatformController : MonoBehaviour
{
    public RotatingPlatform platform;

    public void ActivatePlatform()
    {
        platform.StartMoving(); // Or platform.StartRotating();
    }

    public void DeactivatePlatform()
    {
        platform.StopMoving(); // Or platform.StopRotating(); 
    }
}
```

---

## Notes
- The script requires a Rigidbody component on the same GameObject.
- Uses Cysharp's UniTask for async movement; ensure UniTask is installed in your project.
- You can extend the script to add triggers, switches, or event-based control for more complex interactions.

---

## LoopType Enum
- **None**: Platform stops at the last waypoint.
- **Loop**: Platform loops back to the first waypoint after reaching the last.
- **PingPong**: Platform reverses direction at the ends, moving back and forth.

---

## Dependencies
- [Cysharp UniTask](https://github.com/Cysharp/UniTask)

---

For more advanced usage, see the package documentation or contact the author.

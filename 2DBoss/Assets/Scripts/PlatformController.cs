using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlatformController : RaycastController {

    public LayerMask passangerMask;
    

    public Vector3[] localWaypoints;
    Vector3[] globalWaypoints;

    public float speed;
    public bool cyclic;
    public float waitTime;

    [Range(0,2)]
    public float easeAmount;

    int fromWaypointIndex;
    float percentBetweenWayPoints;
    float nextMoveTime;

    List<PassengerMovemnt> passengerMovement;
    Dictionary<Transform, Controller2D> passengerDictionary = new Dictionary<Transform, Controller2D>();


	// Use this for initialization
	public override void Awake () {
        base.Awake();

        globalWaypoints = new Vector3[localWaypoints.Length];
        for (int i = 0; i < localWaypoints.Length; i++)
        {
            globalWaypoints[i] = localWaypoints[i] + transform.position;
        }
	}
	
	// Update is called once per frame
	void Update () {

        UpdateRaycastORigins();

        Vector3 velocity = CalculatePlatformMovement();

        CalculatePassangerMovement(velocity);

        MovePassangers(true);
        transform.Translate(velocity);
        MovePassangers(false);
    }

    float Ease(float x) {
        float a = easeAmount + 1;
        return Mathf.Pow(x, a) / (Mathf.Pow(x,a)+Mathf.Pow(1-x,a));
    }

    Vector3 CalculatePlatformMovement() {

        if (Time.time < nextMoveTime) {
            return Vector3.zero;
        }

        fromWaypointIndex %= globalWaypoints.Length;
        int toWayPointIndex = (fromWaypointIndex + 1) % globalWaypoints.Length;
        float distanceBetweenWaypoints = Vector3.Distance(globalWaypoints[fromWaypointIndex], globalWaypoints[toWayPointIndex]);
        percentBetweenWayPoints += Time.deltaTime * speed/distanceBetweenWaypoints;
        percentBetweenWayPoints = Mathf.Clamp01(percentBetweenWayPoints);
        float easedPercentBetweenWayPoints = Ease(percentBetweenWayPoints);

        Vector3 newPos = Vector3.Lerp(globalWaypoints[fromWaypointIndex], globalWaypoints[toWayPointIndex], easedPercentBetweenWayPoints);

        if (percentBetweenWayPoints >= 1) {
            percentBetweenWayPoints = 0;
            fromWaypointIndex++;
            if (!cyclic) {
                if (fromWaypointIndex >= globalWaypoints.Length - 1) {
                    fromWaypointIndex = 0;
                    System.Array.Reverse(globalWaypoints);
                }
            }
            nextMoveTime = Time.time + waitTime;
        }

        return newPos - transform.position;
    }

    void MovePassangers(bool beforeMovePlatform) {
        foreach (PassengerMovemnt passenger in passengerMovement) {
            if (!passengerDictionary.ContainsKey(passenger.transform))
            {
                passengerDictionary.Add(passenger.transform, passenger.transform.GetComponent<Controller2D>());
            }

            if (passenger.moveBeforePlatform == beforeMovePlatform)
            {
                passengerDictionary[passenger.transform].Move(passenger.velocity, passenger.standingOnPlatform);
            }
        }
    }


    void CalculatePassangerMovement(Vector3 velocity) {
        HashSet<Transform> movedPassangers = new HashSet<Transform>();
        passengerMovement = new List<PassengerMovemnt>();
        float directionX = Mathf.Sign(velocity.x);
        float directionY = Mathf.Sign(velocity.y);

        //Vertical movement
        if (velocity.y != 0)
        {
            float rayLength = Mathf.Abs(velocity.y) + skinWidth;
            for (int i = 0; i < verticalRayCount; i++)
            {
                Vector2 rayOrigin = (directionY == -1) ? raycastOrigins.bottomLeft : raycastOrigins.topLeft;
                rayOrigin += Vector2.right * (verticalRaySpacing * i);
                RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.up * directionY, rayLength,passangerMask);

                if (hit && hit.distance != 0) {
                    if (!movedPassangers.Contains(hit.transform)) {
                        movedPassangers.Add(hit.transform);
                        float pushX=(directionY == 1)?velocity.x:0;
                        float pushY = velocity.y - (hit.distance - skinWidth) * directionY;

                        passengerMovement.Add(new PassengerMovemnt(hit.transform, new Vector3(pushX,pushY),directionY == 1, true));
                    }
                }
            }
        }
        //Horizontal movement
        if (velocity.x != 0)
        {
            float rayLength = Mathf.Abs(velocity.x) + skinWidth;
            for (int i = 0; i < horizontalRayCount; i++)
            {
                Vector2 rayOrigin = (directionX == -1) ? raycastOrigins.bottomLeft : raycastOrigins.bottomRight;
                rayOrigin += Vector2.up * (horizontalRaySpacing * i);
                RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.right * directionX, rayLength, passangerMask);

            
                if (hit && hit.distance != 0)
                {
                    if (!movedPassangers.Contains(hit.transform))
                    {
                        movedPassangers.Add(hit.transform);
                        float pushX = velocity.x - (hit.distance - skinWidth) * directionX;
                        float pushY = -skinWidth;

                        passengerMovement.Add(new PassengerMovemnt(hit.transform, new Vector3(pushX, pushY), false, true));
                    }
                }
            }
        }
        //Passanger on top - horizontal or downward moving platform

        if (directionY == -1 || velocity.y == 0 && velocity.x != 0) {
            float rayLength = skinWidth * 2;
            for (int i = 0; i < verticalRayCount; i++)
            {
                Vector2 rayOrigin = raycastOrigins.topLeft + Vector2.right * (verticalRaySpacing * i);
                RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.up, rayLength, passangerMask);

                if (hit && hit.distance != 0)
                {
                    if (!movedPassangers.Contains(hit.transform))
                    {
                        movedPassangers.Add(hit.transform);
                        float pushX = velocity.x;
                        float pushY = velocity.y;

                        passengerMovement.Add(new PassengerMovemnt(hit.transform, new Vector3(pushX, pushY), true, false));
                    }
                }
            }
        }
    }

    struct PassengerMovemnt {
        public Transform transform;
        public Vector3 velocity;
        public bool standingOnPlatform;
        public bool moveBeforePlatform;

        public PassengerMovemnt(Transform _transform, Vector3 _velocity, bool _standingOnPlatform, bool _moveBeforePlatform) {
            transform = _transform;
            velocity = _velocity;
            standingOnPlatform = _standingOnPlatform;
            moveBeforePlatform = _moveBeforePlatform;
        }
    }

    void OnDrawGizmos() {
        if (localWaypoints != null)
        {
            Gizmos.color = Color.red;
            float size = .3f;

            for (int i = 0; i < localWaypoints.Length; i++) {
                Vector3 globalWaypointPos =(Application.isPlaying)?globalWaypoints[i] : localWaypoints[i] + transform.position;
                Gizmos.DrawLine(globalWaypointPos - Vector3.up * size, globalWaypointPos + Vector3.up * size);
                Gizmos.DrawLine(globalWaypointPos - Vector3.left * size, globalWaypointPos + Vector3.left * size);
            }
        }
    }

}

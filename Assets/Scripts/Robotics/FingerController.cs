using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FingerController : MonoBehaviour
{
    // Transform components of the VR tracked body parts
    // These will be put in a list of points of interest
    [SerializeField] private Transform _root;
    [SerializeField] private Transform _tip;
    [SerializeField] private Transform _ikTarget;

    [SerializeField] private float _wobbleSpeed = 0.1f;

    // Constraints for the target position of the finger tip, in the local
    // space of the finger's current orientation. It moves in a 2d plane.
    // This is so the finger doesn't bend over on itself in ways that
    // are unnatural.
    [SerializeField] private AnimationCurve _targetLocalYHighLimit;
    [SerializeField] private AnimationCurve _targetLocalYLowLimit;
    [SerializeField] private AnimationCurve _targetLocalXLimit;

    private Transform[] _possibleObjectsOfInterest;
    public Transform[] PossibleObjectsOfInterest {
        set { _possibleObjectsOfInterest = value; }
    }

    private static int NumFingers;
    private int _id;
    private float _fingerLength = 0f;
    private FingerState _state;
    private Vector3 _surfaceNormal;
    private Transform _objectOfInterest;
    private Vector3 _locationOnObject;
    private System.Random _rng;

    void Start()
    {
        // Each finger gets a unique ID number, used to generate unique noisy wobble animation signal
        _id = NumFingers++;

        // Store the finger's maximum reach
        _fingerLength = Vector3.Distance(_root.position, _tip.position);

        // Make a unique random number generator for this finger
        _rng = new System.Random(Random.Range(0, int.MaxValue));

        // Store the orientation of the surface the finger was placed on
        _surfaceNormal = transform.up;

        // Initialize the finger's first point of interest
        ChangeObjectOfInterest();
        ChangePointOfInterest();
        StartCoroutine(UpdatePOI());
    }

    void Update()
    {
        // Change behavioral state based on how close the point of interest is
        float detectionRange = _fingerLength * 3f;
        float proximity = Vector3.Distance(_root.position, _objectOfInterest.transform.position);
        if (proximity < detectionRange) {
            _state = FingerState.Observing;
        } else {
            _state = FingerState.Idle;
        }
        
        // Run the behaviour for the current state
        switch (_state) {
            case FingerState.Idle:
                Idle();
                break;
            case FingerState.Observing:
                Observe();
                break;
        }
    }

    private void Idle() {
        // Pick a fixed target point for the finger tip, one that looks kind of relaxed
        Vector3 localTargetPosition = new Vector3(0f, _fingerLength * 0.7f, 0.2f);

        // Add a gentle swaying motion around the target point
        Vector3 targetPositionWobble = new Vector3(
            -1f + 2f * Mathf.PerlinNoise(Time.time * 2f * _wobbleSpeed, _id * 11f),
            -1f + 2f * Mathf.PerlinNoise(Time.time * 5f * _wobbleSpeed, _id * 13f),
            -1f + 2f * Mathf.PerlinNoise(Time.time * 3f * _wobbleSpeed, _id * 7f)
        ) * (_fingerLength * 0.4f);

        localTargetPosition += targetPositionWobble;

        // Smoothly move from current point to newly calculated point
        _ikTarget.localPosition = Vector3.Lerp(_ikTarget.localPosition, localTargetPosition, 1f * Time.deltaTime);
    }

    private void Observe() {
        /*
        The finger has two ways to move:
        - Rotate the entire finger in a circular motion from its base, like a turret
        - Extend and curl the finger inside a 2D Plane, by moving the target for the
        finger tip inside this plane
        */

        /* Rotating the base */

        // Target point is somewhere around the center of the object of interest (see UpdatePOI())
        Vector3 targetPosition = _objectOfInterest.position + _locationOnObject;

        // Determine the direction to the given target along the surface the finger is sitting on
        Vector3 targetDirection = Vector3.ProjectOnPlane(targetPosition - transform.position, _surfaceNormal);
        targetDirection.Normalize();

        // Make a rotation that aligns the base of the finger to look at the target
        Quaternion targetRotation = Quaternion.LookRotation(targetDirection, _surfaceNormal);

        // Apply the rotation smoothly over time
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 2f * Time.deltaTime);

        /* Moving the finger tip */

        // We want to analyze the target position in the finger's local coordinate space
        Vector3 localTargetPosition = transform.InverseTransformPoint(targetPosition);

        // Add an excited swaying motion around the target point
        Vector3 targetPositionWobble = new Vector3(
            -1f + 2f * Mathf.PerlinNoise(Time.time * 7f * _wobbleSpeed, _id * 11f),
            -1f + 2f * Mathf.PerlinNoise(Time.time * 5f * _wobbleSpeed, _id * 13f),
            -1f + 2f * Mathf.PerlinNoise(Time.time * 9f * _wobbleSpeed, _id * 7f)
        ) * (_fingerLength * 0.4f);
        localTargetPosition += targetPositionWobble;

        // This makes it so that
        // - the Y axis indicates the finger extending out from the base, inside the 2d plane
        // - the Z axis indicated the finger moving up and down, inside the 2d plane
        // - the X axis indicates the finger moving left and right, out of the 2d plane

        // We use some curve profiles to limit where the finger tip target can go, inside that 2d plane
        // The curves take the local Y position as input, and return the min/max range of motion in X and Z
        // Finger motion is symmetric on the X axis (I can move my finger left/right a bit), but curls distinctly one way along Z
        // (Note: I slightly messed up the naming of the curves here)
        float zHighLimit = _targetLocalYHighLimit.Evaluate(localTargetPosition.y / _fingerLength);
        float zLowLimit = _targetLocalYLowLimit.Evaluate(localTargetPosition.y / _fingerLength);
        float xLimit = _targetLocalXLimit.Evaluate(localTargetPosition.y / _fingerLength);

        // We apply the limits to the target position
        localTargetPosition.y = Mathf.Clamp(localTargetPosition.y, _fingerLength * 0.2f, _fingerLength);
        localTargetPosition.z = -Mathf.Clamp(-localTargetPosition.z, zLowLimit * _fingerLength, zHighLimit * _fingerLength);
        localTargetPosition.x = Mathf.Clamp(localTargetPosition.x, -xLimit * _fingerLength, xLimit * _fingerLength);

        // Smoothly move from current point to newly calculated point
        _ikTarget.localPosition = Vector3.Lerp(_ikTarget.localPosition, localTargetPosition, 3f * Time.deltaTime);
    }

    private IEnumerator UpdatePOI() {
        // This loop is scheduled to run from the Start() function
        // It runs forever, well, until you stop the application anyway

        while (true) {
            // Roll a 10-sided dice, if we roll a 0, we change the object we're interested in
            if (_rng.Next(10) == 0) {
                ChangeObjectOfInterest();
            }
            // Roll a 4-sided dice, if we roll a 0, we change the point on the object we are looking at
            if (_rng.Next(4) == 0) {
                ChangePointOfInterest();
            }

            // Wait anywhere between 0.5 and 1.0 seconds before entering this loop again
            // This makes it so that no 2 fingers switch their point of interest
            // exactly at the same time.
            yield return new WaitForSeconds(0.5f + 0.5f * (float)_rng.NextDouble());
        }
    }

    private void ChangeObjectOfInterest() {
        // Pick a random object of interest from the list we have
        _objectOfInterest = _possibleObjectsOfInterest[_rng.Next(_possibleObjectsOfInterest.Length)];
    }

    private void ChangePointOfInterest() {
        // Pick a random point in a spherical range around the object of interest
        _locationOnObject = Random.insideUnitSphere * 0.2f;
    }

    private void OnDrawGizmos() {
        if (!Application.isPlaying) {
            return;
        }

        // Draw the finger tip target location (visible if the Gizmos button is enabled in the editor)
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(_ikTarget.position, 0.033f);
    }

    public enum FingerState {
        Idle,
        Observing
    }
}

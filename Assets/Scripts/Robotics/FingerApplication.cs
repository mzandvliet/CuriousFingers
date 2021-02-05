using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class FingerApplication : MonoBehaviour {
    [SerializeField] private GameObject _fingerPrototype;
    [SerializeField] private Collider[] _roomSurfaces;
    [SerializeField] private float _fingerSpacing = 0.6f;

    [SerializeField] private Transform _player;

    private Transform[] _pointsOfInterest;

    // On startup
    private void Awake() {
        // Make a list of the vr-tracked bodyparts
        // (We assume all direct children of the player object are individual body parts)
        _pointsOfInterest = new Transform[_player.childCount];
        for (int i = 0; i < _pointsOfInterest.Length; i++)
        {
            _pointsOfInterest[i] = _player.GetChild(i);
        }

        // For each surface in the room
        for (int i = 0; i < _roomSurfaces.Length; i++)
        {
            var surface = _roomSurfaces[i].transform;

            // Determine a grid of regularly spaced fingers along the surface
            int stepsHorizontal = Mathf.FloorToInt(surface.localScale.x * 0.8f / _fingerSpacing);
            int stepsVertical = Mathf.FloorToInt(surface.localScale.z * 0.8f / _fingerSpacing);
            
            // For each column in the grid
            for (int x = 0; x < stepsHorizontal; x++)
            {
                // For each row in the grid
                for (int z = 0; z < stepsVertical; z++)
                {
                    // Make a copy of the finger
                    var fingerObj = GameObject.Instantiate(_fingerPrototype);

                    fingerObj.transform.position =
                        // wall center
                        surface.position +
                        
                        // push out towards the surface
                        surface.up * surface.localScale.y * 0.4f +

                        // place horigontally
                        surface.right * surface.localScale.x * -0.4f +
                        surface.right * _fingerSpacing * x +

                        // place vertically
                        surface.forward * surface.localScale.z * -0.4f +
                        surface.forward * _fingerSpacing * z;

                    // random rotation (think of a clock placed on the surface)
                    Vector3 lookDir =
                        surface.right * (-1f + 2f * Random.value) +
                        surface.forward * (-1f + 2f * Random.value);
                    lookDir.Normalize();
                    
                    fingerObj.transform.LookAt(lookDir, surface.up);
                    fingerObj.transform.parent = transform;

                    // Give the finger controller component the points of interest it needs
                    var finger = fingerObj.GetComponent<FingerController>();
                    finger.PossibleObjectsOfInterest = _pointsOfInterest;
                }
            }
        }

        // Disable the prototype, we don't need it anymore
        _fingerPrototype.gameObject.SetActive(false);
    }
}
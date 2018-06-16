using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateCameraWithAlt : MonoBehaviour {
	[SerializeField] Camera cam;
	[SerializeField] float mouseSensitivity = 1f;
	
	void Start () {
		if(cam == null) cam = Camera.main;
	}
	
	void Update () {
		if(Input.GetKey(KeyCode.LeftAlt)) {
			cam.transform.Rotate(new Vector3(0f, Input.GetAxis("Mouse X") * mouseSensitivity, 0f), Space.World);
			cam.transform.Rotate(new Vector3(-Input.GetAxis("Mouse Y") * mouseSensitivity,  0f, 0f), Space.Self);
		}
	}
}

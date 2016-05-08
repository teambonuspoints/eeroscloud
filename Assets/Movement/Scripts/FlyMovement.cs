using UnityEngine;
using System.Collections;

public class FlyMovement : MonoBehaviour {

	public float thrust = 10.0f;

	// Update is called once per frame
	void Update () {
		transform.position += Camera.main.transform.forward * thrust * Time.deltaTime;

	}
}

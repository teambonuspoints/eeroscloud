using UnityEngine;
using System.Collections;


public class FlyMovement : MonoBehaviour {

	public float thrust = 10.0f;

	private float zmax = 10;
	private float xmax = 120;
	private float ymax = 40;

	// Update is called once per frame
	void Update () {
		transform.position += Camera.main.transform.forward * thrust * Time.deltaTime;

		Vector3 cPos = transform.position;



		if (cPos.z > zmax) {
			Vector3 newPos = Camera.main.transform.position;
			newPos.z = -1.0f * zmax;
			transform.position = newPos;
		} 
		if (cPos.z < -1 * zmax) {
			Vector3 newPos = Camera.main.transform.position;
			newPos.z = 1.0f * zmax;
			transform.position = newPos;
		}
		if (cPos.x > xmax) {
			Vector3 newPos = Camera.main.transform.position;
			newPos.x = -1.0f * xmax;
			transform.position = newPos;
		}
		if (cPos.x < -1 * xmax) {
			Vector3 newPos = Camera.main.transform.position;
			newPos.x = 1.0f * xmax;
			transform.position = newPos;
		}
		if (cPos.y > ymax) {
			Vector3 newPos = Camera.main.transform.position;
			newPos.y = -1.0f * ymax;
			transform.position = newPos;
		}
		if (cPos.y < -1 * ymax) {
			Vector3 newPos = Camera.main.transform.position;
			newPos.y = 1.0f * ymax;
			transform.position = newPos;
		}



		
	}
}

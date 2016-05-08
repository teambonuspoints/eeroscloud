using UnityEngine;
using System.Collections;


public class FlyMovement : MonoBehaviour {

	public float thrust = 10.0f;

	private float zmin = -90.0f;
	private float zmax = 130.0f;
	private float xmin = -70.0f;
	private float xmax = 130.0f;
	private float ymin = -50.0f;
	private float ymax = 50.0f;

	// Update is called once per frame
	void Update () {
		transform.position += Camera.main.transform.forward * thrust * Time.deltaTime;

		Vector3 cPos = transform.position;



		if (cPos.z > zmax) {
			Vector3 newPos = Camera.main.transform.position;
			newPos.z = zmin;
			transform.position = newPos;
		} 
		if (cPos.z < -1 * zmax) {
			Vector3 newPos = Camera.main.transform.position;
			newPos.z =  zmax;
			transform.position = newPos;
		}
		if (cPos.x > xmax) {
			Vector3 newPos = Camera.main.transform.position;
			newPos.x = xmin;
			transform.position = newPos;
		}
		if (cPos.x < -1 * xmax) {
			Vector3 newPos = Camera.main.transform.position;
			newPos.x = xmax;
			transform.position = newPos;
		}
		if (cPos.y > ymax) {
			Vector3 newPos = Camera.main.transform.position;
			newPos.y =  ymin;
			transform.position = newPos;
		}
		if (cPos.y < -1 * ymax) {
			Vector3 newPos = Camera.main.transform.position;
			newPos.y = ymax;
			transform.position = newPos;
		}



		
	}
}

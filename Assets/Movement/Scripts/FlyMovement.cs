using UnityEngine;
using System.Collections;

public class FlyMovement : MonoBehaviour {

	public float thrust = 10.0f;
	public CardboardHead head;

	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
		
		transform.position += thrust * head.Gaze.direction * Time.deltaTime;

		//this.transform.position += this.transform.forward * thrust;
	}
}

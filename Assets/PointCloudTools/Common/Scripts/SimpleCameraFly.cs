// modified from Simple Free Fly Camera script: http://forum.unity3d.com/threads/167604-Simplle-Free-Fly-Camera-script
// http://unitycoder.com

using UnityEngine;
using System.Collections;

namespace unitycodercom_pointcloud_extras
{

	public class SimpleCameraFly : MonoBehaviour 
	{

		public float flySpeed = 3; // default speed
		public float accelerationRatio = 2; // when shift is pressed
		public float slowDownRatio = 0.5f; // when ctrl is pressed

		// main loop
		void Update () 
		{

			// shift is pressed down
			if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
			{
				flySpeed *= accelerationRatio; // increase flyspeed
			}
			
			if (Input.GetKeyUp(KeyCode.LeftShift) || Input.GetKeyUp(KeyCode.RightShift))
			{
				flySpeed /= accelerationRatio; // decrease flyspeed back to normal
			}

			if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl))
			{
				flySpeed *= slowDownRatio; // decrease flyspeed
			}

			if (Input.GetKeyUp(KeyCode.LeftControl) || Input.GetKeyUp(KeyCode.RightControl))
			{
				flySpeed /= slowDownRatio; // // increase flyspeed back to normal
			}

			if (Input.GetAxis("Vertical") != 0)
			{
				transform.Translate(transform.forward * flySpeed * Input.GetAxis("Vertical") * Time.deltaTime, Space.World);
			}
			if (Input.GetAxis("Horizontal") != 0)
			{
				transform.Translate(transform.right * flySpeed * Input.GetAxis("Horizontal")* Time.deltaTime, Space.World);
			}
			if (Input.GetKey(KeyCode.E))
			{
				transform.Translate(transform.up * flySpeed*0.5f* Time.deltaTime, Space.World);
			}
			else if (Input.GetKey(KeyCode.Q))
			{
				transform.Translate(-transform.up * flySpeed*0.5f* Time.deltaTime, Space.World);
			}


		} // update
	} // class
} // namespace

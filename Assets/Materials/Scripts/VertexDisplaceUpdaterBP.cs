using UnityEngine;
using System.Collections;

// USAGE: attach to gameobject with special vertex displacement shader, assign displacerObj (shader checks distance to this object)


public class VertexDisplaceUpdaterBP : MonoBehaviour {

	public GameObject displacerObj;

	void Update () 
	{
		Material mat = GetComponent<Renderer>().sharedMaterial;
		Transform tr = displacerObj.transform;
		Vector3 pos = tr.position;
		Vector3 fwd = tr.forward;
		mat.SetVector("_Pos", new Vector4(pos.x, pos.y, pos.z, 1));
		mat.SetVector("_Dir", new Vector4(fwd.x, fwd.y, fwd.z, 1));
	}
}
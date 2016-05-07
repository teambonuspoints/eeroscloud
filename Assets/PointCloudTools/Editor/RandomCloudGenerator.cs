// Random Point Cloud Binary Generator
// Creates point cloud with given amount of points for testing

using UnityEditor;
using UnityEngine;
using System.Collections;
using System.IO;

namespace unitycodercom_RandomCloudGenerator
{
		
	public class RandomCloudGenerator : EditorWindow
	{
		private static string appName = "RandomCloudGenerator";
		private bool readRGB = true;
		private int plyVertexCount = 1000000;
		private byte binaryVersion = 1;
		private int width=500;
		private int height=500;
		private int depth=500;
		private bool createXYZ = false;

		// create menu item and window
	    [MenuItem ("Window/PointCloudTools/Create test binary cloud",false,200)]
	    static void Init () 
		{
			RandomCloudGenerator window = (RandomCloudGenerator)EditorWindow.GetWindow (typeof (RandomCloudGenerator));
			window.titleContent = new GUIContent(appName);
			window.minSize = new Vector2(340,280);
			window.maxSize = new Vector2(340,284);
	    }

		// window closed
		void OnDestroy()
		{
			// could do cleaning up here, if needed
		}

		
		// main loop
		void OnGUI () 
		{
			plyVertexCount = EditorGUILayout.IntField("Amount of points",plyVertexCount);

			width = EditorGUILayout.IntField("Area width (z)",width);
			height = EditorGUILayout.IntField("Area height (y)",width);
			depth = EditorGUILayout.IntField("Area depth (z)",width);

			readRGB = EditorGUILayout.ToggleLeft(new GUIContent("Create RGB cloud",null,""), readRGB);
			createXYZ = EditorGUILayout.ToggleLeft(new GUIContent("Create XYZ ascii file",null,""), createXYZ);
			EditorGUILayout.Space();

			// convert button
			if(GUILayout.Button (new GUIContent ("Create Test Binary", "Creates random binary cloud"), GUILayout.Height(40))) 
			{
				CreateRandomCloud();
			}
			GUI.enabled = true;
		} //ongui


		bool IsNullOrEmptyLine(string line)
		{
			if (line.Length<3 || line == null || line == string.Empty) {Debug.LogError("First line of the file is empty..quitting!"); return true;}
			return false;
		}



		// conversion function
		void CreateRandomCloud()
		{
			if (plyVertexCount<1 && plyVertexCount>99999999) return;

			var saveFilePath = EditorUtility.SaveFilePanel("Save binary file","Assets/","random.bin","bin");
			float x=0,y=0,z=0;//,r=0,g=0,b=0; //,nx=0,ny=0,nz=0;; // init vals
			float progress = 0;
			long progressCounter=0;				

			// prepare to start saving binary file

			// write header
			var writer = new BinaryWriter(File.Open(saveFilePath, FileMode.Create));
			writer.Write(binaryVersion);
			writer.Write((System.Int32)plyVertexCount);
			writer.Write(readRGB);

			progress = 0;
			progressCounter=0;				

			long rowCount = 0;
			bool haveMoreToRead = true;

			// process all points
			while (haveMoreToRead)
			{

				if (progressCounter>65000)
				{
					EditorUtility.DisplayProgressBar(appName,"Creating random point cloud",progress/plyVertexCount);
					progressCounter=0;
				}

				progressCounter++;
				progress++;

				x = Random.Range(-width*0.5f,width*0.5f);
				y = Random.Range(-height*0.5f,height*0.5f);
				z = Random.Range(-depth*0.5f,depth*0.5f);

				writer.Write(x);
				writer.Write(y);
				writer.Write(z);
						
				// if have color data
				if (readRGB)
				{
					Color c = new Color(Random.value, Random.value, Random.value,1);
					writer.Write(c.r);
					writer.Write(c.g);
					writer.Write(c.b);
				}

				rowCount++;
				if (rowCount>=plyVertexCount) haveMoreToRead = false;

			}

			writer.Close();

			Debug.Log(appName+"> Binary file saved: "+saveFilePath + " ("+plyVertexCount+" points)");
			EditorUtility.ClearProgressBar();

			if (createXYZ) CreateXYZ();


		}

		void CreateXYZ()
		{
			var saveFilePath = EditorUtility.SaveFilePanel("Save XYZ file","Assets/","random.xyz","xyz");
			float x=0,y=0,z=0;//,r=0,g=0,b=0; //,nx=0,ny=0,nz=0;; // init vals
			float progress = 0;
			long progressCounter=0;				
			
			// prepare to start saving binary file
			
			// write header
			var writer = new StreamWriter(File.Open(saveFilePath, FileMode.Create));
			//writer.Write(binaryVersion);
			//writer.Write((System.Int32)plyVertexCount);
			//writer.Write(readRGB);

			string sep = " ";

			progress = 0;
			progressCounter=0;				
			
			long rowCount = 0;
			bool haveMoreToRead = true;

			// TEST
			//Vector3[] cloud = new Vector3[plyVertexCount];



			// process all points
			while (haveMoreToRead)
			{
				
				if (progressCounter>65000)
				{
					EditorUtility.DisplayProgressBar(appName,"Creating random point cloud",progress/plyVertexCount);
					progressCounter=0;
				}
				
				progressCounter++;
				progress++;
				
				x = Random.Range(-width*0.5f,width*0.5f);
				y = Random.Range(-height*0.5f,height*0.5f);
				z = Random.Range(-depth*0.5f,depth*0.5f);



				//cloud[rowCount] = new Vector3(x,y,z);

				//Debug.DrawLine(cloud[rowCount],cloud[rowCount]+Vector3.up*rowCount,Color.red,20);

				// if have color data
				if (readRGB)
				{
					writer.WriteLine(x+sep+y+sep+z+sep+RandomColorValue()+sep+RandomColorValue()+sep+RandomColorValue());
				}else{
					writer.WriteLine(x+sep+y+sep+z);
				}
				
				rowCount++;
				if (rowCount>=plyVertexCount) haveMoreToRead = false;
				
			}
			
			writer.Close();
			
			Debug.Log(appName+"> XYZ File saved: "+saveFilePath + " ("+plyVertexCount+" points)");
			EditorUtility.ClearProgressBar();




		}

		int RandomColorValue()
		{
			return Random.Range(0,255);
		}



		bool ValidateSaveAndRead(string path, string fileToRead)
		{
			if(path.Length < 1) {Debug.Log(appName+"> Save cancelled..");	return false;}
			return true;
		}


	} // class
} // namespace

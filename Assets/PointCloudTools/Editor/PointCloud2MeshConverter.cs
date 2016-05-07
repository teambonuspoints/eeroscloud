// Point Cloud to Unity Mesh Converter
// Converts pointcloud data into multiple mesh assets
// http://unitycoder.com

using UnityEditor;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

#pragma warning disable 0219 // disable unused var warnings


namespace unitycodercom_PointCloud2MeshConverter
{
	
	public class PointCloud2MeshConverter : EditorWindow
	{
		private static string appName = "PointCloud2Mesh";
		private Object sourceFile;

		private bool readRGB = false;
		private bool readIntensity = false;
		private bool readNormals = false;
		private bool useUnitScale = true; // scaling enabled
		private float unitScale = 0.001f;
		private bool flipYZ = true;
		private bool autoOffsetNearZero = true; // takes first point value as offset
		private bool enableManualOffset = false;
		private Vector3 manualOffset = Vector3.zero;

		// advanced settings
		private bool optimizePoints = false;
		private bool createLODS=false;
		private int lodLevels=3; // including full mesh (so 2 are generated)
		private int minLodVertexCount=1000; // last LOD mesh has this many verts
		private bool decimatePoints = false;
		private int removeEveryNth = 5;
//		private bool forceRecalculateNormals = false;

		// mesh generation stuff
		private int vertCount=65000;
		private Material meshMaterial;
		private List<Mesh> cloudList = new List<Mesh>();
		private int meshCounter = 1;
		private GameObject folder;
		private long masterPointCount = 0;
		private string savePath;
		// create menu item and window
		[MenuItem ("Window/PointCloudTools/Convert Point Cloud To Unity Meshes",false,2)]
		static void Init () 
		{
			PointCloud2MeshConverter window = (PointCloud2MeshConverter)EditorWindow.GetWindow (typeof (PointCloud2MeshConverter));
			window.titleContent = new GUIContent(appName);
			window.minSize = new Vector2(340,560);
			window.maxSize = new Vector2(340,564);
		}

		// window closed
		void OnDestroy()
		{
			// TODO: cleaning up, if needed
		}

		
		// main loop
		void OnGUI () 
		{
			// source file
			GUILayout.Label ("Point Cloud source file", EditorStyles.boldLabel);
			sourceFile = EditorGUILayout.ObjectField(sourceFile, typeof(Object), false);

			// TODO: only get fileinfo once, no need to request again
			GUILayout.Label (sourceFile!=null?"file:"+GetSelectedFileInfo():"",EditorStyles.miniLabel);

			EditorGUILayout.Space();

			// start import settings
			GUILayout.Label ("Import settings", EditorStyles.boldLabel);

			EditorGUILayout.BeginHorizontal();
			readRGB = EditorGUILayout.ToggleLeft(new GUIContent("Read RGB values",null,"Read R G B values"), readRGB,GUILayout.Width(160));
			readIntensity = EditorGUILayout.ToggleLeft(new GUIContent("Read Intensity value",null,"Read intensity value"), readIntensity);
			readRGB = readIntensity?false:readRGB;
			EditorGUILayout.EndHorizontal();


			readNormals = EditorGUILayout.ToggleLeft(new GUIContent("Read Normal values (PLY)",null,"Only for .PLY files"), readNormals);

			// extra options
			EditorGUILayout.Space();
			useUnitScale = EditorGUILayout.BeginToggleGroup(new GUIContent("Scale values",null,"Enable scaling"), useUnitScale);
			unitScale = EditorGUILayout.FloatField(new GUIContent(" Scaling multiplier",null,"To scale millimeters to unity meters, use 0.001"), unitScale);
			EditorGUILayout.EndToggleGroup();
			EditorGUILayout.Space();
			flipYZ = EditorGUILayout.ToggleLeft(new GUIContent("Flip Y & Z values",null,"Flip YZ values because Unity Y is up"), flipYZ);
			EditorGUILayout.Space();
			autoOffsetNearZero = EditorGUILayout.ToggleLeft(new GUIContent("Auto-offset near 0,0,0",null,"Takes first line from xyz data as offset"), autoOffsetNearZero);
			enableManualOffset = EditorGUILayout.BeginToggleGroup(new GUIContent("Add manual offset",null,"Add this offset to XYZ values"), enableManualOffset);
			autoOffsetNearZero = enableManualOffset?false:autoOffsetNearZero;

			manualOffset = EditorGUILayout.Vector3Field(new GUIContent("Offset"+(flipYZ?" (added after YZ values flip)":""),null,""), manualOffset);
			EditorGUILayout.EndToggleGroup();
			EditorGUILayout.Space();

			// advanced settings
			EditorGUILayout.Space();
			GUILayout.Label ("Advanced Settings", EditorStyles.boldLabel);
			EditorGUILayout.Space();
			optimizePoints = EditorGUILayout.ToggleLeft(new GUIContent("Optimize points *Not recommended yet",null,"Sorts points on X axis"), optimizePoints);
//			forceRecalculateNormals = EditorGUILayout.ToggleLeft(new GUIContent("Force RecalculateNormals()",null,"Note: Uses builtin RecalculateNormals(), it wont give correct normals"), forceRecalculateNormals);
			createLODS = EditorGUILayout.ToggleLeft(new GUIContent("Create LODS",null,""), createLODS);
			GUI.enabled = createLODS;
			lodLevels = EditorGUILayout.IntSlider(new GUIContent("LOD levels:",null,"Including LOD0 (main) mesh level"), lodLevels, 2, 4);
			//minLodVertexCount = EditorGUILayout.IntSlider(new GUIContent("Minimum LOD point count:",null,"How many points in the last (furthest) LOD mesh"), minLodVertexCount, 1, Mathf.Clamp(vertCount-1,1,65000));
			GUI.enabled = true;

			decimatePoints = EditorGUILayout.ToggleLeft(new GUIContent("Decimate points",null,"Skip rows from data"), decimatePoints);
			GUI.enabled = decimatePoints;
			removeEveryNth = EditorGUILayout.IntField(new GUIContent("Remove every Nth point",null,""), Mathf.Clamp(removeEveryNth,0,99999));
			GUI.enabled = true;


			// mesh settings
			EditorGUILayout.Space();
			GUILayout.Label ("Mesh Output settings", EditorStyles.boldLabel);
			EditorGUILayout.Space();
			vertCount = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("Vertices per mesh",null,"How many verts per mesh (max 65k). !Warning: Low values will create millions of files!"), vertCount),1,65000);
			meshMaterial = (Material)EditorGUILayout.ObjectField(new GUIContent("Mesh material",null,"Material & Shader for the meshes"),meshMaterial, typeof(Material), true);

			EditorGUILayout.Space();
			GUI.enabled = sourceFile==null?false:true; // disabled if no source selected
			if(GUILayout.Button (new GUIContent ("Convert to Meshes", "Convert source to meshes"), GUILayout.Height(40))) 
			{
				Convert2Mesh();
			}
			GUI.enabled = true;
		}


		void Convert2Mesh()
		{
			cloudList.Clear();
			meshCounter = 1;
			savePath = EditorUtility.SaveFilePanelInProject("Mesh assets output folder & basename","PointChunk","asset","Set base filename");

			EditorUtility.DisplayProgressBar(appName,"Checking file..",0.25f);

			// check path
			if(savePath.Length == 0) 
			{
				Debug.LogWarning(appName+"> Cancelled..");
				EditorUtility.ClearProgressBar();
				return;
			}

			string fileToRead= AssetDatabase.GetAssetPath(sourceFile);
			if(fileToRead.Length != 0) 
			{
				//Debug.Log ("readfile: "+path);
			}else{
				Debug.LogWarning(appName+"> Cannot find file ("+fileToRead+")");
				EditorUtility.ClearProgressBar();
				return;
			}
			
			if (!File.Exists(fileToRead))
			{
				Debug.LogWarning(appName+"> Cannot find file ("+fileToRead+")");
				EditorUtility.ClearProgressBar();
				return;
			}

			string fileExtension = Path.GetExtension(fileToRead).ToLower();

			if (fileExtension!=".ply")
			{
				if (readNormals) 
				{
					Debug.LogWarning(appName+"> Importing normals is only supported for .PLY files");
					readNormals = false;
				}
			}

			// TEMPORARY: Custom reader for Brekel binary data
			if (fileExtension==".bin")
			{
				Debug.Log(fileExtension);
				EditorUtility.ClearProgressBar();
				BrekelDataConvert(fileToRead);
				return;
			}

			// TEMPORARY: Custom reader for LAS binary data
			if (fileExtension==".las")
			{
				EditorUtility.ClearProgressBar();
				LASDataConvert(fileToRead);
				return;
			}

			masterPointCount=0;
			long lines=0;
			int dataCount=0; 
			PointData[] pointArray;
			//Vector3 minCorner = new Vector3(Mathf.Infinity,Mathf.Infinity,Mathf.Infinity);
			//Vector3 maxCorner = new Vector3(Mathf.NegativeInfinity,Mathf.NegativeInfinity,Mathf.NegativeInfinity);
			double minCornerX=Mathf.Infinity;
			double minCornerY=Mathf.Infinity;
			double minCornerZ=Mathf.Infinity;
			double maxCornerX=Mathf.NegativeInfinity;
			double maxCornerY=Mathf.NegativeInfinity;
			double maxCornerZ=Mathf.NegativeInfinity;

			using (StreamReader reader = new StreamReader(File.OpenRead(fileToRead)))
			{
				double x=0,y=0,z=0,nx=0,ny=0,nz=0;
				float r=0,g=0,b=0; //,a=0;
				int indexR=3,indexG=4,indexB=5;
				int indexNX=3,indexNY=4,indexNZ=5;
				int indexI=3;

				string rawLine = null;
				string origRawLine = null;
				string[] lineSplitted = null;
				int commentsLength=0;
				int commentLines=0;

				// formats
				bool replaceCommas=false; // for cgo, catia asc (depends on pc regional settings)

				// find first line of point data
				bool comments=true;
				bool hasNormals=false;

				// parse header
				while (comments && !reader.EndOfStream)
				{
					origRawLine = reader.ReadLine();

					// check for normals
					if (origRawLine.ToLower().Contains("property float nx")) hasNormals=true; 



					// early exit if certain file type
					if (fileExtension==".ply" && masterPointCount==0)
					{
						if (origRawLine.ToLower().Contains("element vertex"))
						{
							// get point count
							int tempParse=0;
							if (int.TryParse(origRawLine.Split(' ')[2], out tempParse))
							{
								masterPointCount = tempParse;
							}else{
								Debug.LogError("PLY Header parsing failed, point count not founded");
								EditorUtility.ClearProgressBar();
								return;

							}
						}
					}

					if ((fileExtension==".pts" || fileExtension==".cgo") && masterPointCount==0)
					{
						// get point count
						int tempParse=0;
						rawLine = Regex.Replace(origRawLine, "[^.0-9 ]+[^e\\-\\d]", "").Trim(); // cleanup non numeric

						if (int.TryParse(rawLine, out tempParse))
						{
							masterPointCount = tempParse;
							commentLines=1;
						}else{
							Debug.LogError(fileExtension.ToUpper()+" header parsing failed, point count not founded");
							EditorUtility.ClearProgressBar();
							return;
							
						}
					}
						    

//					rawLine = origRawLine.Replace(",","."); // for cgo/catia asc
					rawLine = Regex.Replace(origRawLine, "[^.0-9 ]+[^e\\-\\d]", ""); // cleanup non numeric
					rawLine = rawLine.Replace("   "," ").Replace("  "," ").Trim();

					lineSplitted = rawLine.Split(' ');

					if (rawLine.StartsWith("#") || rawLine.StartsWith("!") || rawLine.StartsWith("*") || rawLine.ToLower().Contains("comment") || (!ValidateColumns(lineSplitted.Length)))
					{
						commentsLength+=origRawLine.Length+1; // +1 is end of line?
						commentLines++;
					}else{ // actual data

						// test with split

						if (rawLine.Contains(",")) replaceCommas=true;

						if (replaceCommas) rawLine = rawLine.Replace(",","."); // for cgo/catia asc
						lineSplitted = rawLine.Split(' ');

						if (readRGB && lineSplitted.Length<6) {Debug.LogError("No RGB data founded after XYZ, disabling readRGB"); readRGB = false;}
						if (readIntensity && (lineSplitted.Length!=4 && lineSplitted.Length != 7)) {Debug.LogError("No Intensity data founded after XYZ, disabling readIntensity"); readIntensity = false;}

						if (readNormals)
						{
							//if (lineSplitted.Length!=6 && lineSplitted.Length != 7 && lineSplitted.Length != 9 && lineSplitted.Length != 10) {
							if (!hasNormals)
							{
								Debug.LogError("No normals data founded, disabling readNormals. ["+lineSplitted.Length+" values founded]"); 
								readNormals = false;
							}else{ // we have normals
								// for PLY, RGB values are at the end
								indexR+=3;
								indexG+=3;
								indexB+=3;
							}
						}else{ // check if normals are there, but dont use them
							if (hasNormals)
							{
								indexR+=3;
								indexG+=3;
								indexB+=3;
							}
						}

						dataCount = lineSplitted.Length;
						comments=false;
						lines++;
					}
				} // while (parsing header)

				bool skipRow = false;
				int skippedRows = 0;

				// get first data row
				if (!double.TryParse(lineSplitted[0], out x)) skipRow = true;
				if (!double.TryParse(lineSplitted[1], out y)) skipRow = true;
				if (!double.TryParse(lineSplitted[2], out z)) skipRow = true;


				if (skipRow)
				{
					skippedRows++;
					Debug.LogWarning("First point data row was skipped, conversion will most likely fail (rawline:"+rawLine+")");
				}

				
				if (enableManualOffset || autoOffsetNearZero)
				{
					manualOffset = flipYZ?new Vector3((float)x,(float)z,(float)y):new Vector3((float)x,(float)y,(float)z);
				}
				
				// scaling enabled, scale offset too
				if (useUnitScale) manualOffset*=unitScale;

				// jump back to start of first line
				EditorUtility.ClearProgressBar();


				// use header count value from ply, cgo, pts..
				if (fileExtension==".ply" || fileExtension==".pts" || fileExtension==".cgo")
				{
					lines = masterPointCount;
				}else{
					// calculate rest of data lines
					while (!reader.EndOfStream)
					{
						origRawLine = reader.ReadLine();
						if (replaceCommas) rawLine = origRawLine.Replace(",",".");
						rawLine = Regex.Replace(rawLine, "[^.0-9 ]+[^e\\-\\d]", ""); // cleanup non numeric
						rawLine= rawLine.Replace("   "," ").Replace("  "," ").Trim();

						if (lines % 64000==1)
						{
							if (EditorUtility.DisplayCancelableProgressBar(appName,"Counting lines..",lines/20000000.0f))
							{
								Debug.Log("Cancelled at: "+lines);
								EditorUtility.ClearProgressBar();
								return;
							}
						}

						// check if data is valid
						if (!rawLine.StartsWith("!"))
						{
							if (!rawLine.StartsWith("*"))
							{
								if (!rawLine.StartsWith("#"))
								{
									lineSplitted = rawLine.Split(' ');

									if (lineSplitted.Length == dataCount)
									{
										lines++;
									}
								}
							}
						}
					} // count points
					
					EditorUtility.ClearProgressBar();
				}

				// reset back to start
				reader.DiscardBufferedData();
				reader.BaseStream.Seek(0, SeekOrigin.Begin); 
				reader.BaseStream.Position = 0;

				if (commentLines>0)
				{
					for (int i = 0; i < commentLines; i++) {
						reader.ReadLine();
					}
				}

				masterPointCount = lines;

				pointArray = new PointData[masterPointCount];

				long rowCount = 0;
				bool readMore = true;
				double tempVal=0;

				//read all point cloud data here
				for (rowCount = 0; rowCount < masterPointCount-1; rowCount++) 
				{

					if (rowCount % 64000==1)
					{
						EditorUtility.DisplayProgressBar(appName,"Processing points..",rowCount/(float)lines);
						//progressCounter=0;
					}

					// process each line
					rawLine = reader.ReadLine().Trim();

					// trim duplicate spaces
					rawLine = rawLine.Replace(",","."); // for cgo/catia asc

					rawLine = Regex.Replace(rawLine, "[^.0-9 ]+[^e\\-\\d]", "").Trim(); // cleanup non numeric
					rawLine= rawLine.Replace("   "," ").Replace("  "," ").Trim();
					lineSplitted = rawLine.Split(' ');

					// have same amount of columns in data?
					if (lineSplitted.Length == dataCount)
					{

						if (!double.TryParse(lineSplitted[0], out x)) skipRow = true;
						if (!double.TryParse(lineSplitted[1], out y)) skipRow = true;
						if (!double.TryParse(lineSplitted[2], out z)) skipRow = true;

						if (readRGB)
						{
//							if (!float.TryParse(lineSplitted[indexR], out r)) skipRow = true;
//							if (!float.TryParse(lineSplitted[indexG], out g)) skipRow = true;
//							if (!float.TryParse(lineSplitted[indexB], out b)) skipRow = true;

							r = System.Convert.ToInt32(lineSplitted[indexR]);
							g = System.Convert.ToInt32(lineSplitted[indexG]);
							b = System.Convert.ToInt32(lineSplitted[indexB]);

							r/=255f;
							g/=255f;
							b/=255f;
						}

						if (readIntensity)
						{
							// TODO: handle different intensity values
							if (!float.TryParse(lineSplitted[indexI], out r)) skipRow = true;

							// re-range PTS intensity
							if (fileExtension==".pts")
							{
								r = Remap(r,-2048,2047,0,1);
							}

							g=r;
							b=r;
						}

						if (readNormals)
						{
							if (!double.TryParse(lineSplitted[indexNX], out nx)) skipRow = true;
							if (!double.TryParse(lineSplitted[indexNY], out ny)) skipRow = true;
							if (!double.TryParse(lineSplitted[indexNZ], out nz)) skipRow = true;
						}

						// if flip
						if (flipYZ)
						{
							tempVal = z;
							z=y;
							y=tempVal;

							// flip normals?
							if (readNormals)
							{
								tempVal = nz;
								nz=ny;
								ny=tempVal;
							}

						}

						// scaling enabled
						if (useUnitScale)
						{
							x *= unitScale;
							y *= unitScale;
							z *= unitScale;
						}
						
						// manual offset enabled
						if (autoOffsetNearZero || enableManualOffset)
						{
							x-=manualOffset.x;
							y-=manualOffset.y;
							z-=manualOffset.z;
						}

						// get cloud corners
						if (!skipRow)
						{
							minCornerX = System.Math.Min(x,minCornerX);
							minCornerY = System.Math.Min(y,minCornerY);
							minCornerZ = System.Math.Min(z,minCornerZ);

							maxCornerX = System.Math.Max(x,maxCornerX);
							maxCornerY = System.Math.Max(y,maxCornerY);
							maxCornerZ = System.Math.Max(z,maxCornerZ);

							pointArray[rowCount].vertex.Set((float)x,(float)y,(float)z);
							pointArray[rowCount].uv.Set((float)x,(float)y);
							pointArray[rowCount].indice = ((int)rowCount) % vertCount;
						}

						if (readRGB || readIntensity)
						{
							pointArray[rowCount].color.r = r;
							pointArray[rowCount].color.g = g;
							pointArray[rowCount].color.b = b;
							pointArray[rowCount].color.a = 0.5f;
						}

						if (readNormals) pointArray[rowCount].normal.Set((float)nx,(float)ny,(float)nz);


					}else{ // if row length
						skipRow=true;
					}
//					} // line len


					if (skipRow)
					{
						skippedRows++;
						skipRow = false;
					}else{
//						rowCount++;
					}

//					if (reader.EndOfStream)// || rowCount>=masterPointCount)
//					{
						//Debug.Log(reader.EndOfStream);
						//Debug.Log(rowCount>=masterPointCount);
						//readMore = false;
//						Debug.LogError("Reached end of file too early ("+rowCount+"/"+masterPointCount+")");
//						break;
//					}

				} // while reading file
				EditorUtility.ClearProgressBar();

				if (skippedRows>0) Debug.LogWarning("Skipped "+skippedRows.ToString()+" rows (out of "+masterPointCount+" rows) because of parsing errors");

			} // using reader

	

			// sort points, FIXME: something strange happens inside sort, points go missing or lose precision?
			if (optimizePoints)
			{
				EditorUtility.DisplayProgressBar(appName,"Optimizing points..",0.25f);

				// sort by val
				//System.Array.Sort<PointData>(pointArray, (a,b) => ((a.vertex.x)>(b.vertex.x))?1:-1);

				// distance sort
				//System.Array.Sort<PointData>(pointArray, (a,b) => (minCorner-a.vertex).sqrMagnitude.CompareTo((minCorner-b.vertex).sqrMagnitude));

				// TODO: sorting loses precision??
				// sort by x
				System.Array.Sort<PointData>(pointArray, (a,b) => (minCornerX-a.vertex.x).CompareTo(minCornerX-b.vertex.x));
				EditorUtility.ClearProgressBar();

				// TODO: split by slicing grid
			}

			// build mesh assets
			int indexCount=0;

			Vector3[] vertices2 = new Vector3[vertCount];
			Vector2[] uvs2 = new Vector2[vertCount];
			int[] triangles2 = new int[vertCount];
			Color[] colors2 = new Color[vertCount];
			Vector3[] normals2 = new Vector3[vertCount];

			EditorUtility.DisplayProgressBar(appName,"Creating "+((int)(pointArray.Length/vertCount))+" mesh arrays",0.75f);

			// process all point data into meshes
			for (int i = 0; i < pointArray.Length; i++)
			{
				vertices2[indexCount] = pointArray[i].vertex;
				uvs2[indexCount] = pointArray[i].uv;
				triangles2[indexCount] = pointArray[i].indice;
				if (readRGB || readIntensity) colors2[indexCount] = pointArray[i].color;
				if (readNormals) normals2[indexCount] = pointArray[i].normal;

				if (decimatePoints)
				{
					if (i % removeEveryNth == 0) indexCount++;
				}else{
					indexCount++;
				}

				if (indexCount>=vertCount || i==pointArray.Length-1)
				{
					var go = BuildMesh(vertices2, uvs2, triangles2, colors2, normals2);

					if (createLODS) BuildLODS(go, vertices2, uvs2, triangles2, colors2, normals2);

					indexCount=0;

					// need to clear arrays, should use lists otherwise last mesh has too many verts (or slice last array)
					System.Array.Clear(vertices2,0,vertCount);
					System.Array.Clear(uvs2,0,vertCount);
					System.Array.Clear(triangles2,0,vertCount);
					if (readRGB || readIntensity) System.Array.Clear(colors2,0,vertCount);
					if (readNormals) System.Array.Clear(normals2,0,vertCount);

				}
			}

			EditorUtility.ClearProgressBar();

			// save meshes

			EditorUtility.DisplayProgressBar(appName,"Saving "+(cloudList.Count)+" mesh assets",0.95f);


			string pad="";
			for (int i=0;i<cloudList.Count;i++)
			{
				if (i<1000) pad="0";
				if (i<100) pad="00";
				if (i<10) pad="000";
				AssetDatabase.CreateAsset(cloudList[i],savePath+pad+i);
				AssetDatabase.SaveAssets(); // not needed?
			} // save meshes

			EditorUtility.ClearProgressBar();
			
			AssetDatabase.Refresh();

			Debug.Log("Total amount of points processed:"+masterPointCount);

		} // convert2meshOptimized2


		bool ValidateColumns (int len)
		{
			bool valid = false;
			//                                             NNN
			//  XYZ       XYZI      XYZRGB    XYZIRGB   XYZXYZRGBA
			if (len==3 || len==4 || len==6 || len==7 || len==10)    valid = true;
			return valid;
		}




		void BrekelDataConvert(string fileToRead)
		{
			var reader = new BinaryReader(File.OpenRead(fileToRead));

			byte binaryVersion=reader.ReadByte();
			int numberOfFrames=reader.ReadInt32();
			//float frameRate=reader.ReadSingle(); // NOT YET USED
			reader.ReadSingle(); // skip framerate field
			bool containsRGB=reader.ReadBoolean();

			// TODO: if its 1, read our own binary
			if (binaryVersion!=2) Debug.LogWarning("BinaryVersion is not 2 - reading file most likely fails..");

			/*
			Debug.Log("binaryVersion:"+binaryVersion);
			Debug.Log("numberOfFrames:"+numberOfFrames);
			Debug.Log("frameRate:"+frameRate);
			Debug.Log("containsRGB:"+containsRGB);
			*/

			int pointCounter = 0; // array index
			Vector3[] vertices = new Vector3[vertCount];
			Vector2[] uvs = new Vector2[vertCount];
			int[] triangles = new int[vertCount];
			Color[] colors = new Color[vertCount];
			Vector3[] normals = new Vector3[vertCount];

			int[] numberOfPointsPerFrame;
			numberOfPointsPerFrame = new int[numberOfFrames];
			
			for (int i=0;i<numberOfFrames;i++)
			{
				numberOfPointsPerFrame[i] = reader.ReadInt32();//(int)System.BitConverter.ToInt32(data,byteIndex);
			}

			// Binary positions for each frame, not used
			for (int i=0;i<numberOfFrames;i++)
			{
				reader.ReadInt64();
			}


			float x,y,z,r,g,b;

			for (int frame=0;frame<numberOfFrames;frame++)
			{
				for (int i=0;i<numberOfPointsPerFrame[frame];i++)
				{
					x = reader.ReadSingle();
					y = reader.ReadSingle();
					z = reader.ReadSingle();

					if (containsRGB)
					{
						r = reader.ReadSingle();
						g = reader.ReadSingle();
						b = reader.ReadSingle();
						colors[pointCounter] = new Color(r,g,b,0.5f);
					}

					// scaling enabled
					if (useUnitScale)
					{
						x *= unitScale;
						y *= unitScale;
						z *= unitScale;
					}
					
					// manual offset enabled
					if (enableManualOffset)
					{
						x-=manualOffset.x;
						y-=manualOffset.x;
						z-=manualOffset.x;
					}



					// if flip
					if (flipYZ)
					{
						vertices[pointCounter]=new Vector3(x,z,y);
					}else{ // noflip
						vertices[pointCounter]=new Vector3(x,y,z);
					}

					uvs[pointCounter]=new Vector2(x,y);
					triangles[pointCounter]= pointCounter;
					
					pointCounter++;
					
					// do we have enough for this mesh?
					if (pointCounter>=vertCount || i==numberOfPointsPerFrame[frame]-1)
					{
						BuildMesh(vertices, uvs, triangles, colors, normals);
						pointCounter=0;
					}
				} // points on each frame

			} // frames
				

			// save meshes
			string pad="";
			for (int i=0;i<cloudList.Count;i++)
			{
				// TODO: padding for 1000 objects?
				if (cloudList.Count>99)
				{
					if (i<100) pad="0";
					if (i<10) pad="00";
				}else{
					if (i<10) pad="0";
				}
				AssetDatabase.CreateAsset(cloudList[i],savePath+pad+i+".asset");
				//AssetDatabase.SaveAssets(); // not needed?
			} // save meshes
			
			AssetDatabase.Refresh();
			reader.Close();
		}



		void LASDataConvert(string fileToRead)
		{
			PointData[] pointArray;
			Vector3 minCorner = new Vector3(Mathf.Infinity,Mathf.Infinity,Mathf.Infinity);
			Vector3 maxCorner = new Vector3(Mathf.NegativeInfinity,Mathf.NegativeInfinity,Mathf.NegativeInfinity);

			
			double x=0f,y=0f,z=0f;
			float r=0f,g=0f,b=0f;
			
			BinaryReader reader= new BinaryReader(File.OpenRead(fileToRead));
			string fileSignature = new string(reader.ReadChars(4));
			
			if (fileSignature != "LASF") Debug.LogError("LAS> FileSignature error: '"+fileSignature+"'");
			
			
			// NOTE: Currently most of this info is not used
			
			ushort fileSourceID = reader.ReadUInt16();
			ushort globalEncoding = reader.ReadUInt16();
			
			ulong projectID1 = reader.ReadUInt32(); // optional?
			ushort projectID2 = reader.ReadUInt16(); // optional?
			ushort projectID3 = reader.ReadUInt16(); // optional?
			string projectID4 = new string(reader.ReadChars(8)); // optional?
			
			byte versionMajor = reader.ReadByte();
			byte versionMinor = reader.ReadByte();
			
			string systemIdentifier = new string(reader.ReadChars(32));
			string generatingSoftware = new string(reader.ReadChars(32));
			
			ushort fileCreationDayOfYear = reader.ReadUInt16();
			ushort fileCreationYear = reader.ReadUInt16();
			ushort headerSize = reader.ReadUInt16();
			
			ulong offsetToPointData = reader.ReadUInt32();
			
			ulong numberOfVariableLengthRecords = reader.ReadUInt32();
			
			byte pointDataRecordFormat = reader.ReadByte();
			
			
			ushort PointDataRecordLength = reader.ReadUInt16();
			
			ulong legacyNumberOfPointRecords = reader.ReadUInt32();
			
			ulong[] legacyNumberOfPointsByReturn = new ulong[] {reader.ReadUInt32(),reader.ReadUInt32(),reader.ReadUInt32(),reader.ReadUInt32(),reader.ReadUInt32()};
			
			
			double xScaleFactor = reader.ReadDouble();
			double yScaleFactor = reader.ReadDouble();
			double zScaleFactor = reader.ReadDouble();
			
			double xOffset = reader.ReadDouble();
			double yOffset = reader.ReadDouble();
			double zOffset = reader.ReadDouble();
			double maxX = reader.ReadDouble();
			double minX = reader.ReadDouble();
			double MaxY = reader.ReadDouble();
			double minY = reader.ReadDouble();
			double maxZ = reader.ReadDouble();
			double minZ = reader.ReadDouble();
			
			
			// Only for 1.4
			if (versionMajor==1 && versionMinor==4)
			{
				ulong startOfFirstExtentedVariableLengthRecord = reader.ReadUInt64();
				ulong numberOfExtentedVariableLengthRecords = reader.ReadUInt32();
				
				ulong numberOfPointRecords = reader.ReadUInt64();
				ulong[] numberOfPointsByReturn = new ulong[] {reader.ReadUInt64(),reader.ReadUInt64(),reader.ReadUInt64(),reader.ReadUInt64(),reader.ReadUInt64(),
					reader.ReadUInt64(),reader.ReadUInt64(),reader.ReadUInt64(),reader.ReadUInt64(),reader.ReadUInt64(),
					reader.ReadUInt64(),reader.ReadUInt64(),reader.ReadUInt64(),reader.ReadUInt64(),reader.ReadUInt64()};

				
			}

			//ulong numberOfPointRecords = reader.ReadUInt64();
			// VariableLengthRecords
			if (numberOfVariableLengthRecords>0)
			{
				ushort vlrReserved = reader.ReadUInt16();
				string vlrUserID = new string(reader.ReadChars(16));
				ushort vlrRecordID = reader.ReadUInt16();
				ushort vlrRecordLengthAfterHeader = reader.ReadUInt16();
				string vlrDescription = new string(reader.ReadChars(32));
				/*
				Debug.Log("vlrReserved:"+vlrReserved);
				Debug.Log("vlrUserID:"+vlrUserID);
				Debug.Log("vlrRecordID:"+vlrRecordID);
				Debug.Log("vlrRecordLengthAfterHeader:"+vlrRecordLengthAfterHeader);
				Debug.Log("vlrDescription:"+vlrDescription);*/
				
			}
			
			// jump to points start pos
			reader.BaseStream.Seek((long)offsetToPointData, SeekOrigin.Begin);
			
			// format #2
			if (pointDataRecordFormat!=2 && pointDataRecordFormat!=3) Debug.LogWarning("LAS Import might fail - only pointDataRecordFormat #2 & #3 are supported (Your file is "+pointDataRecordFormat+")");
			if (versionMinor!=2) Debug.LogWarning("LAS Import might fail - only version LAS 1.2 is supported. (Your file is "+versionMajor+"."+versionMinor+")");
			
			masterPointCount = (int)legacyNumberOfPointRecords;
			
			// scaling enabled, scale manual offset
			if (useUnitScale) manualOffset*=unitScale;
			
			// progressbar
			float progress = 0;
			long progressCounter=0;				
			EditorUtility.ClearProgressBar();
			

			int rowCount = 0;
			bool haveMoreToRead = true;
			bool firstPointRead=false;


			int pointCounter = 0; // array index
			Vector3[] vertices = new Vector3[vertCount];
			Vector2[] uvs = new Vector2[vertCount];
			int[] triangles = new int[vertCount];
			Color[] colors = new Color[vertCount];
			Vector3[] normals = new Vector3[vertCount];

			pointArray = new PointData[masterPointCount];

			Debug.Log("Reading "+masterPointCount+" points..");

			// process all points
			while (haveMoreToRead)
			{
				if (progressCounter>65000)
				{
					//EditorUtility.DisplayProgressBar(appName,"Reading LAS file ",progress/masterPointCount);
					if (EditorUtility.DisplayCancelableProgressBar(appName,"Counting lines..",progress/masterPointCount))
					{
						Debug.Log("Cancelled at: "+progress);
						EditorUtility.ClearProgressBar();
						return;
					}
					progressCounter=0;
				}
				
				progressCounter++;
				progress++;
				
				long intX = reader.ReadInt32();
				long intY = reader.ReadInt32();
				long intZ = reader.ReadInt32();
				
				reader.ReadBytes(8); // unknown
				
				if (pointDataRecordFormat==3) reader.ReadBytes(8); // GPS Time for format#3
				
				var colorR = reader.ReadBytes(2); // RED
				var colorG = reader.ReadBytes(2); // GREEN
				var colorB = reader.ReadBytes(2); // BLUE
				
				x=intX*xScaleFactor+xOffset;
				y=intY*yScaleFactor+yOffset;
				z=intZ*zScaleFactor+zOffset;
				
				// manual scaling enabled
				if (useUnitScale)
				{
					x *= unitScale;
					y *= unitScale;
					z *= unitScale;
				}
				
				if (flipYZ)
				{
					double yy=y;
					y=z;
					z=yy;
				}
				
				if (autoOffsetNearZero)
				{
					if (!firstPointRead)
					{
						manualOffset = new Vector3((float)x,(float)y,(float)z);
						firstPointRead=true;
					}
					
					x-=manualOffset.x;
					y-=manualOffset.y;
					z-=manualOffset.z;
					
					
				}else{ // only 1 can be used autooffset or manual
					
					if (enableManualOffset)
					{
						x-=manualOffset.x;
						y-=manualOffset.y;
						z-=manualOffset.z;
					}
				}

				vertices[pointCounter]=new Vector3((float)x,(float)y,(float)z);

				if (readRGB)
				{
					r = (float)System.BitConverter.ToUInt16(colorR, 0);
					g = (float)System.BitConverter.ToUInt16(colorG, 0);
					b = (float)System.BitConverter.ToUInt16(colorB, 0);
					
					//if (rowCount<100)	Debug.Log("row:"+(rowCount+1)+" xyz:"+x+","+y+","+z+" : "+r+","+g+","+b);
					
					r = ((float)r)/256f;//float.Parse(row[3])/255;
					g = ((float)g)/256f;//float.Parse(row[4])/255;
					b = ((float)b)/256f;//float.Parse(row[5])/255;
					
					// fix for high values
					if (r>1) r/=256f;
					if (g>1) g/=256f;
					if (b>1) b/=256f;

					colors[pointCounter] = new Color(r,g,b,0.5f); // TODO: adjust alpha
					pointArray[rowCount].color = new Color(r,g,b,0.5f);
				}

				// get cloud corners

				minCorner.x = Mathf.Min((float)x,minCorner.x);
				minCorner.y = Mathf.Min((float)y,minCorner.y);
				minCorner.z = Mathf.Min((float)z,minCorner.z);
				
				maxCorner.x = Mathf.Max((float)x,maxCorner.x);
				maxCorner.y = Mathf.Max((float)y,maxCorner.y);
				maxCorner.z = Mathf.Max((float)z,maxCorner.z);
				
				pointArray[rowCount].vertex = new Vector3((float)x,(float)y,(float)z);
				pointArray[rowCount].uv = new Vector2((float)x,(float)y);
				pointArray[rowCount].indice = rowCount % vertCount;

				rowCount++;

				if (reader.BaseStream.Position >= reader.BaseStream.Length || rowCount>=masterPointCount)
				{
					haveMoreToRead = false;
				}

				
			} // while loop reading file
			
			// build mesh assets
			int indexCount=0;
			
			Vector3[] vertices2 = new Vector3[vertCount];
			Vector2[] uvs2 = new Vector2[vertCount];
			int[] triangles2 = new int[vertCount];
			Color[] colors2 = new Color[vertCount];
			Vector3[] normals2 = new Vector3[vertCount];
			
//			Debug.Log("Total points: "+pointArray.Length);
			
			EditorUtility.DisplayProgressBar(appName,"Creating "+((int)(pointArray.Length/vertCount))+" mesh arrays",0.5f);
			
			for (int i = 0; i < pointArray.Length; i++) 
			{
				vertices2[indexCount] = pointArray[i].vertex;
				uvs2[indexCount] = pointArray[i].uv;
				triangles2[indexCount] = pointArray[i].indice;
				if (readRGB) colors2[indexCount] = pointArray[i].color;
				
				if (decimatePoints)
				{
					if (i % removeEveryNth == 0)	indexCount++;
				}else{
					indexCount++;
				}
				
				if (indexCount>=vertCount || i==pointArray.Length-1)
				{
//					BuildMesh(vertices2, uvs2, triangles2, colors2, normals2);

					var go = BuildMesh(vertices2, uvs2, triangles2, colors2, normals2);
				
					if (createLODS) BuildLODS(go, vertices2, uvs2, triangles2, colors2, normals2);


					System.Array.Clear(vertices2,0,vertCount);
					System.Array.Clear(uvs2,0,vertCount);
					System.Array.Clear(triangles2,0,vertCount);
					if (readRGB || readIntensity) System.Array.Clear(colors2,0,vertCount);
					if (readNormals) System.Array.Clear(normals2,0,vertCount);


					indexCount=0;
				}
			}
			
			EditorUtility.ClearProgressBar();
			
			// save meshes
			
			EditorUtility.DisplayProgressBar(appName,"Saving "+(cloudList.Count)+" mesh assets",0.75f);
			
			
			string pad="";
			for (int i=0;i<cloudList.Count;i++)
			{
				if (i<1000) pad="0";
				if (i<100) pad="00";
				if (i<10) pad="000";
				AssetDatabase.CreateAsset(cloudList[i],savePath+pad+i+".asset");
				AssetDatabase.SaveAssets(); // not needed?
			} // save meshes
			
			EditorUtility.ClearProgressBar();
			
			AssetDatabase.Refresh();

			
			EditorUtility.ClearProgressBar();
		}


		// helper functions
		GameObject BuildMesh(Vector3[] verts,Vector2[] uvs, int[] tris, Color[] colors, Vector3[] normals)
		{

			GameObject target = new GameObject();

			target.AddComponent<MeshFilter>();
			target.AddComponent<MeshRenderer>();
			Mesh mesh = new Mesh();
			target.GetComponent<MeshFilter>().mesh = mesh;
			target.transform.name = "PC_"+meshCounter;
			target.GetComponent<Renderer>().sharedMaterial = meshMaterial;
			target.GetComponent<Renderer>().receiveShadows = false;
			target.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

			GameObject lodRoot=null;

			if (!folder)
			{
				folder= new GameObject();
				folder.name = "PointClouds";
			}


			if (createLODS) 
			{
				lodRoot = new GameObject();
				lodRoot.transform.parent = folder.transform;
				lodRoot.name = "lod_"+meshCounter;
				target.transform.parent = lodRoot.transform;
			}else{

				target.transform.parent = folder.transform;
			}


			// TODO: increment position
			//target.transform.position+=Vector3.right*meshCounter;

			mesh.vertices = verts;
			mesh.uv = uvs;
			if (readRGB||readIntensity) mesh.colors = colors;
			if (readNormals) mesh.normals = normals;

			// TODO: use scanner centerpoint and calculate direction from that..
			//if (forceRecalculateNormals) ...

			mesh.SetIndices (tris, MeshTopology.Points, 0);

			cloudList.Add(mesh);
			meshCounter++;

			return target;
		}

		void BuildLODS(GameObject target,Vector3[] verts,Vector2[] uvs, int[] tris, Color[] colors, Vector3[] normals)
		{
			GameObject go;
			LODGroup group;

			group = target.transform.parent.gameObject.AddComponent<LODGroup>();
			LOD[] lods = new LOD[lodLevels];


			float lerpStep = 1/(float)(lodLevels-1);
			float lerpVal=1;

			// make LODS
			for (int i=0; i<lodLevels; i++)
			{

				if (i==0) // main mesh
				{
					go = target;

				}else{ // make simplified meshes

					go = new GameObject();
					go.AddComponent<MeshFilter>();
					go.AddComponent<MeshRenderer>();

					Mesh mesh = new Mesh();
					go.GetComponent<MeshFilter>().mesh = mesh;
					go.transform.name = "PC_"+meshCounter+"_"+i.ToString();
					go.GetComponent<Renderer>().sharedMaterial = meshMaterial;
					go.GetComponent<Renderer>().receiveShadows = false;
					go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

					lerpVal-=lerpStep;
					int newVertCount = (int)Mathf.Lerp(minLodVertexCount,decimatePoints?vertCount/removeEveryNth:vertCount,lerpVal);

					var newVerts = new Vector3[newVertCount];
					var newUvs = new Vector2[newVertCount];
					var newColors = new Color[newVertCount];
					var newNormals = new Vector3[newVertCount];
					var newTris = new int[newVertCount];

					// get new verts
					float oldIndex=0;
					float stepSize= vertCount/(float)newVertCount;

					// TODO: if rounds to same index, take next instead of same point?
					float o=0;

					for (int newIndex = 0; newIndex < newVertCount; newIndex++)
					{
						newVerts[newIndex] = verts[Mathf.FloorToInt(o)];
						newUvs[newIndex] = uvs[Mathf.FloorToInt(o)];
						newTris[newIndex] = newIndex; //tris[newIndex];


						if (readRGB) newColors[newIndex] = colors[Mathf.FloorToInt(o)];

						/*
						// for debugging LODS, different colors per lod
						switch(i)
						{
						case 1:
							newColors[newIndex] = Color.red;
							break;
						case 2:
							newColors[newIndex] = Color.green;
							break;
						case 3:
							newColors[newIndex] = Color.yellow;
							break;
						case 4:
							newColors[newIndex] = Color.cyan;
							break;
						default:
							newColors[newIndex] = Color.magenta;
							break;
						}*/

						if (readNormals) newNormals[newIndex] = normals[Mathf.FloorToInt(o)];
						o+=stepSize;
					}

					mesh.vertices = newVerts;
					mesh.uv = newUvs;
					if (readRGB||readIntensity) mesh.colors = newColors;
					if (readNormals) mesh.normals = newNormals;
					mesh.SetIndices (newTris, MeshTopology.Points, 0);
				} // if master


				go.transform.parent = target.transform.parent;
				Renderer[] renderers = new Renderer[1];
				renderers[0] = go.GetComponent<Renderer>();
				float LODVal = Mathf.Lerp(1f,0.1f,(i+1)/(float)lodLevels);
				lods[i] = new LOD(LODVal, renderers);
			}// for create lods

			group.SetLODs(lods);
			group.RecalculateBounds();
		} //BuildLODS



		string GetSelectedFileInfo()
		{
			string tempFilePath = AssetDatabase.GetAssetPath(sourceFile);
			string tempFileName = Path.GetFileName(tempFilePath);
			return tempFileName+" ("+(new FileInfo( tempFilePath).Length/1000000) +"MB)";
		}

		bool ValidateSaveAndRead(string path, string fileToRead)
		{
			if(path.Length < 1) {Debug.Log(appName+"> Save cancelled..");	return false;}
			if(fileToRead.Length <1) { Debug.LogError(appName+"> Cannot find file ("+fileToRead+")"); return false; }
			if (!File.Exists(fileToRead)) { Debug.LogError(appName+"> Cannot find file ("+fileToRead+")"); return false; }
			
			if (Path.GetExtension(fileToRead).ToLower()==".bin") {Debug.LogError("Source file extension is .bin, binary file conversion is not supported"); return false;}
			
			return true;
		}

		float Remap(float source, float sourceFrom, float sourceTo, float targetFrom, float targetTo)
		{
			return targetFrom + (source-sourceFrom)*(targetTo-targetFrom)/(sourceTo-sourceFrom);
		}


	} // class
} // namespace

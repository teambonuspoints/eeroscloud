// Point Cloud to Binary Converter
// Saves pointcloud data into custom binary file, for faster viewing
// http://unitycoder.com
using System.Text.RegularExpressions;

#pragma warning disable 0219 // disable unused var warnings

using UnityEditor;
using UnityEngine;
using System.Collections;
//using System.Collections.Generic;
using System.IO;

namespace unitycodercom_PointCloud2Binary
{
		
	public class PointCloud2BinaryConverter : EditorWindow
	{
		private static string appName = "PointCloud2Binary";
		private Object sourceFile;
		//                                           0     1        2     3     4           5             6      7 
		private string[] fileFormats = new string[]{"XYZ","XYZRGB","CGO","ASC","CATIA ASC","PLY (ASCII)","LAS", "PTS"};
		private string[] fileFormatInfo = new string[]{"sample: \"32.956900 5.632800 5.673400\"", // XYZ
			"sample: \"32.956900 5.632800 5.670000 128 190 232\"", // XYZRGB
			"sample: \"683,099976 880,200012 5544,700195\"", // CGO
			"sample: \" -1192.9 2643.6 5481.2\"", // ASC
			"sample: \"X 31022.1919 Y -3314.1098 Z 6152.5000\"", //  CATIA ASC
			"sample: \"-0.680891 -90.6809 0 204 204 204 255\"", // PLY
			"info: LAS 1.4", // LAS
			"info: 42.72464 -16.1426 -32.16625 -399 88 23 98" // PTS
		};
		private int fileFormat = 0;
		private bool readRGB = false;
		private bool readIntensity = false; // only for PTS currently
		//		private bool readNormals = false;
		private bool useUnitScale = true;
		private float unitScale = 0.001f;
		private bool flipYZ = true;
		private bool autoOffsetNearZero = true; // takes first point value as offset
		private bool enableManualOffset = false;
		private Vector3 manualOffset = Vector3.zero;
		private bool plyHasNormals=false;

		private long masterPointCount = 0;

//		private bool compressed=false;

		private byte binaryVersion = 1;

		// create menu item and window
	    [MenuItem ("Window/PointCloudTools/Convert Point Cloud To Binary",false,1)]
	    static void Init () 
		{
			PointCloud2BinaryConverter window = (PointCloud2BinaryConverter)EditorWindow.GetWindow (typeof (PointCloud2BinaryConverter));
			window.titleContent = new GUIContent(appName);
			window.minSize = new Vector2(340,380);
			window.maxSize = new Vector2(340,384);
	    }

		// window closed
		void OnDestroy()
		{
			// could do cleaning up here, if needed
		}

		
		// main loop
		void OnGUI () 
		{
			// source field
			GUILayout.Label ("Point Cloud source file", EditorStyles.boldLabel);
			sourceFile = EditorGUILayout.ObjectField(sourceFile, typeof(Object), true);
			//GUILayout.Label (sourceFile!=null?("file:"+(Path.GetFileName(AssetDatabase.GetAssetPath(sourceFile)))):"",EditorStyles.miniLabel);

			GUILayout.Label (sourceFile!=null?"file:"+GetSelectedFileInfo():"",EditorStyles.miniLabel);


	        EditorGUILayout.Space();

			// file format dropdown
			GUILayout.Label (new GUIContent("Input file format","File extension can be anything, this selection decides the parsing method"));
			fileFormat = EditorGUILayout.Popup(fileFormat, fileFormats);
			GUILayout.Label (fileFormatInfo[fileFormat],EditorStyles.miniLabel);
			EditorGUILayout.Space();
			EditorGUILayout.Space();

			// import RGB
			GUILayout.Label ("Import settings", EditorStyles.boldLabel);
			EditorGUILayout.BeginHorizontal();
			if (fileFormat==0) // XYZ format
			{
				GUI.enabled = false;
				readRGB = false;
				readRGB = EditorGUILayout.ToggleLeft(new GUIContent("Read RGB values",null,"Read R G B (INT) values"), readRGB);
				GUI.enabled = true;
			}else{
				readRGB = EditorGUILayout.ToggleLeft(new GUIContent("Read RGB values",null,"Read R G B (INT) values"), readRGB);
			}

			if (fileFormat==7) // PTS
			{
				readIntensity = EditorGUILayout.ToggleLeft(new GUIContent("Read INT value",null,"Read PTS INT values"), readIntensity);
				readRGB = readIntensity?false:readRGB;
			}
			EditorGUILayout.EndHorizontal();
				
			/*
			if (fileFormats[fileFormat] == "PLY (ASCII)")
			{
				GUI.enabled = true;
			}else{
				GUI.enabled = false;
			}
			readNormals = EditorGUILayout.ToggleLeft(new GUIContent("Read Normal values",null,"Read NX NY NZ (float) values"), readNormals);
			GUI.enabled = true;*/
			EditorGUILayout.Space();

			// scaling
			useUnitScale = EditorGUILayout.BeginToggleGroup(new GUIContent("Scale values",null,"Enable scaling"), useUnitScale);
			unitScale = EditorGUILayout.FloatField(new GUIContent(" Scaling multiplier",null,"Multiply XYZ values with this multiplier"), unitScale);
			EditorGUILayout.EndToggleGroup();
			EditorGUILayout.Space();

			// flip y/z
			flipYZ = EditorGUILayout.ToggleLeft(new GUIContent("Flip Y & Z values",null,"Flip YZ values because Unity Y is up"), flipYZ);
			EditorGUILayout.Space();

			// offset
			autoOffsetNearZero = EditorGUILayout.ToggleLeft(new GUIContent("Auto-offset near 0,0,0",null,"Takes first line from xyz data as offset"), autoOffsetNearZero);
			enableManualOffset = EditorGUILayout.BeginToggleGroup(new GUIContent("Add Manual Offset",null,"Add this offset to XYZ values"), enableManualOffset);
			manualOffset = EditorGUILayout.Vector3Field(new GUIContent("Offset"+(flipYZ?" (added before YZ values flip)":""),null,""), manualOffset);
			EditorGUILayout.EndToggleGroup();
			EditorGUILayout.Space();
			GUI.enabled = sourceFile==null?false:true;

			// extras
			//compressed = EditorGUILayout.ToggleLeft(new GUIContent("Compress colors",null,"Compresses RGB into single float"), compressed);


			// convert button
			if(GUILayout.Button (new GUIContent ("Convert to Binary", "Convert source to binary"), GUILayout.Height(40))) 
			{
				Convert2Binary();
			}
			GUI.enabled = true;
		} //ongui

		bool IsNullOrEmptyLine(string line)
		{
			if (line.Length<3 || line == null || line == string.Empty) {Debug.LogError("First line of the file is empty..quitting!"); return true;}
			return false;
		}

		PeekHeaderData PeekHeaderASC(StreamReader reader)
		{
			PeekHeaderData ph = new PeekHeaderData();
			string line="";
			bool comments=true;

			while (comments && !reader.EndOfStream)
			{
				line = reader.ReadLine().Replace("   "," ").Replace("  "," ").Trim();
				ph.linesRead++;
				if (line.StartsWith("#") || line.StartsWith("!")) // temporary fix for Geomagic asc
				{
					// still comments
				}else{
					comments=false;
				}
			}
			
			string[] row = line.Split(' ');

			if (readRGB){if (row.Length<4) {Debug.LogError("No RGB data founded after XYZ, disabling readRGB"); readRGB = false;}}

			// check for CatiaASC
			if (line.ToLower().StartsWith("x")) {Debug.LogError("This looks like CATIA Asc data, you have selected 'ASC' input file format instead"); ph.readSuccess = false; return ph;}

			ph.x = double.Parse(row[0]);
			ph.y = double.Parse(row[1]);
			ph.z = double.Parse(row[2]);
			ph.readSuccess = true;
			
			return ph;
		}

		PeekHeaderData PeekHeaderCGO(StreamReader reader)
		{
			PeekHeaderData ph = new PeekHeaderData();

			string line = reader.ReadLine(); // first line has numbers
			line = line.Replace("   "," ").Replace("  "," ").Trim();
			line = reader.ReadLine(); // get sample 1st line
			ph.linesRead++;
			if (IsNullOrEmptyLine(line)) {ph.readSuccess = false; return ph;}
			string[] row = line.Split(' ');
			if (readRGB){if (row.Length<4) {Debug.LogError("No RGB data founded after XYZ, disabling readRGB"); readRGB = false;}}
			ph.x = double.Parse(row[0].Replace(",","."));
			ph.y = double.Parse(row[1].Replace(",","."));
			ph.z = double.Parse(row[2].Replace(",","."));
			ph.readSuccess = true;

			return ph;
		}

		PeekHeaderData PeekHeaderCATIA_ASC(StreamReader reader)
		{
			PeekHeaderData ph = new PeekHeaderData();
			string line = reader.ReadLine(); // first lines are not used
			line = line.Replace("   "," ").Replace("  "," ").Trim();
			line = reader.ReadLine(); // 2
			line = reader.ReadLine(); // 3
			line = reader.ReadLine(); // 4
			line = reader.ReadLine(); // 5
			line = reader.ReadLine(); // 6
			line = reader.ReadLine(); // 7
			line = reader.ReadLine(); // 8

			line = reader.ReadLine(); // first actual line
			ph.linesRead++;

			if (IsNullOrEmptyLine(line)) {ph.readSuccess = false; return ph;}
			string[] row = line.Split(' ');
			if (readRGB){if (row.Length<11) {Debug.LogError("No RGB data founded after XYZ, disabling readRGB"); readRGB = false;}}
			ph.x = double.Parse(row[1]);
			ph.y = double.Parse(row[3]);
			ph.z = double.Parse(row[5]);
			ph.readSuccess = true;

			return ph;
		}

		PeekHeaderData PeekHeaderXYZ(StreamReader reader)
		{
			PeekHeaderData ph = new PeekHeaderData();
			string line = reader.ReadLine(); // first actual line
			line = line.Replace("   "," ").Replace("  "," ").Trim();
			ph.linesRead++;
			// check if first line is NOT empty
			if (IsNullOrEmptyLine(line)) {ph.readSuccess = false; return ph;}

			string[] row = line.Split(' ');
			if (readRGB){if (row.Length<6) {Debug.LogError("No RGB data founded after XYZ, disabling readRGB"); readRGB = false;}}

			ph.x = double.Parse(row[0]);
			ph.y = double.Parse(row[1]);
			ph.z = double.Parse(row[2]);
			ph.readSuccess = true;

			return ph;
		}

		PeekHeaderData PeekHeaderPTS(StreamReader reader)
		{
			PeekHeaderData ph = new PeekHeaderData();
			string line = reader.ReadLine(); // first line is point count
			ph.linesRead++;

			line = line.Replace("   "," ").Replace("  "," ").Trim();
			line = Regex.Replace(line, "[^0-9]", ""); // remove non-numeric chars

			if (IsNullOrEmptyLine(line)) {ph.readSuccess = false; return ph;}

			if (!long.TryParse(line, out masterPointCount))
			{
				Debug.LogError("Failed to read point count from PTS file");
				ph.readSuccess = false; return ph;
			}

			line = reader.ReadLine(); // first actual line
			ph.linesRead++;

			line = line.Replace("   "," ").Replace("  "," ").Trim();

			string[] row = line.Split(' ');

//			Debug.Log(row.Length);

			if (readRGB){ if (row.Length<6) {Debug.LogError("No RGB data founded, disabling readRGB"); readRGB = false;}}
			if (readIntensity){ if (row.Length!=4 && row.Length!=7) {Debug.LogError("No Intensity data founded, disabling readIntensity"); readIntensity = false;}}

			// take first point pos
			ph.x = double.Parse(row[0]);
			ph.y = double.Parse(row[1]);
			ph.z = double.Parse(row[2]);

			ph.readSuccess = true;
			
			return ph;
		}
		//PeekHeaderData PeekHeaderLAS(BinaryReader reader)
		//{

			// read all points




//			ph.linesRead = 0; // not applicable? ir use offsetpoint?
//			ph.readSuccess = false;
			//Debug.Break();
			//return null; //ph;
		//}


		PeekHeaderData PeekHeaderPLY(StreamReader reader)
		{
			PeekHeaderData ph = new PeekHeaderData();
			string line = reader.ReadLine();
			ph.linesRead++;
			line = line.Replace("   "," ").Replace("  "," ").Trim();
			// is this ply
			if (line.ToLower()!="ply")
			{
				Debug.LogWarning("Header error #1: not 'ply'");
				ph.readSuccess = false;
				return ph;
			}
			line = reader.ReadLine();
			ph.linesRead++;
			// is this ascii ply
			if (!line.Contains("format ascii"))
			{
				Debug.LogWarning("Header error #2: not 'format ascii'");
				ph.readSuccess = false;
				return ph;
			}

			// read comment line
			line = reader.ReadLine();
			ph.linesRead++;

			// get vertex count
			line = reader.ReadLine();
			ph.linesRead++;
			string[] row = line.Split(' ');
			masterPointCount = long.Parse(row[2]);
			Debug.Log("Reading "+masterPointCount+" points..");
			
			if (masterPointCount < 1) { Debug.LogError("Header error #3: ply vertex count < 1"); ph.readSuccess = false; return ph;}
			
			// check properties
			line = reader.ReadLine();
			ph.linesRead++;
			if (line.ToLower()!="property float x")	{ Debug.LogError("Header error #4a: property x error"); ph.readSuccess = false; return ph;}
			line = reader.ReadLine();
			ph.linesRead++;
			if (line.ToLower()!="property float y")	{ Debug.LogError("Header error #4b: property y error"); ph.readSuccess = false; return ph;}
			line = reader.ReadLine();
			ph.linesRead++;
			if (line.ToLower()!="property float z")	{ Debug.LogWarning("Header error #4c: property z error"); ph.readSuccess = false; return ph;}
			
			// check for normal data
			//if (readNormals)
			//{
			line = reader.ReadLine();
			ph.linesRead++;

			if (line.ToLower()=="property float nx")
			{
				plyHasNormals = true;

				line = reader.ReadLine(); // ny
				line = reader.ReadLine(); // nz
				ph.linesRead++;
				ph.linesRead++;

				
				// rgb
				line = reader.ReadLine();
				ph.linesRead++;
				if (line.ToLower()=="property uchar red")
				{
					// yes, take other lines out also
					line = reader.ReadLine(); // g
					line = reader.ReadLine(); // b
					line = reader.ReadLine(); // a
					ph.linesRead++;
					ph.linesRead++;
					ph.linesRead++;

					
				}else{ // no color vals
					readRGB = false;
				}

				// face elements (not used)
				line = reader.ReadLine();
				ph.linesRead++;

				
			}else{ // no normals
				
				if (line.ToLower()=="property uchar red")
				{
					// yes, take other lines out also
					line = reader.ReadLine(); // g
					line = reader.ReadLine(); // b
					line = reader.ReadLine(); // a
					ph.linesRead++;
					ph.linesRead++;
					ph.linesRead++;

					// face elements (not used)
					line = reader.ReadLine();
					ph.linesRead++;

				}else{ // no color vals or normals either
					readRGB = false;
				}
			}
			
			// property list title (not used)
			line = reader.ReadLine();
			ph.linesRead++;

			// end header
			line = reader.ReadLine();
			ph.linesRead++;

			if (line.ToLower()!="end_header") {
				Debug.LogWarning("Header error #5: 'end_header' not found in correct place");
				ph.readSuccess = false;
				return ph;
			}
			
			// read first line to get data

			line = reader.ReadLine(); // x y z r g b a
			ph.linesRead++;

			
			if (IsNullOrEmptyLine(line)) {ph.readSuccess = false; return ph;}
			row = line.Split(' ');
			if (readRGB){	if (row.Length<5) {Debug.LogError("No RGB data founded, disabling readRGB"); readRGB = false;}}
			ph.x = double.Parse(row[0]);
			ph.y = double.Parse(row[1]);
			ph.z = double.Parse(row[2]);
			ph.readSuccess = true;

			return ph;

		}




		// conversion function
		void Convert2Binary()
		{

			// TEMPORARY: Custom reader for LAS binary
			if (fileFormats[fileFormat]=="LAS")
			{
				LASDataConvert();
				return;
			}

			//var saveFilePath = EditorUtility.SaveFilePanelInProject("Save binary file","Assets/",sourceFile.name+".bin","bin");
			var saveFilePath = EditorUtility.SaveFilePanelInProject("Output binary file",sourceFile.name+".bin","bin","");

			string fileToRead= AssetDatabase.GetAssetPath(sourceFile);
			if (!ValidateSaveAndRead(saveFilePath, fileToRead)) return;

			long lines=0;

			// TODO: put reader to peekheader, then new reader when reading actual data
			// get initial data (so can check if data is ok)
			using (StreamReader txtReader = new StreamReader(File.OpenRead(fileToRead)))
			//StreamReader txtReader=  = new StreamReader(File.OpenRead(fileToRead));
			{
				double x=0,y=0,z=0;
				float r=0,g=0,b=0; //,nx=0,ny=0,nz=0;; // init vals
				string line = null;
				string[] row = null;

				PeekHeaderData headerCheck;
				headerCheck.x=0;headerCheck.y=0;headerCheck.z=0;
				headerCheck.linesRead=0;

				switch (fileFormats[fileFormat])
				{
					case "ASC": // ASC (space at front)
					{
						headerCheck = PeekHeaderASC(txtReader);
						if (!headerCheck.readSuccess) {txtReader.Close(); return;}
						lines = headerCheck.linesRead;
					} break;
					
					case "CGO": // CGO	(counter at first line and uses comma)
					{
						headerCheck = PeekHeaderCGO(txtReader);
						if (!headerCheck.readSuccess) {txtReader.Close(); return;}
						lines = headerCheck.linesRead;
					} break;
					
					
					case "CATIA ASC": // CATIA ASC (with header and Point Format           = 'X %f Y %f Z %f')
					{
						headerCheck = PeekHeaderCATIA_ASC(txtReader);
						if (!headerCheck.readSuccess) {txtReader.Close(); return;}
						lines = headerCheck.linesRead;
					} break;

					
					case "XYZRGB": case "XYZ":	// XYZ RGB(INT)
					{
						headerCheck = PeekHeaderXYZ(txtReader);
						if (!headerCheck.readSuccess) {txtReader.Close(); return;}
						lines = headerCheck.linesRead;
					} break;

				
					case "PTS": // PTS (INT) (RGB)
					{
						headerCheck = PeekHeaderPTS(txtReader);
						if (!headerCheck.readSuccess) {txtReader.Close(); return;}
						lines = headerCheck.linesRead;
					} break;

					case "PLY (ASCII)": // PLY (ASCII)
					{
						headerCheck = PeekHeaderPLY(txtReader);
						if (!headerCheck.readSuccess) {txtReader.Close(); return;}
						//lines = headerCheck.linesRead;
					}
					break;

					default:
						Debug.LogError(appName+"> Unknown fileformat error (1)");
					break;
					
				} // switch format


				if (autoOffsetNearZero)
				{
					manualOffset = new Vector3((float)headerCheck.x,(float)headerCheck.y,(float)headerCheck.z);
				}

				// scaling enabled, scale offset too
				if (useUnitScale) manualOffset*=unitScale;

				// progressbar
				float progress = 0;
				long progressCounter=0;				


				// get total amount of points
				if (fileFormats[fileFormat]=="PLY (ASCII)" || fileFormats[fileFormat]=="PTS")
				{
					lines = masterPointCount;

					// reset back to start of file
					txtReader.DiscardBufferedData();
					txtReader.BaseStream.Seek(0, SeekOrigin.Begin); 
					txtReader.BaseStream.Position = 0;

					// get back to before first actual data line
					for (int i = 0; i < headerCheck.linesRead-1; i++) {
						txtReader.ReadLine();
					}



					
				}else{ // other formats need to be read completely

					// reset back to start of file
					txtReader.DiscardBufferedData();
					txtReader.BaseStream.Seek(0, SeekOrigin.Begin); 
					txtReader.BaseStream.Position = 0;

					// get back to before first actual data line
					for (int i = 0; i < headerCheck.linesRead; i++) {
						txtReader.ReadLine();
					}
					lines=0;
					
					while (!txtReader.EndOfStream)
					{
						line = txtReader.ReadLine().Replace("  "," ").Replace("  "," ").Trim();

						if (progressCounter>100000)
						{
							EditorUtility.DisplayProgressBar(appName,"Counting lines..",lines/50000000.0f);
							progressCounter=0;
						}
						
						progressCounter++;

						if (line.Length>9)
						{
							if (!line.StartsWith("!"))
							{
								if (!line.StartsWith("*"))
								{
									if (line.Split(' ').Length>=3)
									{
										lines++;
									}
								}
							}
						}
					}
					
					EditorUtility.ClearProgressBar();
					
					// reset back to start of data
					txtReader.DiscardBufferedData();
					txtReader.BaseStream.Seek(0, SeekOrigin.Begin); 
					txtReader.BaseStream.Position = 0;

					for (int i = 0; i < headerCheck.linesRead; i++) 
					{
						txtReader.ReadLine();
					}
					
					masterPointCount = lines;
				
				}
				/*
				txtReader.DiscardBufferedData();
				txtReader.BaseStream.Seek(0, SeekOrigin.Begin); 
				txtReader.BaseStream.Position = 0;
*/

				// prepare to start saving binary file

				// write header
				var writer = new BinaryWriter(File.Open(saveFilePath, FileMode.Create));

				writer.Write(binaryVersion);
				writer.Write((System.Int32)masterPointCount);
				writer.Write(readRGB|readIntensity);

//				progress = 0;
				progressCounter=0;				

				int skippedRows=0;
				long rowCount = 0;
				bool haveMoreToRead = true;

				// process all points
				while (haveMoreToRead)
				{

					if (progressCounter>65000)
					{
						EditorUtility.DisplayProgressBar(appName,"Converting point cloud to binary file",rowCount/(float)lines);
						progressCounter=0;
					}

					progressCounter++;
					//progress++;

					line = txtReader.ReadLine();
//					if (rowCount<10) Debug.Log (line);

					if (!line.StartsWith("!") || !line.StartsWith("*")) // Catia asc, cgo
					{
						if (line!=null && line.Length>9)
						{
							// trim duplicate spaces
							line = line.Replace("   "," ").Replace("  "," ").Trim();
							row = line.Split(' ');

//							if (rowCount<10) Debug.Log(line);

							if (row.Length>2) // catia asc and others
							{

								switch (fileFormats[fileFormat])
								{
									case "ASC": // ASC
										x = double.Parse(row[0]);
										y = double.Parse(row[1]);
										z = double.Parse(row[2]);
									break;
								
									case "CGO": // CGO	(counter at first line and uses comma)
										
										x = double.Parse(row[0].Replace(",","."));
										y = double.Parse(row[1].Replace(",","."));
										z = double.Parse(row[2].Replace(",","."));
									break;
								

								case "CATIA ASC": // CATIA ASC (with header and Point Format           = 'X %f Y %f Z %f')
										x = double.Parse(row[1]);
										y = double.Parse(row[3]);
										z = double.Parse(row[5]);
									break;

								
								case "XYZRGB": case "XYZ":	// XYZ RGB(INT)
									x = double.Parse(row[0]);
									y = double.Parse(row[1]);
									z = double.Parse(row[2]);
									if (readRGB)
									{
										r = float.Parse(row[3])/255f;
										g = float.Parse(row[4])/255f;
										b = float.Parse(row[5])/255f;
									}
									break;

								case "PTS": // PTS (INT) (RGB)
									x = double.Parse(row[0]);
									y = double.Parse(row[1]);
									z = double.Parse(row[2]);

										if (readRGB)
										{
											if (row.Length==7) // XYZIRGB
											{
												r = float.Parse(row[4])/255f;
												g = float.Parse(row[5])/255f;
												b = float.Parse(row[6])/255f;
											}
											else if (row.Length==6) // XYZRGB
											{
												r = float.Parse(row[3])/255f;
												g = float.Parse(row[4])/255f;	
												b = float.Parse(row[5])/255f;
											}
										}
										else if (readIntensity)
										{
											if (row.Length==4 || row.Length==7) // XYZI or XYZIRGB
											{
												// pts intensity -2048 to 2047
												r = Remap(float.Parse(row[3]),-2048,2047,0,1);
												g = r;
												b = r;
											}
										}
									break;

								case "PLY (ASCII)": // PLY (ASCII)

									//if (rowCount<10) Debug.Log(line);

									x = double.Parse(row[0]);
									y = double.Parse(row[1]);
									z = double.Parse(row[2]);

									/*
									// normals
									if (readNormals)
									{
										// Vertex normals are the normalized average of the normals of the faces that contain that vertex
										// TODO: need to fix normal values?
										nx = float.Parse(row[3]);
										ny = float.Parse(row[4]);
										nz = float.Parse(row[5]);

										// and rgb
										if (readRGB)
										{
											r = float.Parse(row[6])/255;
											g = float.Parse(row[7])/255;
											b = float.Parse(row[8])/255;
											//a = float.Parse(row[6])/255; // TODO: alpha not supported yet
										}
										
									}else{ // no normals, but maybe rgb
										*/
										if (readRGB)
										{
											if (plyHasNormals)
											{
												r = float.Parse(row[6])/255f;
												g = float.Parse(row[7])/255f;
												b = float.Parse(row[8])/255f;
											}else{ // no normals
												r = float.Parse(row[3])/255f;
												g = float.Parse(row[4])/255f;
												b = float.Parse(row[5])/255f;
										}
											//a = float.Parse(row[6])/255; // TODO: alpha not supported yet
										}
									/*
									}*/
									break;

									default:
										Debug.LogError(appName + "> Error : Unknown format");
									break;

								} // switch

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

								// if flip
								if (flipYZ)
								{
									writer.Write((float)x);
									writer.Write((float)z);
									writer.Write((float)y);
								}else{
									writer.Write((float)x);
									writer.Write((float)y);
									writer.Write((float)z);
								}
								
								// if have color data
								if (readRGB||readIntensity)
								{
									writer.Write(r);
									writer.Write(g);
									writer.Write(b);
								}
								/*
								// if have normals data, TODO: not possible yet
								if (readNormals)
								{
									writer.Write(nx);
									writer.Write(ny);
									writer.Write(nz);
								}
								*/

								rowCount++;

							}else{ // if row length
								Debug.Log(line);
								skippedRows++;
							}

						}else{ // if linelen
							skippedRows++;
						}
					}

					//if (txtReader.EndOfStream || rowCount>=masterVertexCount) haveMoreToRead = false;

					if (txtReader.EndOfStream || rowCount>=masterPointCount) 
					{

						if (skippedRows>0) Debug.LogWarning("Parser skipped "+skippedRows+" rows (wrong length or bad data)");
						//Debug.Log(masterVertexCount);

						if (rowCount<masterPointCount) // error, file ended too early, not enough points
						{
							Debug.LogWarning("File does not contain enough points, fixing point count to "+rowCount +" (expected : "+ masterPointCount +")");
							
							// fix header point count
							writer.BaseStream.Seek(0, SeekOrigin.Begin);
							writer.Write(binaryVersion);
							writer.Write((System.Int32)rowCount);
							
						}
						
						haveMoreToRead = false;
						
					}

//					if (rowCount>masterVertexCount) haveMoreToRead = false;

				} // while loop reading file

				writer.Close();

				Debug.Log(appName+"> Binary file saved: "+saveFilePath + " ("+masterPointCount+" points)");
				EditorUtility.ClearProgressBar();
			} // using reader
		} // convert2binary

		void LASDataConvert()
		{
			var saveFilePath = EditorUtility.SaveFilePanel("Save binary file","Assets/",sourceFile.name+".bin","bin");
			string fileToRead= AssetDatabase.GetAssetPath(sourceFile);
			if (!ValidateSaveAndRead(saveFilePath, fileToRead)) return;
			
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
				
				/*
				Debug.Log ("startOfFirstExtentedVariableLengthRecord:"+startOfFirstExtentedVariableLengthRecord); // FIXME
				Debug.Log ("numberOfExtentedVariableLengthRecords:"+numberOfExtentedVariableLengthRecords);
				Debug.Log ("numberOfPointRecords:"+numberOfPointRecords);
				Debug.Log ("numberOfPointsByReturn:"+numberOfPointsByReturn[0]); // *** // FIXME
				*/
				
			}
			
			
			/*
			Debug.Log ("fileSignature:"+fileSignature);
			Debug.Log ("fileSourceID:"+fileSourceID);
			Debug.Log ("globalEncoding:"+globalEncoding);
			Debug.Log ("ProjectID1:"+projectID1);
			Debug.Log ("ProjectID2:"+projectID2);
			Debug.Log ("ProjectID3:"+projectID3);
			Debug.Log ("ProjectID4:"+projectID4);
			
			Debug.Log ("versionMajor:"+versionMajor);
			Debug.Log ("versionMinor:"+versionMinor);
			Debug.Log ("systemIdentifier:"+systemIdentifier);
			Debug.Log ("generatingSoftware:"+generatingSoftware);
			Debug.Log ("fileCreationDayOfYear:"+fileCreationDayOfYear);
			Debug.Log ("fileCreationYear:"+fileCreationYear);
			Debug.Log ("headerSize:"+headerSize);
			Debug.Log ("offsetToPointData:"+offsetToPointData);
			Debug.Log ("numberOfVariableLengthRecords:"+numberOfVariableLengthRecords);
			Debug.Log ("pointDataRecordFormat:"+pointDataRecordFormat);
			Debug.Log ("PointDataRecordLength:"+PointDataRecordLength);
			Debug.Log ("legacyNumberOfPointRecords:"+legacyNumberOfPointRecords);
			Debug.Log ("legacyNumberOfPointsByReturn:"+legacyNumberOfPointsByReturn[0]); // ***
			Debug.Log ("xScaleFactor:"+xScaleFactor);
			Debug.Log ("yScaleFactor:"+yScaleFactor);
			Debug.Log ("zScaleFactor:"+zScaleFactor);
			Debug.Log ("xOffset:"+xOffset);
			Debug.Log ("yOffset:"+yOffset);
			Debug.Log ("zOffset:"+zOffset);
			Debug.Log ("maxX:"+maxX);
			Debug.Log ("minX:"+minX);
			Debug.Log ("MaxY:"+MaxY);
			Debug.Log ("minY:"+minY);
			Debug.Log ("maxZ:"+maxZ);
			Debug.Log ("minZ:"+minZ);
			*/
			
			
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
				Debug.Log("vlrDescription:"+vlrDescription);
				*/
				
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

			// saving, write header
			var writer = new BinaryWriter(File.Open(saveFilePath, FileMode.Create));
			writer.Write(binaryVersion);
			writer.Write((System.Int32)masterPointCount);
			writer.Write(readRGB);
			
			long rowCount = 0;
			bool haveMoreToRead = true;
			bool firstPointRead=false;
			
			// process all points
			while (haveMoreToRead)
			{
				if (progressCounter>65000)
				{
					EditorUtility.DisplayProgressBar(appName,"Converting point cloud to binary file",progress/(float)masterPointCount);
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
				
				writer.Write((float)x);
				writer.Write((float)y);
				writer.Write((float)z);
				
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
					
					writer.Write(r);
					writer.Write(g);
					writer.Write(b);
				}
				
				rowCount++;
				
				if (reader.BaseStream.Position >= reader.BaseStream.Length || rowCount>=masterPointCount) 
				{
					
					if (rowCount<masterPointCount) // error, file ended too early, not enough points
					{
						Debug.LogWarning("LAS file does not contain enough points, fixing point count to "+rowCount);
						
						// fix header point count
						writer.BaseStream.Seek(0, SeekOrigin.Begin);
						writer.Write(binaryVersion);
						writer.Write((System.Int32)rowCount);
						
					}
					
					haveMoreToRead = false;
					
				}
				
			} // while loop reading file
			
			writer.Close();
			
			Debug.Log(appName+"> Binary file saved: "+saveFilePath + " ("+masterPointCount+" points)");
			EditorUtility.ClearProgressBar();
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

		string GetSelectedFileInfo()
		{
			string tempFilePath = AssetDatabase.GetAssetPath(sourceFile);
			string tempFileName = Path.GetFileName(tempFilePath);
			return tempFileName+" ("+(new FileInfo( tempFilePath).Length/1000000) +"MB)";
		}



	} // class
} // namespace

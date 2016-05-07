// Reads text file and prints out few first lines (for debug purposes, so you can check what the file contains)

using UnityEditor;
using UnityEngine;
using System.Collections;
using System.IO;

namespace unitycodercom_ShowHeaderDataHelper
{
		
	public class ShowHeaderDataHelper : EditorWindow
	{
		private static string appName = "ShowHeaderDataHelper";

		private Object sourceFile;
		private int linesToRead=25;

		// create menu item and window
		[MenuItem ("Window/PointCloudTools/ShowHeaderDataHelper",false,201)]
	    static void Init () 
		{
			ShowHeaderDataHelper window = (ShowHeaderDataHelper)EditorWindow.GetWindow (typeof (ShowHeaderDataHelper));
			window.titleContent = new GUIContent(appName);
			window.minSize = new Vector2(340,180);
			window.maxSize = new Vector2(340,184);
	    }

		// main loop
		void OnGUI () 
		{
			GUILayout.Label ("Point Cloud source file", EditorStyles.boldLabel);
			sourceFile = EditorGUILayout.ObjectField(sourceFile, typeof(Object), true);
			EditorGUILayout.Space();

			GUI.enabled = sourceFile==null?false:true;
			if(GUILayout.Button (new GUIContent ("Read header data", "Prints out header data (ascii only)"), GUILayout.Height(40))) 
			{
			 	PrintHeaderData();
			}
			GUI.enabled = true;
		} //ongui

		void PrintHeaderData()
		{
			string fileToRead= AssetDatabase.GetAssetPath(sourceFile);
			if (!File.Exists(fileToRead)) {Debug.LogError("File not founded:"+fileToRead); return;}

			using (StreamReader reader = new StreamReader(File.OpenRead(fileToRead)))
			{
				string output="";
				for (int i=0;i<linesToRead;i++)
				{
					var line = reader.ReadLine();
					output+=line+"\n"; // adds new line
				}
				Debug.Log(output);
			}
		}


	} // class
} // namespace

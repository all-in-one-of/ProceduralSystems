/*
* Copyright (c) <2018> Side Effects Software Inc.
* All rights reserved.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*
* 1. Redistributions of source code must retain the above copyright notice,
*    this list of conditions and the following disclaimer.
*
* 2. The name of Side Effects Software may not be used to endorse or
*    promote products derived from this software without specific prior
*    written permission.
*
* THIS SOFTWARE IS PROVIDED BY SIDE EFFECTS SOFTWARE "AS IS" AND ANY EXPRESS
* OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
* OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.  IN
* NO EVENT SHALL SIDE EFFECTS SOFTWARE BE LIABLE FOR ANY DIRECT, INDIRECT,
* INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
* LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA,
* OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
* LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
* NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE,
* EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

#if (UNITY_EDITOR_WIN || UNITY_EDITOR_OSX || UNITY_STANDALONE_LINUX)
#define HOUDINIENGINEUNITY_ENABLED
#endif

using System.Text;
using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif


namespace HoudiniEngineUnity
{
	/////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	// Typedefs (copy these from HEU_Common.cs)
	using HAPI_NodeId = System.Int32;
	using HAPI_PartId = System.Int32;
	using HAPI_AssetLibraryId = System.Int32;
	using HAPI_StringHandle = System.Int32;
	using HAPI_ErrorCodeBits = System.Int32;


	/// <summary>
	/// General utlitity functions.
	/// </summary>
	public static class HEU_HAPIUtility
	{

		/// <summary>
		/// Return Houdini Engine installation and session information.
		/// Tries to use existing or creates new session to find information.
		/// </summary>
		/// <returns>String containing installation and session information.</returns>
		public static string GetHoudiniEngineInstallationInfo()
		{
#if HOUDINIENGINEUNITY_ENABLED
			StringBuilder sb = new StringBuilder();

			sb.AppendFormat("Required Houdini Version: {0}.{1}.{2}\nRequired Houdini Engine Version: {3}.{4}.{5}\n\n",
									HEU_HoudiniVersion.HOUDINI_MAJOR,
									HEU_HoudiniVersion.HOUDINI_MINOR,
									HEU_HoudiniVersion.HOUDINI_BUILD,
									HEU_HoudiniVersion.HOUDINI_ENGINE_MAJOR,
									HEU_HoudiniVersion.HOUDINI_ENGINE_MINOR,
									HEU_HoudiniVersion.HOUDINI_ENGINE_API);

			// Check if existing session is valid, or create a new session. Then query installation information.
			HEU_SessionBase session = HEU_SessionManager.GetDefaultSession();
			if (session != null && session.IsSessionValid())
			{
				int hMajor = session.GetEnvInt(HAPI_EnvIntType.HAPI_ENVINT_VERSION_HOUDINI_MAJOR);
				int hMinor = session.GetEnvInt(HAPI_EnvIntType.HAPI_ENVINT_VERSION_HOUDINI_MINOR);
				int hBuild = session.GetEnvInt(HAPI_EnvIntType.HAPI_ENVINT_VERSION_HOUDINI_BUILD);

				int heuPatch = session.GetEnvInt(HAPI_EnvIntType.HAPI_ENVINT_VERSION_HOUDINI_PATCH);

				int heuMajor = session.GetEnvInt(HAPI_EnvIntType.HAPI_ENVINT_VERSION_HOUDINI_ENGINE_MAJOR);
				int heuMinor = session.GetEnvInt(HAPI_EnvIntType.HAPI_ENVINT_VERSION_HOUDINI_ENGINE_MINOR);
				int heuAPI = session.GetEnvInt(HAPI_EnvIntType.HAPI_ENVINT_VERSION_HOUDINI_ENGINE_API);

				sb.AppendFormat("Installed Houdini Version: {0}.{1}.{2}{3}\n", hMajor, hMinor, hBuild, (heuPatch > 0) ? "." + heuPatch.ToString() : "");
				sb.AppendFormat("Installed Houdini Engine Version: {0}.{1}.{2}\n\n", heuMajor, heuMinor, heuAPI);

				sb.AppendFormat("Houdini Binaries Path: {0}\n", HEU_Platform.GetHoudiniEnginePath() + HEU_HoudiniVersion.HAPI_BIN_PATH);
				sb.AppendFormat("Unity Plugin Version: {0}\n\n", HEU_HoudiniVersion.UNITY_PLUGIN_VERSION);

				HEU_SessionData sessionData = session.GetSessionData();
				if (sessionData != null)
				{
					sb.AppendFormat("Session ID: {0}\n", sessionData.SessionID);
					sb.AppendFormat("Session Type: {0}\n", sessionData.SessionType);
					sb.AppendFormat("Process ID: {0}\n", sessionData.ProcessID);

					if (sessionData.SessionType == HAPI_SessionType.HAPI_SESSION_THRIFT)
					{
						sb.AppendFormat("Pipe name: {0}\n", sessionData.PipeName);
					}

					sb.AppendLine();
				}

				sb.Append("License Type Acquired: ");
				HAPI_License license = HEU_SessionManager.GetCurrentLicense(false);
				switch (license)
				{
					case HAPI_License.HAPI_LICENSE_NONE: sb.Append("None\n"); break;
					case HAPI_License.HAPI_LICENSE_HOUDINI_ENGINE: sb.Append("Houdini Engine\n"); break;
					case HAPI_License.HAPI_LICENSE_HOUDINI: sb.Append("Houdini (Escape)\n"); break;
					case HAPI_License.HAPI_LICENSE_HOUDINI_FX: sb.Append("Houdini FX\n"); break;
					case HAPI_License.HAPI_LICENSE_HOUDINI_ENGINE_INDIE: sb.Append("Houdini Engine Indie"); break;
					case HAPI_License.HAPI_LICENSE_HOUDINI_INDIE: sb.Append("Houdini Indie\n"); break;
					default: sb.Append("Unknown\n"); break;
				}
			}
			else // Unable to establish a session
			{
				sb.AppendLine("Unable to detect Houdini Engine installation.");
				sb.AppendLine("License Type Acquired: Unknown\n");
				if(session != null)
				{
					sb.AppendLine("Failure possibly due to: " + session.GetLastSessionError());
				}
			}

			sb.AppendLine();
			sb.Append("PATH: \n" + GetEnvironmentPath());

			Debug.Log(sb.ToString());

			return sb.ToString();
#else
			return "";
#endif
		}


		/// <summary>
		/// Return the PATH environment value for current process.
		/// </summary>
		/// <returns>The PATH environment string.</returns>
		public static string GetEnvironmentPath()
		{
			string pathStr = System.Environment.GetEnvironmentVariable("PATH", System.EnvironmentVariableTarget.Process);

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
			pathStr = pathStr.Replace(";", "\n");
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
			pathStr = pathStr.Replace(":", "\n");
#endif
			return pathStr;
		}

		/// <summary>
		/// Returns true if given file is a Houdini Digital Asset file.
		/// </summary>
		/// <param name="filePath">File name to check</param>
		/// <returns>True if file is a Houdini Digital Asset</returns>
		public static bool IsHoudiniAssetFile(string filePath)
		{
			return (filePath.EndsWith(".otl", System.StringComparison.OrdinalIgnoreCase)
					|| filePath.EndsWith(".otllc", System.StringComparison.OrdinalIgnoreCase)
					|| filePath.EndsWith(".otlnc", System.StringComparison.OrdinalIgnoreCase)
					|| filePath.EndsWith(".hda", System.StringComparison.OrdinalIgnoreCase)
					|| filePath.EndsWith(".hdalc", System.StringComparison.OrdinalIgnoreCase)
					|| filePath.EndsWith(".hdanc", System.StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>
		/// Abstraction around Unity warning logger so provide some control for logging.
		/// </summary>
		/// <param name="message">String message to log</param>
		public static void Log(string message)
		{
			Debug.Log(message);
		}

		/// <summary>
		/// Abstraction around Unity warning logger so provide some control for logging.
		/// </summary>
		/// <param name="message">String message to log</param>
		public static void LogWarning(string message)
		{
			Debug.LogWarning(message);
		}

		/// <summary>
		/// Abstraction around Unity error logger so provide some control for logging.
		/// </summary>
		/// <param name="message">String message to log</param>
		public static void LogError(string message)
		{
			Debug.LogError(message);
		}

		/// <summary>
		/// For the given object, returns its file path if it exists.
		/// </summary>
		/// <param name="inObject">Object to get the path for.</param>
		/// <returns>Valid path or null if none found.</returns>
		public static string LocateValidFilePath(UnityEngine.Object inObject)
		{
			return inObject != null ? HEU_AssetDatabase.GetAssetPath(inObject) : null;
		}

		/// <summary>
		/// For the file path, returns a valid location if exists.
		/// If inFilePath is not valid, it uses the file name to search the asset database to 
		/// find the actual valid location (in case it was moved).
		/// </summary>
		/// <param name="gameObjectName">Name of the asset for which to find the path.</param>
		/// <param name="inFilePath">Current path of the asset to validate. Could be null or invalid.</param>
		/// <returns>Valid path or null if none found.</returns>
		public static string LocateValidFilePath(string assetName, string inFilePath)
		{
#if UNITY_EDITOR
			// Find asset if its not at given path
			if (!HEU_Platform.DoesFileExist(inFilePath))
			{
				string fileName = HEU_Platform.GetFileNameWithoutExtension(inFilePath);
				string[] guids = AssetDatabase.FindAssets(fileName);
				if (guids.Length > 0)
				{
					foreach (string guid in guids)
					{
						string newPath = AssetDatabase.GUIDToAssetPath(guid);
						if (newPath != null && newPath.Length > 0)
						{
							Debug.Log(string.Format("Note: changing asset path for {0} to {1}.", assetName, newPath));
							return newPath;
						}
					}
				}

				// No valid path
				throw new HEU_HoudiniEngineError(string.Format("Houdini Asset file has moved from last location: {0}", inFilePath));
			}
#endif
			return inFilePath;
		}

		/// <summary>
		/// Load and instantiate an HDA asset in Unity and Houdini, for the asset located at given path.
		/// </summary>
		/// <param name="filePath">Full path to the HDA in Unity project</param>
		/// <param name="initialPosition">Initial location to create the instance in Unity.</param>
		/// <returns>Returns the newly created gameobject for the asset in the scene, or null if creation failed.</returns>
		public static GameObject InstantiateHDA(string filePath, Vector3 initialPosition, HEU_SessionBase session, bool bBuildAsync)
		{
			if (filePath == null || !HEU_Platform.DoesFileExist(filePath))
			{
				return null;
			}

			// This will be the root GameObject for the HDA. Adding HEU_HoudiniAssetRoot
			// allows to use a custom Inspector.
			GameObject rootGO = new GameObject(HEU_Defines.HEU_DEFAULT_ASSET_NAME);
			HEU_HoudiniAssetRoot assetRoot = rootGO.AddComponent<HEU_HoudiniAssetRoot>();

			// Under the root, we'll add the HEU_HoudiniAsset onto another GameObject
			// This will be marked as EditorOnly to strip out for builds
			GameObject hdaGEO = new GameObject(HEU_PluginSettings.HDAData_Name);
			hdaGEO.transform.parent = rootGO.transform;

			// This holds all Houdini Engine data
			HEU_HoudiniAsset asset = hdaGEO.AddComponent<HEU_HoudiniAsset>();
			// Marking as EditorOnly to be excluded from builds
			if(HEU_GeneralUtility.DoesUnityTagExist(HEU_PluginSettings.EditorOnly_Tag))
			{
				hdaGEO.tag = HEU_PluginSettings.EditorOnly_Tag;
			}

			// Bind the root to the asset
			assetRoot._houdiniAsset = asset;

			// Populate asset with what we know
			asset.SetupAsset(HEU_HoudiniAsset.HEU_AssetType.TYPE_HDA, filePath, rootGO, session);

			// Build it in Houdini Engine
			asset.RequestReload(bBuildAsync);

			// Apply Unity transform and possibly upload to Houdini Engine
			rootGO.transform.position = initialPosition;
			// TODO: push transform to HAPI

			Debug.LogFormat("{0}: Created new HDA asset from {1} of type {2}.", HEU_Defines.HEU_NAME, filePath, asset.AssetType);

			return rootGO;
		}

		public static bool LoadHDAFile(HEU_SessionBase session, string assetPath, out HAPI_NodeId assetLibraryID, out string[] assetNames)
		{
			assetLibraryID = HEU_Defines.HEU_INVALID_NODE_ID;
			assetNames = new string[0];

			// Load the file
			string validAssetPath = HEU_HAPIUtility.LocateValidFilePath(assetPath, assetPath);
			if (validAssetPath != null)
			{
				assetPath = validAssetPath;

				HAPI_AssetLibraryId libraryID = 0;
				bool bResult = session.LoadAssetLibraryFromFile(assetPath, false, out libraryID);
				if (!bResult)
				{
					return false;
				}

				int assetCount = 0;
				bResult = session.GetAvailableAssetCount(libraryID, out assetCount);
				if (!bResult)
				{
					return false;
				}
				Debug.AssertFormat(assetCount > 0, "Houdini Engine: Invalid Asset Count of {0}", assetCount);

				HAPI_StringHandle[] assetNameLengths = new HAPI_StringHandle[assetCount];
				bResult = session.GetAvailableAssets(libraryID, ref assetNameLengths, assetCount);
				if (!bResult)
				{
					return false;
				}
				// Sanity check that our array hasn't changed size
				Debug.Assert(assetNameLengths.Length == assetCount, "Houdini Engine: Invalid Asset Names");

				assetNames = new string[assetCount];
				for (int i = 0; i < assetCount; ++i)
				{
					assetNames[i] = HEU_SessionManager.GetString(assetNameLengths[i]);
				}

				return true;
			}

			return false;
		}


		public static bool CreateAndCookAssetNode(HEU_SessionBase session, string assetName, bool bCookTemplatedGeos, out HAPI_NodeId newAssetID)
		{
			newAssetID = HEU_Defines.HEU_INVALID_NODE_ID;

			// Create top level node. Note that CreateNode will cook the node if HAPI was initialized with threaded cook setting on.
			bool bResult = session.CreateNode(-1, assetName, "", false, out newAssetID);
			if (!bResult)
			{
				return false;
			}

			// Make sure cooking is successfull before proceeding. Any licensing or file data issues will be caught here.
			if (!ProcessHoudiniCookStatus(session, assetName))
			{
				return false;
			}

			// In case the cooking wasn't done previously, force it now.
			bResult = CookNodeInHoudini(session, newAssetID, bCookTemplatedGeos, assetName);
			if (!bResult)
			{
				// When cook failed, deleted the node created earlier
				session.DeleteNode(newAssetID);
				newAssetID = HEU_Defines.HEU_INVALID_NODE_ID;
				return false;
			}

			// Get the asset ID
			HAPI_AssetInfo assetInfo = new HAPI_AssetInfo();
			bResult = session.GetAssetInfo(newAssetID, ref assetInfo);
			if (bResult)
			{
				// Check for any errors
				HAPI_ErrorCodeBits errors = session.CheckForSpecificErrors(newAssetID, (HAPI_ErrorCodeBits)HAPI_ErrorCode.HAPI_ERRORCODE_ASSET_DEF_NOT_FOUND);
				if (errors > 0)
				{
					// TODO: revisit for UI improvement
					HEU_EditorUtility.DisplayDialog("Asset Missing Sub-asset Definitions",
						"There are undefined nodes. This is due to not being able to find specific " +
						"asset definitions.", "Ok");
					return false;
				}
			}

			return true;
		}

		/// <summary>
		/// Cooks node and returns true if successfull.
		/// </summary>
		/// <param name="nodeID">The node to cook</param>
		/// <param name="bCookTemplatedGeos">Whether to cook templated geos</param>
		/// <returns>True if successfully cooked node</returns>
		public static bool CookNodeInHoudini(HEU_SessionBase session, HAPI_NodeId nodeID, bool bCookTemplatedGeos, string assetName)
		{
			bool bResult = session.CookNode(nodeID, bCookTemplatedGeos);
			if (bResult)
			{
				return HEU_HAPIUtility.ProcessHoudiniCookStatus(session, assetName);
			}

			return bResult;
		}

		/// <summary>
		/// Waits until cooking has finished.
		/// </summary>
		/// <returns>True if cooking was successful</returns>
		public static bool ProcessHoudiniCookStatus(HEU_SessionBase session, string assetName)
		{
			bool bResult = true;
			HAPI_State statusCode = HAPI_State.HAPI_STATE_STARTING_LOAD;

			// Busy wait until cooking has finished
			while (bResult && statusCode > HAPI_State.HAPI_STATE_MAX_READY_STATE)
			{
				bResult = session.GetStatus(HAPI_StatusType.HAPI_STATUS_COOK_STATE, out statusCode);

				// TODO: notify user using HAPI_GetStatusString, and HAPI_GetCookingCurrentCount / HAPI_GetCookingTotalCount for % completion.
			}

			// Check cook results for any errors
			if (statusCode == HAPI_State.HAPI_STATE_READY_WITH_COOK_ERRORS)
			{
				// We should be able to continue even with these errors, but at least notify user.
				string statusString = session.GetStatusString(HAPI_StatusType.HAPI_STATUS_COOK_RESULT, HAPI_StatusVerbosity.HAPI_STATUSVERBOSITY_WARNINGS);
				Debug.LogWarning(string.Format("Houdini Engine: Cooking finished with some errors for asset: {0}\n{1}", assetName, statusString));
			}
			else if (statusCode == HAPI_State.HAPI_STATE_READY_WITH_FATAL_ERRORS)
			{
				string statusString = session.GetStatusString(HAPI_StatusType.HAPI_STATUS_COOK_RESULT, HAPI_StatusVerbosity.HAPI_STATUSVERBOSITY_ERRORS);
				Debug.LogError(string.Format("Houdini Engine: Cooking failed for asset: {0}\n{1}", assetName, statusString));
				return false;
			}
			else
			{
				//Debug.LogFormat("Houdini Engine: Cooking result {0} for asset: {1}", (HAPI_State)statusCode, AssetName);
			}
			return true;
		}

		public static GameObject CreateNewAsset(HEU_HoudiniAsset.HEU_AssetType assetType, string rootName = "HoudiniAsset", Transform parentTransform = null, HEU_SessionBase session = null, bool bBuildAsync = true)
		{
			if (session == null)
			{
				session = HEU_SessionManager.GetOrCreateDefaultSession();
			}
			if (!session.IsSessionValid())
			{
				Debug.LogWarning("Invalid Houdini Engine session!");
				return null;
			}

			// This will be the root GameObject for the HDA. Adding HEU_HoudiniAssetRoot
			// allows to use a custom Inspector.
			GameObject rootGO = new GameObject();
			HEU_HoudiniAssetRoot assetRoot = rootGO.AddComponent<HEU_HoudiniAssetRoot>();

			// Set the game object's name to the asset's name
			rootGO.name = string.Format("{0}{1}", rootName, rootGO.GetInstanceID());

			// Under the root, we'll add the HEU_HoudiniAsset onto another GameObject
			// This will be marked as EditorOnly to strip out for builds
			GameObject hdaGEO = new GameObject(HEU_PluginSettings.HDAData_Name);
			hdaGEO.transform.parent = rootGO.transform;

			// This holds all Houdini Engine data
			HEU_HoudiniAsset asset = hdaGEO.AddComponent<HEU_HoudiniAsset>();
			// Marking as EditorOnly to be excluded from builds
			if (HEU_GeneralUtility.DoesUnityTagExist(HEU_PluginSettings.EditorOnly_Tag))
			{
				hdaGEO.tag = HEU_PluginSettings.EditorOnly_Tag;
			}

			// Bind the root to the asset
			assetRoot._houdiniAsset = asset;

			// Populate asset with what we know
			asset.SetupAsset(assetType, null, rootGO, session);

			// Build it in Houdini Engine
			asset.RequestReload(bBuildAsync);

			if (parentTransform != null)
			{
				rootGO.transform.parent = parentTransform;
				rootGO.transform.localPosition = Vector3.zero;
			}
			else
			{
				rootGO.transform.position = Vector3.zero;
			}

			return rootGO;
		}

		/// <summary>
		/// Creates a new Curve asset in scene, as well as in a Houdini session.
		/// </summary>
		/// <returns>A valid curve asset gameobject or null if failed.</returns>
		public static GameObject CreateNewCurveAsset(Transform parentTransform = null, HEU_SessionBase session = null, bool bBuildAsync = true)
		{
			return CreateNewAsset(HEU_HoudiniAsset.HEU_AssetType.TYPE_CURVE, "HoudiniCurve", parentTransform, session, bBuildAsync);
		}

		/// <summary>
		/// Creates a new input asset in scene, as well as in a Houdini session.
		/// </summary>
		/// <returns>A valid input asset gameobject or null if failed.</returns>
		public static GameObject CreateNewInputAsset(Transform parentTransform = null, HEU_SessionBase session = null, bool bBuildAsync = true)
		{
			return CreateNewAsset(HEU_HoudiniAsset.HEU_AssetType.TYPE_INPUT, "HoudiniInput", parentTransform, session, bBuildAsync);
		}

		/// <summary>
		/// Destroy children of the given transform. Does not destroy inTransform itself.
		/// </summary>
		/// <param name="inTransform">Tranform whose children are to be destroyed</param>
		public static void DestroyChildren(Transform inTransform)
		{
			List<GameObject> children = new List<GameObject>();

			foreach(Transform child in inTransform)
			{
				children.Add(child.gameObject);
			}
			
			foreach(GameObject child in children)
			{
				DestroyGameObject(child);
			}
		}

		/// <summary>
		/// Destroy the given game object, including its internal mesh and any shared materials.
		/// </summary>
		/// <param name="gameObect">Game object to destroy</param>
		public static void DestroyGameObject(GameObject gameObect, bool bRegisterUndo = false)	// TODO: remove default bRegisterUndo arg
		{
			DestroyGameObjectMeshData(gameObect, bRegisterUndo: bRegisterUndo);

			HEU_GeneralUtility.DestroyImmediate(gameObect, bAllowDestroyingAssets: true, bRegisterUndo: bRegisterUndo);
		}

		/// <summary>
		/// Destroy mesh data (filter, collider, renderer).
		/// Destroys non-persistance materials.
		/// </summary>
		/// <param name="gameObect"></param>
		/// <param name="bRegisterUndo"></param>
		public static void DestroyGameObjectMeshData(GameObject gameObect, bool bRegisterUndo)
		{
			// Destroy the internal mesh (but not if its an asset).
			MeshFilter meshFilter = gameObect.GetComponent<MeshFilter>();
			if (meshFilter != null)
			{
				Mesh mesh = meshFilter.sharedMesh;
				if (mesh != null)
				{
					HEU_GeneralUtility.DestroyImmediate(mesh, false, bRegisterUndo: bRegisterUndo);
					meshFilter.sharedMesh = null;
				}
			}

			// Destroy the internal mesh collider (but not if its an asset).
			MeshCollider meshCollider = gameObect.GetComponent<MeshCollider>();
			if (meshCollider != null)
			{
				Mesh mesh = meshCollider.sharedMesh;
				if (mesh != null)
				{
					HEU_GeneralUtility.DestroyImmediate(mesh, false, bRegisterUndo: bRegisterUndo);
					meshCollider.sharedMesh = null;
				}
			}

			// Clean up any shared materials (but not if its an asset).
			Renderer renderer = gameObect.GetComponent<Renderer>();
			if (renderer != null)
			{
				Material[] materials = renderer.sharedMaterials;
				int numMaterials = materials.Length;
				for (int i = 0; i < numMaterials; ++i)
				{
					HEU_MaterialFactory.DestroyNonAssetMaterial(materials[i], bRegisterUndo: bRegisterUndo);
					materials[i] = null;
				}
				renderer.sharedMaterials = materials;
			}
		}

		/// <summary>
		/// Destroy child GameObjects under the given gameObject with component T.
		/// </summary>
		/// <typeparam name="T">The component to look for on the child GameObjects</typeparam>
		/// <param name="gameObject">The GameObject's children to search through</param>
		public static void DestroyChildrenWithComponent<T>(GameObject gameObject) where T : Component
		{
			Transform trans = gameObject.transform;
			List<GameObject> children = new List<GameObject>();
			foreach(Transform t in trans)
			{
				children.Add(t.gameObject);
			}

			foreach(GameObject c in children)
			{
				if(c.GetComponent<T>() != null)
				{
					HEU_HAPIUtility.DestroyGameObject(c);
				}
			}
		}

		/// <summary>
		/// Returns true if given asset is valid in given Houdini session.
		/// </summary>
		/// <param name="session">Session to check</param>
		/// <param name="assetID">ID of the asset to check</param>
		/// <returns>True if asset is valid in given session</returns>
		public static bool IsAssetValidInHoudini(HEU_SessionBase session, HAPI_NodeId assetID)
		{
			// Without a valid asset ID, we can't really check in Houdini session
			if (assetID != HEU_Defines.HEU_INVALID_NODE_ID)
			{
				// Use _assetID with uniqueHoudiniNodeId to see if our asset matches up in Houdini
				HAPI_NodeInfo nodeInfo = new HAPI_NodeInfo();
				if (session.GetNodeInfo(assetID, ref nodeInfo, false))
				{
					return session.IsNodeValid(assetID, nodeInfo.uniqueHoudiniNodeId);
				}
			}
			return false;
		}

		// TRANSFORMS -------------------------------------------------------------------------------------------------

		/// <summary>
		/// Apply Houdini Engine world transform to Unity's transform object.
		/// This assumes given HAPI transform is in world space.
		/// </summary>
		/// <param name="hapiTransform">Houdini Engine transform to get data from</param>
		/// <param name="unityTransform">The Unity transform to apply data to</param>
		public static void ApplyWorldTransfromFromHoudiniToUnity(HAPI_Transform hapiTransform, Transform unityTransform)
		{
			// Houdini uses right-handed coordinate system, while Unity uses left-handed.
			// Note: we always use global transform space when communicating with Houdini

			// Invert the X for position
			unityTransform.position = new Vector3(-hapiTransform.position[0], hapiTransform.position[1], hapiTransform.position[2]);

			// Invert Y and Z for rotation
			Quaternion quaternion = new Quaternion(hapiTransform.rotationQuaternion[0], hapiTransform.rotationQuaternion[1], hapiTransform.rotationQuaternion[2], hapiTransform.rotationQuaternion[3]);
			Vector3 euler = quaternion.eulerAngles;
			euler.y = -euler.y;
			euler.z = -euler.z;
			unityTransform.rotation = Quaternion.Euler(euler);

			// No inversion required for scale
			// We can't directly set global scale in Unity, but the proper workaround is to unparent, set scale, then reparent
			Vector3 scale = new Vector3(hapiTransform.scale[0], hapiTransform.scale[1], hapiTransform.scale[2]);
			if(unityTransform.parent != null)
			{
				Transform parent = unityTransform.parent;
				unityTransform.parent = null;
				unityTransform.localScale = scale;
				unityTransform.parent = parent;
			}
			else
			{
				unityTransform.localScale = scale;
			}
		}

		/// <summary>
		/// Apply Houdini Engine local transform to Unity's transform object.
		/// This assumes given HAPI transform is in local space.
		/// </summary>
		/// <param name="hapiTransform">Houdini Engine transform to get data from</param>
		/// <param name="unityTransform">The Unity transform to apply data to</param>
		public static void ApplyLocalTransfromFromHoudiniToUnity(ref HAPI_Transform hapiTransform, Transform unityTransform)
		{
			// Houdini uses right-handed coordinate system, while Unity uses left-handed.
			// Note: we always use global transform space when communicating with Houdini

			// Invert the X for position
			unityTransform.localPosition = new Vector3(-hapiTransform.position[0], hapiTransform.position[1], hapiTransform.position[2]);

			// Invert Y and Z for rotation
			Quaternion quaternion = new Quaternion(hapiTransform.rotationQuaternion[0], hapiTransform.rotationQuaternion[1], hapiTransform.rotationQuaternion[2], hapiTransform.rotationQuaternion[3]);
			Vector3 euler = quaternion.eulerAngles;
			euler.y = -euler.y;
			euler.z = -euler.z;
			unityTransform.localRotation = Quaternion.Euler(euler);

			// No inversion required for scale
			// We can't directly set global scale in Unity, but the proper workaround is to unparent, set scale, then reparent
			Vector3 scale = new Vector3(hapiTransform.scale[0], hapiTransform.scale[1], hapiTransform.scale[2]);
			unityTransform.localScale = scale;
		}

		/// <summary>
		/// Apply matrix to transform.
		/// </summary>
		/// <param name="matrix"></param>
		/// <param name="transform"></param>
		public static void ApplyMatrixToLocalTransform(ref Matrix4x4 matrix, Transform transform)
		{
			transform.localPosition = GetPosition(ref matrix);
			transform.localRotation = GetQuaternion(ref matrix);
			transform.localScale = GetScale(ref matrix);
		}

		/// <summary>
		/// Returns Unity 4x4 matrix corresponding to the given HAPI_Transform.
		/// Converts from Houdini to Unity coordinate system.
		/// </summary>
		/// <param name="hapiTransform">HAPI transform to get values from</param>
		/// <returns>Matrix4x4 in Unity coordinate system</returns>
		public static Matrix4x4 GetMatrixFromHAPITransform(ref HAPI_Transform hapiTransform, bool bConvertToUnity = true)
		{
			float invert = bConvertToUnity ? -1f : 1f;

			// TODO: Refactor this so as to use a common function to get these values
			// Invert the X for position
			Vector3 position = new Vector3(invert * hapiTransform.position[0], hapiTransform.position[1], hapiTransform.position[2]);

			// Invert Y and Z for rotation
			Quaternion quaternion = new Quaternion(hapiTransform.rotationQuaternion[0], hapiTransform.rotationQuaternion[1], hapiTransform.rotationQuaternion[2], hapiTransform.rotationQuaternion[3]);
			Vector3 euler = quaternion.eulerAngles;
			euler.y = invert * euler.y;
			euler.z = invert * euler.z;
			Quaternion rotation = Quaternion.Euler(euler);

			// No inversion required for scale
			// We can't directly set global scale in Unity, but the proper workaround is to unparent, set scale, then reparent
			Vector3 scale = new Vector3(hapiTransform.scale[0], hapiTransform.scale[1], hapiTransform.scale[2]);

			Matrix4x4 matrix = new Matrix4x4();
			matrix.SetTRS(position, rotation, scale);
			return matrix;
		}

		public static Quaternion GetQuaternion(ref Matrix4x4 m)
		{
			// Check to stop warning about "Look rotation viewing vector is zero" from Quaternion.LookRotation().
			if (
				Mathf.Approximately(0.0f, m.GetColumn(2).x) &&
				Mathf.Approximately(0.0f, m.GetColumn(2).y) &&
				Mathf.Approximately(0.0f, m.GetColumn(2).z) &&
				Mathf.Approximately(0.0f, m.GetColumn(2).w) &&
				Mathf.Approximately(0.0f, m.GetColumn(1).x) &&
				Mathf.Approximately(0.0f, m.GetColumn(1).y) &&
				Mathf.Approximately(0.0f, m.GetColumn(1).z) &&
				Mathf.Approximately(0.0f, m.GetColumn(1).w))
			{
				return new Quaternion();
			}
			else
			{
				return Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1));
			}
		}

		public static Vector3 GetPosition(ref Matrix4x4 m)
		{
			return m.GetColumn(3);
		}

		public static void SetMatrixPosition(ref Matrix4x4 m, ref Vector3 position)
		{
			m.SetColumn(3, position);
		}

		public static Vector3 GetScale(ref Matrix4x4 m)
		{
			var x = Mathf.Sqrt(m.m00 * m.m00 + m.m10 * m.m10 + m.m20 * m.m20);
			var y = Mathf.Sqrt(m.m01 * m.m01 + m.m11 * m.m11 + m.m21 * m.m21);
			var z = Mathf.Sqrt(m.m02 * m.m02 + m.m12 * m.m12 + m.m22 * m.m22);

			return new Vector3(x, y, z);
		}

		public static HAPI_TransformEuler GetHAPITransformFromMatrix(ref Matrix4x4 mat)
		{
			Quaternion q = GetQuaternion(ref mat);
			Vector3 r = q.eulerAngles;

			Vector3 p = GetPosition(ref mat);
			Vector3 s = GetScale(ref mat);

			HAPI_TransformEuler transform = new HAPI_TransformEuler(true);

			transform.position[0] = -p[0];
			transform.position[1] = p[1];
			transform.position[2] = p[2];

			transform.rotationEuler[0] = r[0];
			transform.rotationEuler[1] = -r[1];
			transform.rotationEuler[2] = -r[2];

			transform.scale[0] = s[0];
			transform.scale[1] = s[1];
			transform.scale[2] = s[2];

			transform.rotationOrder = HAPI_XYZOrder.HAPI_ZXY;
			transform.rstOrder = HAPI_RSTOrder.HAPI_SRT;

			return transform;
		}

		public static HAPI_TransformEuler GetHAPITransform(ref Vector3 p, ref Vector3 r, ref Vector3 s)
		{
			HAPI_TransformEuler transform = new HAPI_TransformEuler(true);

			transform.position[0] = -p[0];
			transform.position[1] = p[1];
			transform.position[2] = p[2];

			transform.rotationEuler[0] = r[0];
			transform.rotationEuler[1] = -r[1];
			transform.rotationEuler[2] = -r[2];

			transform.scale[0] = s[0];
			transform.scale[1] = s[1];
			transform.scale[2] = s[2];

			transform.rotationOrder = HAPI_XYZOrder.HAPI_ZXY;
			transform.rstOrder = HAPI_RSTOrder.HAPI_SRT;

			return transform;
		}

		public static Matrix4x4 GetMatrix4x4(ref Vector3 p, ref Vector3 r, ref Vector3 s)
		{
			Matrix4x4 matrix = new Matrix4x4();
			matrix.SetTRS(p, Quaternion.Euler(r.x, r.y, r.z), s);
			return matrix;
		}

		public static bool IsSameTransform(ref Matrix4x4 transformMatrix, ref Vector3 p, ref Vector3 r, ref Vector3 s)
		{
			// TODO: optimize this
			return (transformMatrix == GetMatrix4x4(ref p, ref r, ref s));
		}

		// INPUT NODES ------------------------------------------------------------------------------------------------

		/// <summary>
		/// Uploads given mesh geometry into Houdini.
		/// Creates a new part for given geo node, and uploads vertices, indices, UVs, Normals, and Colors.
		/// </summary>
		/// <param name="session"></param>
		/// <param name="assetNodeID"></param>
		/// <param name="objectID"></param>
		/// <param name="geoID"></param>
		/// <param name="mesh"></param>
		/// <returns>True if successfully uploaded all required data.</returns>
		public static bool UploadMeshIntoHoudiniNode(HEU_SessionBase session, HAPI_NodeId assetNodeID, HAPI_NodeId objectID, HAPI_NodeId geoID, ref Mesh mesh)
		{
			bool bSuccess = false;

			Vector3[] vertices = mesh.vertices;
			int[] triIndices = mesh.triangles;
			Vector2[] uvs = mesh.uv;
			Vector3[] normals = mesh.normals;
			Color[] colors = mesh.colors;

			HAPI_PartInfo partInfo = new HAPI_PartInfo();
			partInfo.faceCount = triIndices.Length / 3;
			partInfo.vertexCount = triIndices.Length;
			partInfo.pointCount = vertices.Length;
			partInfo.pointAttributeCount = 1;
			partInfo.vertexAttributeCount = 0;
			partInfo.primitiveAttributeCount = 0;
			partInfo.detailAttributeCount = 0;

			if(uvs != null && uvs.Length > 0)
			{
				partInfo.pointAttributeCount++;
			}
			if (normals != null && normals.Length > 0)
			{
				partInfo.pointAttributeCount++;
			}
			if (colors != null && colors.Length > 0)
			{
				partInfo.pointAttributeCount++;
			}

			bSuccess = session.SetPartInfo(geoID, 0, ref partInfo);
			if(!bSuccess)
			{
				return false;
			}

			int[] faceCounts = new int[partInfo.faceCount];
			for(int i = 0; i < partInfo.faceCount; ++i)
			{
				faceCounts[i] = 3;
			}
			bSuccess = HEU_GeneralUtility.SetArray2Arg(geoID, 0, session.SetFaceCount, faceCounts, 0, partInfo.faceCount);
			if (!bSuccess)
			{
				return false;
			}

			int[] vertexList = new int[partInfo.vertexCount];
			for(int i = 0; i < partInfo.faceCount; ++i)
			{
				for(int j = 0; j < 3; ++j)
				{
					vertexList[i * 3 + j] = triIndices[i * 3 + j];
				}
			}
			bSuccess = HEU_GeneralUtility.SetArray2Arg(geoID, 0, session.SetVertexList, vertexList, 0, partInfo.vertexCount);
			if (!bSuccess)
			{
				return false;
			}

			bSuccess = SetMeshPointAttribute(session, geoID, 0, HEU_Defines.HAPI_ATTRIB_POSITION, 3, vertices, ref partInfo, true);
			if (!bSuccess)
			{
				return false;
			}

			bSuccess = SetMeshPointAttribute(session, geoID, 0, HEU_Defines.HAPI_ATTRIB_NORMAL, 3, normals, ref partInfo, true);
			if (!bSuccess)
			{
				return false;
			}

			if (uvs != null && uvs.Length > 0)
			{
				Vector3[] uvs3 = new Vector3[uvs.Length];
				for(int i = 0; i < uvs.Length; ++i)
				{
					uvs3[i][0] = uvs[i][0];
					uvs3[i][1] = uvs[i][1];
					uvs3[i][2] = 0;
				}
				bSuccess = SetMeshPointAttribute(session, geoID, 0, HEU_Defines.HAPI_ATTRIB_UV, 3, uvs3, ref partInfo, false);
				if (!bSuccess)
				{
					return false;
				}
			}

			if(colors != null && colors.Length > 0)
			{
				Vector3[] rgb = new Vector3[colors.Length];
				Vector3[] alpha = new Vector3[colors.Length];
				for(int i = 0; i < colors.Length; ++i)
				{
					rgb[i][0] = colors[i].r;
					rgb[i][1] = colors[i].g;
					rgb[i][2] = colors[i].b;

					alpha[i][0] = colors[i].a;
				}

				bSuccess = SetMeshPointAttribute(session, geoID, 0, HEU_Defines.HAPI_ATTRIB_COLOR, 3, rgb, ref partInfo, false);
				if (!bSuccess)
				{
					return false;
				}

				bSuccess = SetMeshPointAttribute(session, geoID, 0, HEU_Defines.HAPI_ATTRIB_ALPHA, 1, alpha, ref partInfo, false);
				if (!bSuccess)
				{
					return false;
				}
			}

			// TODO: additional attributes (for painting)

			return session.CommitGeo(geoID);
		}

		public static bool SetMeshPointAttribute(HEU_SessionBase session, HAPI_NodeId geoID, HAPI_PartId partID, string attrName,
			int tupleSize, Vector3[] data, ref HAPI_PartInfo partInfo, bool bConvertToHoudiniCoordinateSystem)
		{
			HAPI_AttributeInfo attrInfo = new HAPI_AttributeInfo();
			attrInfo.exists = true;
			attrInfo.owner = HAPI_AttributeOwner.HAPI_ATTROWNER_POINT;
			attrInfo.storage = HAPI_StorageType.HAPI_STORAGETYPE_FLOAT;
			attrInfo.count = partInfo.pointCount;
			attrInfo.tupleSize = tupleSize;
			attrInfo.originalOwner = HAPI_AttributeOwner.HAPI_ATTROWNER_INVALID;

			float[] attrValues = new float[partInfo.pointCount * tupleSize];

			if (session.AddAttribute(geoID, 0, attrName, ref attrInfo))
			{
				float conversionMultiplier = bConvertToHoudiniCoordinateSystem ? -1f : 1f;

				for (int i = 0; i < partInfo.pointCount; ++i)
				{
					attrValues[i * tupleSize + 0] = conversionMultiplier * data[i][0];

					for (int j = 1; j < tupleSize; ++j)
					{
						attrValues[i * tupleSize + j] = data[i][j];
					}
				}
			}

			return HEU_GeneralUtility.SetAttributeArray(geoID, partID, attrName, ref attrInfo, attrValues, session.SetAttributeFloatData, partInfo.pointCount);
		}

		public static bool SetMeshVertexAttribute(HEU_SessionBase session, HAPI_NodeId geoID, HAPI_PartId partID, string attrName,
			int tupleSize, Vector3[] data, int[] indices, ref HAPI_PartInfo partInfo, bool bConvertToHoudiniCoordinateSystem)
		{
			HAPI_AttributeInfo attrInfo = new HAPI_AttributeInfo();
			attrInfo.exists = true;
			attrInfo.owner = HAPI_AttributeOwner.HAPI_ATTROWNER_VERTEX;
			attrInfo.storage = HAPI_StorageType.HAPI_STORAGETYPE_FLOAT;
			attrInfo.count = partInfo.vertexCount;
			attrInfo.tupleSize = tupleSize;
			attrInfo.originalOwner = HAPI_AttributeOwner.HAPI_ATTROWNER_INVALID;

			float[] attrValues = new float[partInfo.vertexCount * tupleSize];

			if (session.AddAttribute(geoID, 0, attrName, ref attrInfo))
			{
				float conversionMultiplier = bConvertToHoudiniCoordinateSystem ? -1f : 1f;

				for (int i = 0; i < partInfo.vertexCount; ++i)
				{
					attrValues[i * tupleSize + 0] = conversionMultiplier * data[indices[i]][0];

					for (int j = 1; j < tupleSize; ++j)
					{
						attrValues[i * tupleSize + j] = data[ indices[i] ][j];
					}
				}
			}

			return HEU_GeneralUtility.SetAttributeArray(geoID, partID, attrName, ref attrInfo, attrValues, session.SetAttributeFloatData, partInfo.vertexCount);
		}

		public static bool UploadMeshIntoInputNode(HEU_SessionBase session, HAPI_NodeId connectedAssetID, ref Mesh mesh, ref Material[] materials, string meshPath)
		{
			Vector3[] vertices = mesh.vertices;
			Vector2[] uvs = mesh.uv;
			Vector3[] normals = mesh.normals;
			Color[] colors = mesh.colors;

			int subMeshCount = mesh.subMeshCount;

			uint[] indexStart = new uint[subMeshCount];
			uint[] indexCount = new uint[subMeshCount];

			// For more than 1 submesh, get the triangle indices in order of how submeshes have been assigned.
			// This order is later used for assigning material IDs.
			int[] triIndices = null;
			if (subMeshCount > 1)
			{
				List<int> triIndexList = new List<HAPI_NodeId>();
				for (int i = 0; i < subMeshCount; ++i)
				{
					uint preCount = (uint)triIndexList.Count;
					triIndexList.AddRange(mesh.GetTriangles(i));

					// GetIndexStart and GetIndexCount are available Unity 5.5+
#if UNITY_5_5_OR_NEWER
					indexStart[i] = mesh.GetIndexStart(i);
					indexCount[i] = mesh.GetIndexCount(i);
#else
					indexStart[i] = preCount;
					indexCount[i] = (uint)(triIndexList.Count - preCount);
#endif
				}
				triIndices = triIndexList.ToArray();
			}
			else
			{
				triIndices = mesh.triangles;

				indexStart[0] = 0;
				indexCount[0] = (uint)triIndices.Length;
			}
			

			HAPI_PartInfo partInfo = new HAPI_PartInfo();
			partInfo.faceCount = triIndices.Length / 3;
			partInfo.vertexCount = triIndices.Length;
			partInfo.pointCount = vertices.Length;
			partInfo.pointAttributeCount = 1;
			partInfo.vertexAttributeCount = 0;
			partInfo.primitiveAttributeCount = 0;
			partInfo.detailAttributeCount = 0;

			if (uvs != null && uvs.Length > 0)
			{
				partInfo.vertexAttributeCount++;
			}
			if (normals != null && normals.Length > 0)
			{
				partInfo.vertexAttributeCount++;
			}
			if (colors != null && colors.Length > 0)
			{
				partInfo.vertexAttributeCount++;
			}
			if (materials != null)
			{
				partInfo.primitiveAttributeCount++;
			}
			if (!string.IsNullOrEmpty(meshPath))
			{
				partInfo.primitiveAttributeCount++;
			}

			HAPI_GeoInfo displayGeoInfo = new HAPI_GeoInfo();
			if(!session.GetDisplayGeoInfo(connectedAssetID, ref displayGeoInfo))
			{
				return false;
			}

			HAPI_NodeId displayNodeID = displayGeoInfo.nodeId;

			if(!session.SetPartInfo(displayNodeID, 0, ref partInfo))
			{
				return false;
			}

			int[] faceCounts = new int[partInfo.faceCount];
			for (int i = 0; i < partInfo.faceCount; ++i)
			{
				faceCounts[i] = 3;
			}
			if(!HEU_GeneralUtility.SetArray2Arg(displayNodeID, 0, session.SetFaceCount, faceCounts, 0, partInfo.faceCount))
			{
				return false;
			}

			if(!HEU_GeneralUtility.SetArray2Arg(displayNodeID, 0, session.SetVertexList, triIndices, 0, partInfo.vertexCount))
			{
				return false;
			}

			if(!SetMeshPointAttribute(session, displayNodeID, 0, HEU_Defines.HAPI_ATTRIB_POSITION, 3, vertices, ref partInfo, true))
			{
				return false;
			}

			//if(normals != null && !SetMeshPointAttribute(session, displayNodeID, 0, HEU_Defines.HAPI_ATTRIB_NORMAL, 3, normals, ref partInfo, true))
			if(normals != null && !SetMeshVertexAttribute(session, displayNodeID, 0, HEU_Defines.HAPI_ATTRIB_NORMAL, 3, normals, triIndices, ref partInfo, true))
			{
				return false;
			}
			
			if (uvs != null && uvs.Length > 0)
			{
				Vector3[] uvs3 = new Vector3[uvs.Length];
				for (int i = 0; i < uvs.Length; ++i)
				{
					uvs3[i][0] = uvs[i][0];
					uvs3[i][1] = uvs[i][1];
					uvs3[i][2] = 0;
				}
				//if(!SetMeshPointAttribute(session, displayNodeID, 0, HEU_Defines.HAPI_ATTRIB_UV, 3, uvs3, ref partInfo, false))
				if (!SetMeshVertexAttribute(session, displayNodeID, 0, HEU_Defines.HAPI_ATTRIB_UV, 3, uvs3, triIndices, ref partInfo, false))
				{
					return false;
				}
			}

			if (colors != null && colors.Length > 0)
			{
				Vector3[] rgb = new Vector3[colors.Length];
				Vector3[] alpha = new Vector3[colors.Length];
				for (int i = 0; i < colors.Length; ++i)
				{
					rgb[i][0] = colors[i].r;
					rgb[i][1] = colors[i].g;
					rgb[i][2] = colors[i].b;

					alpha[i][0] = colors[i].a;
				}

				//if(!SetMeshPointAttribute(session, displayNodeID, 0, HEU_Defines.HAPI_ATTRIB_COLOR, 3, rgb, ref partInfo, false))
				if (!SetMeshVertexAttribute(session, displayNodeID, 0, HEU_Defines.HAPI_ATTRIB_COLOR, 3, rgb, triIndices, ref partInfo, false))
				{
					return false;
				}

				//if(!SetMeshPointAttribute(session, displayNodeID, 0, HEU_Defines.HAPI_ATTRIB_ALPHA, 1, alpha, ref partInfo, false))
				if (!SetMeshVertexAttribute(session, displayNodeID, 0, HEU_Defines.HAPI_ATTRIB_ALPHA, 1, alpha, triIndices, ref partInfo, false))
				{
					return false;
				}
			}
			
			// TODO: additional attributes

			// Set material names for round-trip
			// Based on number of submeshes. The submesh is simply a separate index buffer.
			// Number of materials correspond to number of submeshes.
			if (materials != null)
			{
				if(mesh.subMeshCount != materials.Length)
				{
					Debug.LogWarningFormat("Number of materials {0} does not match number of submeshes {1}. Unable to upload material IDs!", materials.Length, mesh.subMeshCount);
				}
				else
				{
					HAPI_AttributeInfo materialIDAttrInfo = new HAPI_AttributeInfo();
					materialIDAttrInfo.exists = true;
					materialIDAttrInfo.owner = HAPI_AttributeOwner.HAPI_ATTROWNER_PRIM;
					materialIDAttrInfo.storage = HAPI_StorageType.HAPI_STORAGETYPE_STRING;
					materialIDAttrInfo.count = partInfo.faceCount;
					materialIDAttrInfo.tupleSize = 1;
					materialIDAttrInfo.originalOwner = HAPI_AttributeOwner.HAPI_ATTROWNER_INVALID;

					if (session.AddAttribute(displayNodeID, 0, HEU_PluginSettings.UnityMaterialAttribName, ref materialIDAttrInfo))
					{
						string[] materialIDs = new string[partInfo.faceCount];

						string[] materialName = new string[subMeshCount];

						// For each submesh, find the material name. Then assign material name
						// to primitive attribute by mapping index to submesh's start + count indices
						for (int i = 0; i < subMeshCount; ++i)
						{
							materialName[i] = HEU_AssetDatabase.GetAssetPath(materials[i]);
							if(materialName[i] == null)
							{
								materialName[i] = "";
							}
							else if(materialName[i].StartsWith(HEU_Defines.DEFAULT_UNITY_BUILTIN_RESOURCES))
							{
								materialName[i] = HEU_AssetDatabase.GetUniqueAssetPathForUnityAsset(materials[i]);
							}

							uint face = indexStart[i] / 3;
							for (uint j = 0; j < indexCount[i]; j=j+3, ++face)
							{
								materialIDs[face] = materialName[i];
							}
						}

						if (!session.SetAttributeStringData(displayNodeID, 0, HEU_PluginSettings.UnityMaterialAttribName, ref materialIDAttrInfo, materialIDs, 0, partInfo.faceCount))
						{
							return false;
						}
					}
					else
					{
						return false;
					}
				}
			}

			// Set input mesh name
			if (!string.IsNullOrEmpty(meshPath))
			{
				HAPI_AttributeInfo attrInfo = new HAPI_AttributeInfo();
				attrInfo.exists = true;
				attrInfo.owner = HAPI_AttributeOwner.HAPI_ATTROWNER_PRIM;
				attrInfo.storage = HAPI_StorageType.HAPI_STORAGETYPE_STRING;
				attrInfo.count = partInfo.faceCount;
				attrInfo.tupleSize = 1;
				attrInfo.originalOwner = HAPI_AttributeOwner.HAPI_ATTROWNER_INVALID;

				if (session.AddAttribute(displayNodeID, 0, HEU_PluginSettings.UnityInputMeshAttr, ref attrInfo))
				{
					string[] primitiveNameAttr = new string[partInfo.faceCount];
					for (int i = 0; i < partInfo.faceCount; ++i)
					{
						primitiveNameAttr[i] = meshPath;
					}

					if(!session.SetAttributeStringData(displayNodeID, 0, HEU_PluginSettings.UnityInputMeshAttr, ref attrInfo, primitiveNameAttr, 0, partInfo.faceCount))
					{
						return false;
					}
				}
				else
				{
					return false;
				}
			}

			return session.CommitGeo(displayNodeID);
		}

		public static bool CreateInputNodeWithGeoData(HEU_SessionBase session, HAPI_NodeId assetID, GameObject inputObject, out HAPI_NodeId inputNodeID)
		{
			inputNodeID = HEU_Defines.HEU_INVALID_NODE_ID;

			MeshFilter meshfilter = inputObject.GetComponent<MeshFilter>();
			if (meshfilter == null)
			{
				return false;
			}

			Mesh mesh = meshfilter.sharedMesh;
			if (mesh == null)
			{
				return false;
			}

			if (!HEU_HAPIUtility.IsAssetValidInHoudini(session, assetID))
			{
				return false;
			}

			// If connected asset is not valid, then need to create an input asset
			if (inputNodeID == HEU_Defines.HEU_INVALID_NODE_ID)
			{
				string inputName = null;

				HAPI_NodeId newNodeID = HEU_Defines.HEU_INVALID_NODE_ID;
				session.CreateInputNode(out newNodeID, inputName);
				if (newNodeID == HEU_Defines.HEU_INVALID_NODE_ID || !HEU_HAPIUtility.IsAssetValidInHoudini(session, newNodeID))
				{
					Debug.LogErrorFormat("Failed to create new input node in Houdini session!");
					return false;
				}

				inputNodeID = newNodeID;

				if (!session.CookNode(inputNodeID, false))
				{
					Debug.LogErrorFormat("New input node failed to cook!");
					return false;
				}
			}

			string objectPath = HEU_AssetDatabase.GetAssetOrScenePath(inputObject);
			if(string.IsNullOrEmpty(objectPath))
			{
				objectPath = inputObject.name;
			}

			Material[] materials = null;
			MeshRenderer meshRenderer = inputObject.GetComponent<MeshRenderer>();
			if(meshRenderer != null)
			{
				materials = meshRenderer.sharedMaterials;
			}

			return UploadMeshIntoInputNode(session, inputNodeID, ref mesh, ref materials, objectPath);
		}

		public static bool CreateInputNodeWithMultiObjects(HEU_SessionBase session, HAPI_NodeId assetID, 
			ref HAPI_NodeId connectedAssetID, ref List<HEU_InputObjectInfo> inputObjects, ref List<HAPI_NodeId> inputObjectsConnectedAssetIDs, bool bKeepWorldTransform)
		{
			// Create the merge SOP node.
			if (!session.CreateNode(-1, "SOP/merge", null, true, out connectedAssetID))
			{
				Debug.LogErrorFormat("Unable to create merge SOP node for connecting input assets.");
				return false;
			}

			int numObjects = inputObjects.Count;
			for (int i = 0; i < numObjects; ++i)
			{
				HAPI_NodeId meshNodeID = HEU_Defines.HEU_INVALID_NODE_ID;
				inputObjectsConnectedAssetIDs.Add(meshNodeID);

				// Skipping null gameobjects. Though if this causes issues, can always let it continue
				// to create input node, but not upload mesh data
				if (inputObjects[i]._gameObject == null)
				{
					continue;
				}

				bool bResult = CreateInputNodeWithGeoData(session, assetID, inputObjects[i]._gameObject, out meshNodeID);
				if (!bResult || meshNodeID == HEU_Defines.HEU_INVALID_NODE_ID)
				{
					string errorMsg = string.Format("Input at index {0} is not valid", i);
					if (inputObjects[i]._gameObject.GetComponent<HEU_HoudiniAssetRoot>() != null)
					{
						errorMsg += " because it is an HDA. Change the Input Type to HDA.";
					}
					else if (inputObjects[i]._gameObject.GetComponent<MeshFilter>() == null || inputObjects[i]._gameObject.GetComponent<MeshFilter>().sharedMesh == null)
					{
						errorMsg += " because it does not have a valid Mesh. Make sure the GameObject has a MeshFilter component with a valid mesh.";
					}
					else
					{
						errorMsg += ". Unable to create input node.";
					}

					Debug.LogErrorFormat(errorMsg);

					// Skipping this and continuing input processing since this isn't a deal breaker
					continue;
				}

				inputObjectsConnectedAssetIDs[i] = meshNodeID;

				if (!session.ConnectNodeInput(connectedAssetID, i, meshNodeID))
				{
					Debug.LogErrorFormat("Unable to connect input nodes!");
					return false;
				}

				UploadInputObjectTransform(session, inputObjects[i], meshNodeID, bKeepWorldTransform);
			}

			return true;
		}

		public static bool UploadInputObjectTransform(HEU_SessionBase session, HEU_InputObjectInfo inputObject, HAPI_NodeId connectedAssetID, bool bKeepWorldTransform)
		{
			Matrix4x4 inputTransform = Matrix4x4.identity;
			if(inputObject._useTransformOffset)
			{
				if(bKeepWorldTransform)
				{
					// Add offset tranform to world transform
					Transform inputObjTransform = inputObject._gameObject.transform;
					Vector3 position = inputObjTransform.position + inputObject._translateOffset;
					Quaternion rotation = inputObjTransform.rotation * Quaternion.Euler(inputObject._rotateOffset);
					Vector3 scale = Vector3.Scale(inputObjTransform.localScale, inputObject._scaleOffset);

					Vector3 rotVector = rotation.eulerAngles;
					inputTransform = GetMatrix4x4(ref position, ref rotVector, ref scale);
				}
				else
				{
					// Offset from origin.
					inputTransform = GetMatrix4x4(ref inputObject._translateOffset, ref inputObject._rotateOffset, ref inputObject._scaleOffset);
				}
			}
			else
			{
				inputTransform = inputObject._gameObject.transform.localToWorldMatrix;
			}

			HAPI_TransformEuler transformEuler = GetHAPITransformFromMatrix(ref inputTransform);

			HAPI_NodeInfo meshNodeInfo = new HAPI_NodeInfo();
			if (!session.GetNodeInfo(connectedAssetID, ref meshNodeInfo))
			{
				return false;
			}

			if (session.SetObjectTransform(meshNodeInfo.parentId, ref transformEuler))
			{
				inputObject._syncdTransform = inputTransform;
			}

			return true;
		}

		public static bool DoesGeoPartHaveAttribute(HEU_SessionBase session, HAPI_NodeId geoID, HAPI_PartId partID, string attrName, HAPI_AttributeOwner owner, ref HAPI_AttributeInfo attributeInfo)
		{
			if (session.GetAttributeInfo(geoID, partID, attrName, owner, ref attributeInfo))
			{
				return attributeInfo.exists;
				//Debug.LogFormat("Attr {0} exists={1}, with count={2}, type={3}, storage={4}, tuple={5}", "Cd", colorAttrInfo.exists, colorAttrInfo.count, colorAttrInfo.typeInfo, colorAttrInfo.storage, colorAttrInfo.tupleSize);
			}
			return false;
		}

#if UNITY_EDITOR
		public static int TangentModeToHoudiniRampInterpolation(AnimationUtility.TangentMode tangentMode)
		{
			if (tangentMode == AnimationUtility.TangentMode.Constant)
			{
				return 0;
			}
			else if (tangentMode == AnimationUtility.TangentMode.Linear)
			{
				return 1;
			}

			// Use Catmull-Rom for all smooth interpolation
			return 2;
		}

		public static AnimationUtility.TangentMode HoudiniRampInterpolationToTangentMode(int interpolation)
		{
			// interpolation == 0 -> Constant		=> TangentMode.Constant
			// interpolation == 1 -> Linear			=> TangentMode.Linear
			// interpolation == 2 -> Catmull-Rom	=> TangentMode.Free

			if(interpolation == 0)
			{
				return AnimationUtility.TangentMode.Constant;
			}
			else if (interpolation == 1)
			{
				return AnimationUtility.TangentMode.Linear;
			}

			// Use Free for all smooth interpolation
			return AnimationUtility.TangentMode.Free;
		}

		public static int GradientModeToHoudiniColorRampInterpolation(GradientMode gradientMode)
		{
			return (gradientMode == GradientMode.Blend) ? 1 : 0;
		}
#endif

		public static void SetAnimationCurveTangentModes(AnimationCurve animCurve, List<int> tangentValues)
		{
#if UNITY_EDITOR
			AnimationUtility.TangentMode leftTangent = AnimationUtility.TangentMode.Free;
			AnimationUtility.TangentMode rightTangent = AnimationUtility.TangentMode.Free;
			for(int i = 0; i < tangentValues.Count; ++i)
			{
				if (i > 0)
				{
					leftTangent = rightTangent;
				}
				rightTangent = HEU_HAPIUtility.HoudiniRampInterpolationToTangentMode(tangentValues[i]);

				AnimationUtility.SetKeyLeftTangentMode(animCurve, i, leftTangent);
				AnimationUtility.SetKeyRightTangentMode(animCurve, i, rightTangent);
			}
#endif
		}
	}

}   // HoudiniEngineUnity

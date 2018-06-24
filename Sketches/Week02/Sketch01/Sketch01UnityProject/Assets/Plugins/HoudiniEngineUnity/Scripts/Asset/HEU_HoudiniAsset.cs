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

// Uncomment to profile
//#define HEU_PROFILER_ON

using System.Text;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif



namespace HoudiniEngineUnity
{
	/////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	// Typedefs (copy these from HEU_Common.cs)
	using HAPI_NodeId = System.Int32;
	using HAPI_AssetLibraryId = System.Int32;
	using HAPI_StringHandle = System.Int32;
	using HAPI_ErrorCodeBits = System.Int32;
	using HAPI_NodeTypeBits = System.Int32;
	using HAPI_NodeFlagsBits = System.Int32;
	using HAPI_ParmId = System.Int32;
	using HAPI_PartId = System.Int32;

	/// <summary>
	/// Represents a Houdini Digital Asset in Unity.
	/// Contains object nodes, geo nodes, and parts for an HDA.
	/// Contains HDA's parameters.
	/// Load, (re)cook, and bake out asset.
	/// Can (and should) be excluded from builds & runtime.
	/// </summary>
	[ExecuteInEditMode]	// OnEnable/OnDisable for registering for tick
	public sealed class HEU_HoudiniAsset : MonoBehaviour
	{
		//	ASSET DATA ------------------------------------------------------------------------------------------------

		public enum HEU_AssetType
		{
			TYPE_INVALID = 0,
			TYPE_HDA,
			TYPE_CURVE,
			TYPE_INPUT
		}

		[SerializeField]
		private HEU_AssetType _assetType;
		public HEU_AssetType AssetType { get { return _assetType; } }

		[SerializeField]
		private HAPI_AssetInfo _assetInfo;
		public HAPI_AssetInfo AssetInfo { get { return _assetInfo; } }

		[SerializeField]
		private HAPI_NodeInfo _nodeInfo;
		public HAPI_NodeInfo NodeInfo { get { return _nodeInfo; } }

		[SerializeField]
		private string _assetName;
		public string AssetName { get { return _assetName; } }

		[SerializeField]
		private string _assetOpName;
		public string AssetOpName { get { return _assetOpName; } }

		[SerializeField]
		private string _assetHelp;
		public string AssetHelp { get { return _assetHelp; } }

		public int TransformInputCount { get { return _assetInfo.transformInputCount; } }

		public int GeoInputCount { get { return _assetInfo.geoInputCount; } }


		[SerializeField]
		private HAPI_NodeId _assetID = HEU_Defines.HEU_INVALID_NODE_ID;
		public HAPI_NodeId AssetID { get { return _assetID; } }

		[SerializeField]
		private string _assetPath;
		public string AssetPath { get { return _assetPath; } }

#pragma warning disable 0414
		[SerializeField]
		private UnityEngine.Object _assetFileObject;
#pragma warning restore 0414

		public int HandleCount { get { return _assetInfo.handleCount; } }

		[SerializeField]
		private List<HEU_ObjectNode> _objectNodes;

		public GameObject OwnerGameObject { get { return this.gameObject; } }

		[SerializeField]
		private GameObject _rootGameObject;
		public GameObject RootGameObject { get { return _rootGameObject; } }

		[SerializeField]
		private List<HEU_MaterialData> _materialCache;

		[SerializeField]
		private HEU_Parameters _parameters;

		public HEU_Parameters Parameters { get { return _parameters; } }

		[SerializeField]
		private Matrix4x4 _lastSyncedTransformMatrix;

		// Location of this asset's cache folder for storing persistant data
		[SerializeField]
		private string _assetCacheFolderPath;

		// BUILD & COOK -----------------------------------------------------------------------------------------------

		public enum AssetBuildAction
		{
			NONE,
			RELOAD,
			COOK,
			INVALID,
			STRIP_HEDATA,
			DUPLICATE
		}

		[SerializeField]
		private AssetBuildAction _requestBuildAction;

#pragma warning disable 0414
		[SerializeField]
		private bool _checkParameterChangeForCook;

		[SerializeField]
		private bool _skipCookCheck;

		[SerializeField]
		private bool _uploadParameters;

		public enum AssetCookStatus
		{
			NONE,
			COOKING,
			POSTCOOK,
			LOADING,
			POSTLOAD
		}

		[SerializeField]
		private AssetCookStatus _cookStatus;

#pragma warning restore 0414

		public enum AssetCookResult
		{
			NONE,
			SUCCESS,
			ERRORED
		}

		[SerializeField]
		private AssetCookResult _lastCookResult;

		[SerializeField]
		private bool _isCookingAssetReloaded;

		// Force everything to be updated without checking if changed
		private bool _bForceUpdate;

		[SerializeField]
		private long _sessionID;

		// UI TOGGLES -------------------------------------------------------------------------------------------------

		// Disable the warning for unused variables. We're accessing these as SerializedProperty.
#pragma warning disable 0414

		// Whether to show object instance inputs on the UI
		[SerializeField]
		private bool _showInstanceInputs = true;

		[SerializeField]
		private bool _showHDAOptions = false;

		[SerializeField]
		private bool _showGenerateSection = true;

		[SerializeField]
		private bool _showBakeSection = true;

		[SerializeField]
		private bool _showEventsSection = false;

		[SerializeField]
		private bool _showCurvesSection = false;

		[SerializeField]
		private bool _showInputNodesSection = false;

		[SerializeField]
		private bool _showToolsSection = false;

#pragma warning restore 0414

		// ASSET EVENTS -----------------------------------------------------------------------------------------------

		public ReloadEvent _reloadEvent = new ReloadEvent();
		public CookedEvent _cookedEvent = new CookedEvent();
		public BakedEvent _bakedEvent = new BakedEvent();

		// Delegate for Editor window to hook into for callback when needing updating
		public delegate void UpdateUIDelegate();
		public UpdateUIDelegate _refreshUIDelegate;

		// CONNECTIONS ------------------------------------------------------------------------------------------------

		public CookedEvent _downstreamConnectionCookedEvent;

		// HDA OPTIONS ------------------------------------------------------------------------------------------------

		[SerializeField]
		private bool _generateUVs = false;
		public bool GenerateUVs { get { return _generateUVs; } set { _generateUVs = value; } }

		[SerializeField]
		private bool _generateTangents = true;
		public bool GenerateTangents { get { return _generateTangents; } set { _generateTangents = value; } }

		[SerializeField]
		private bool _pushTransformToHoudini = true;
		public bool PushTransformToHoudini { get { return _pushTransformToHoudini; } set { _pushTransformToHoudini = value; } }

		[SerializeField]
		private bool _transformChangeTriggersCooks = false;
		public bool TransformChangeTriggersCooks { get { return _transformChangeTriggersCooks; } set { _transformChangeTriggersCooks = value; } }

		[SerializeField]
		private bool _cookingTriggersDownCooks = true;
		public bool CookingTriggersDownCooks { get { return _cookingTriggersDownCooks; } set { _cookingTriggersDownCooks = value; } }

		[SerializeField]
		private bool _autoCookOnParameterChange = true;
		public bool AutoCookOnParameterChange { get { return _autoCookOnParameterChange; } set { _autoCookOnParameterChange = value; } }

		[SerializeField]
		private bool _ignoreNonDisplayNodes = false;
		public bool IgnoreNonDisplayNodes { get { return _ignoreNonDisplayNodes; } set { _ignoreNonDisplayNodes = value; } }


		// CURVES -----------------------------------------------------------------------------------------------------

		// Toggle curve editing tool in Scene view
		[SerializeField]
		private bool _curveEditorEnabled = true;

		public bool CurveEditorEnabled { get { return _curveEditorEnabled; } set { _curveEditorEnabled = value; } }

		[SerializeField]
		private List<HEU_Curve> _curves;

		[SerializeField]
		private HEU_Curve.CurveDrawCollision _curveDrawCollision;

		[SerializeField]
		private List<Collider> _curveDrawColliders = new List<Collider>();

		[SerializeField]
		private LayerMask _curveDrawLayerMask;

		public HEU_Curve.CurveDrawCollision CurveDrawCollision { get { return _curveDrawCollision; } set { _curveDrawCollision = value; } }

		public List<Collider> GetCurveDrawColliders() { return _curveDrawColliders; }

		public LayerMask GetCurveDrawLayerMask() { return _curveDrawLayerMask; }

		public void SetCurveDrawLayerMask(LayerMask mask) { _curveDrawLayerMask = mask; }

		// INPUT NODES ------------------------------------------------------------------------------------------------

		[SerializeField]
		private List<HEU_InputNode> _inputNodes;

		// HANDLES ----------------------------------------------------------------------------------------------------

		[SerializeField]
		private List<HEU_Handle> _handles;

		[SerializeField]
		private bool _handlesEnabled = true;

		public bool HandlesEnabled { get { return _handlesEnabled; } set { _handlesEnabled = value; } }

		// TOOLS ------------------------------------------------------------------------------------------------------

		[SerializeField]
		private List<HEU_AttributesStore> _attributeStores;

		[SerializeField]
		private bool _editableNodesToolsEnabled = false;

		public bool EditableNodesToolsEnabled { get { return _editableNodesToolsEnabled; } set { _editableNodesToolsEnabled = value; } }

		[SerializeField]
		private HEU_ToolsInfo _toolsInfo;

		public HEU_ToolsInfo ToolsInfo { get { return _toolsInfo; } }

		// PROFILE ----------------------------------------------------------------------------------------------------

#if HEU_PROFILER_ON
		private float _cookStartTime;
		private float _hapiCookEndTime;
		private float _postCookStartTime;
#endif

		//  LOGIC -----------------------------------------------------------------------------------------------------

		/// <summary>
		/// Setup as a new asset
		/// </summary>
		/// <param name="assetType"></param>
		/// <param name="filePath"></param>
		/// <param name="rootGameObject"></param>
		public void SetupAsset(HEU_AssetType assetType, string filePath, GameObject rootGameObject, HEU_SessionBase session)
		{
			_assetType = assetType;
			_assetPath = filePath;
			_rootGameObject = rootGameObject;
			_objectNodes = new List<HEU_ObjectNode>();
			_materialCache = new List<HEU_MaterialData>();
			_parameters = null;
			_curves = new List<HEU_Curve>();
			_inputNodes = new List<HEU_InputNode>();
			_handles = new List<HEU_Handle>();
			_attributeStores = new List<HEU_AttributesStore>();
			_toolsInfo = ScriptableObject.CreateInstance<HEU_ToolsInfo>();

			_showCurvesSection = _assetType == HEU_AssetType.TYPE_CURVE;
			_showInputNodesSection = _assetType == HEU_AssetType.TYPE_INPUT;

			Debug.AssertFormat(session != null && session.IsSessionValid(), "Must have valid session for new asset");
			_sessionID = session.GetSessionData().SessionID;
		}

		/// <summary>
		/// Returns true if this asset has been saved in a scene.
		/// </summary>
		/// <returns>True if asset has been saved in a scene.</returns>
		public bool IsAssetSavedInScene()
		{
			return HEU_AssetDatabase.IsAssetSavedInScene(this.gameObject);
		}

		private void Awake()
		{
#if HOUDINIENGINEUNITY_ENABLED
			//Debug.Log("HEU_HoudiniAsset::Awake - " + AssetName);

			// All assets are checked if valid in Houdini Engine session in Awake.
			// Awake is called at scene load / script compilation.
			// This is done in Awake to make sure it happens for all assets at same time,
			// as otherwise if other assets are already loaded before this check, they might have 
			// same uniqueHoudiniNodeId.
			HEU_SessionBase session = GetAssetSession(false);
			if(session == null || !IsAssetValidInHoudini(session))
			{
				_assetID = HEU_Defines.HEU_INVALID_NODE_ID;
			}

			// Clear out the delegate because receiver might not exist on code refresh
			_refreshUIDelegate = null;
#endif
		}

		private void OnEnable()
		{
#if HOUDINIENGINEUNITY_ENABLED
			// Adding in OnEnable as its called after a code recompile (Awake is not).
			HEU_AssetUpdater.AddAssetForUpdate(this);
#endif
		}

		private void OnDestroy()
		{
#if HOUDINIENGINEUNITY_ENABLED
			HEU_AssetUpdater.RemoveAsset(this);
#endif
		}

		public void AssetUpdate()
		{
#if HOUDINIENGINEUNITY_ENABLED

			if (_cookStatus == AssetCookStatus.POSTCOOK || _cookStatus == AssetCookStatus.POSTLOAD)
			{
				// Only case where the cook status is stil in postcook or postload mode is
				// if it had errored out in Unity. It is okay to then reset cook state, but with error flagged
				SetCookStatus(AssetCookStatus.NONE, AssetCookResult.ERRORED);
			}

			if (_cookStatus == AssetCookStatus.COOKING)
			{
				// Wait for cooking in Houdini to complete
				ProcessHoudiniCookStatus(true);
			}
			else if (_cookStatus == AssetCookStatus.NONE)
			{
				// Not cooking. Process any requests.

				if (HEU_PluginSettings.TransformChangeTriggersCooks && TransformChangeTriggersCooks)
				{
					if(HasTransformChangedSinceLastUpdate() || HasInputNodeTransformChanged())
					{
						RequestCook(true, false, false, true);
					}
				}

				if (_requestBuildAction == AssetBuildAction.RELOAD)
				{
					ClearBuildRequest();
					DoRebuild();
				}
				else if (_requestBuildAction == AssetBuildAction.COOK)
				{
					bool thisCheckParameterChangeForCook = _checkParameterChangeForCook;
					bool thisCkipCookCheck = _skipCookCheck;
					bool thisUploadParameters = _uploadParameters;
					ClearBuildRequest();
					RecookAsync(thisCheckParameterChangeForCook, thisCkipCookCheck, thisUploadParameters);
				}
				else if (_requestBuildAction == AssetBuildAction.STRIP_HEDATA)
				{
					ClearBuildRequest();
					HEU_HoudiniAssetRoot assetRoot = _rootGameObject.GetComponent<HEU_HoudiniAssetRoot>();
					if (assetRoot != null)
					{
						assetRoot.RemoveHoudiniEngineAssetData();
					}
					else
					{
						Debug.LogError(HEU_Defines.HEU_NAME +  ": Unable to Bake In Place due to HEU_HoudiniAssetRoot not found!");
					}
				}
				else if (_requestBuildAction == AssetBuildAction.DUPLICATE)
				{
					ClearBuildRequest();
					DuplicateAsset();
				}
			}
#endif
		}

		/// <summary>
		/// Public interface to request a full reload / build of the asset.
		/// Will reset to same state as if it was just instantiated, but keep
		/// existing transform information and place in Hierarchy.
		/// <param name="bAsync">Reload asynchronoulsy if true, or block until reload completed.</param>
		/// </summary>
		public void RequestReload(bool bAsync)
		{
#if HOUDINIENGINEUNITY_ENABLED
			if (bAsync)
			{
				_requestBuildAction = AssetBuildAction.RELOAD;
			}
			else
			{
				ClearBuildRequest();
				DoRebuild();
			}
#endif
		}

		/// <summary>
		/// Public interface to request a cook of this asset.
		/// Can be async or blocking. If async will return once cook has finished.
		/// </summary>
		/// <param name="bCheckParamsChanged">If true, then will only upload parameters that have changed.</param>
		/// <param name="bAsync">Cook asynchronously or block until cooking is done.</param>
		/// <param name="bSkipCookCheck">If true, will force cook even if cooking is disabled.</param>
		/// <param name="bUploadParameters">If true, will upload parameter values before cooking.</param>
		public void RequestCook(bool bCheckParametersChanged, bool bAsync, bool bSkipCookCheck, bool bUploadParameters)
		{
#if HOUDINIENGINEUNITY_ENABLED
			//Debug.Log(HEU_Defines.HEU_NAME + ": Requesting Cook");

			if (bAsync)
			{
				// We don't want to override Reload or Invalid actions, so
				// for now, only set request if no other pending build actions.
				if (_requestBuildAction == AssetBuildAction.NONE)
				{
					_requestBuildAction = AssetBuildAction.COOK;
				}

				// This could be an update on the cook settings
				if (_requestBuildAction == AssetBuildAction.COOK)
				{
					_checkParameterChangeForCook = bCheckParametersChanged;
					_skipCookCheck = bSkipCookCheck;
					_uploadParameters = bUploadParameters;
				}
				else
				{
					Debug.LogWarning(HEU_Defines.HEU_NAME + ": Asset busy. Unable to start cooking!");
				}
			}
			else
			{
				if (_cookStatus == AssetCookStatus.NONE)
				{
					RecookBlocking(bCheckParametersChanged, bSkipCookCheck, bUploadParameters);
				}
				else
				{
					Debug.LogWarning(HEU_Defines.HEU_NAME + ": Houdini Engine: Asset busy. Unable to start cooking!");
				}
			}
#endif
		}

		public void RequestBakeInPlace()
		{
			if(_requestBuildAction == AssetBuildAction.NONE)
			{
				_requestBuildAction = AssetBuildAction.STRIP_HEDATA;
			}
		}

		public void ClearBuildRequest()
		{
			_requestBuildAction = AssetBuildAction.NONE;
			_checkParameterChangeForCook = false;
			_skipCookCheck = false;
			_uploadParameters = true;
		}

		private bool HasValidAssetPath()
		{
			return !string.IsNullOrEmpty(_assetPath);
		}

		/// <summary>
		/// Completely rebuild this asset, including all geometry, meshes, colliders, materials, and textures.
		/// Notifies listeners.
		/// </summary>
		private void DoRebuild()
		{
			bool bResult = InternalRebuild();

			// Notify listeners that we've built!
			if (_reloadEvent != null)
			{
				List<GameObject> outputObjects = new List<GameObject>();
				GetOutputGameObjects(outputObjects);
				_reloadEvent.Invoke(this, bResult, outputObjects);
			}
		}

		/// <summary>
		/// Completely rebuild this asset, including all geometry, meshes, colliders, materials, and textures.
		/// </summary>
		/// <returns>True if rebuild succeeded</returns>
		private bool InternalRebuild()
		{
			HEU_SessionBase session = GetAssetSession(true);
			if(session == null)
			{
				return false;
			}

			DeleteAllGeneratedData();

			Debug.Assert(_assetID == HEU_Defines.HEU_INVALID_NODE_ID, "Asset must be new or cleaned up! Missing call to CleanUpAsset?");
			Debug.Assert(_objectNodes.Count == 0, "Object list must be empty! Missing call to DeleteAllPersistentData?");

			// Load and cook the HDA
			if (_assetType == HEU_AssetType.TYPE_HDA)
			{
				HAPI_NodeId newAssetID = HEU_Defines.HEU_INVALID_NODE_ID;
				bool bResult = LoadAndCookHDA(session, out newAssetID, HEU_PluginSettings.CookTemplatedGeos);
				if (!bResult)
				{
					if (newAssetID != HEU_Defines.HEU_INVALID_NODE_ID)
					{
						DeleteAllGeneratedData();
					}
					return false;
				}

				_assetID = newAssetID;
			}
			else if (_assetType == HEU_AssetType.TYPE_CURVE)
			{
				HAPI_NodeId newAssetID = HEU_Defines.HEU_INVALID_NODE_ID;
				bool bResult = CreateAndCookCurveAsset(session, out newAssetID, HEU_PluginSettings.CookTemplatedGeos);
				if (!bResult)
				{
					if (newAssetID != HEU_Defines.HEU_INVALID_NODE_ID)
					{
						DeleteAllGeneratedData();
					}
					return false;
				}

				_assetID = newAssetID;
			}
			else if (_assetType == HEU_AssetType.TYPE_INPUT)
			{
				HAPI_NodeId newAssetID = HEU_Defines.HEU_INVALID_NODE_ID;
				bool bResult = CreateAndCookInputAsset(session, out newAssetID, HEU_PluginSettings.CookTemplatedGeos);
				if (!bResult)
				{
					if (newAssetID != HEU_Defines.HEU_INVALID_NODE_ID)
					{
						DeleteAllGeneratedData();
					}
					return false;
				}

				_assetID = newAssetID;
			}
			else
			{
				Debug.LogErrorFormat(HEU_Defines.HEU_NAME + ": Unsupported asset type {0}!", _assetType);
				return false;
			}

			session.GetNodeInfo(_assetID, ref _nodeInfo);
			session.GetAssetInfo(_assetID, ref _assetInfo);

			// Cache asset info
			_assetName = HEU_SessionManager.GetString(_assetInfo.nameSH, session);
			_assetOpName = HEU_SessionManager.GetString(_assetInfo.fullOpNameSH, session);
			_assetHelp = HEU_SessionManager.GetString(_assetInfo.helpTextSH, session);

			Debug.Log(HEU_Defines.HEU_NAME + ": Asset Loaded - ID: " + _assetInfo.nodeId + "\n" +
								"    Full Name: " + _assetOpName + "\n" +
								"    Version: " + HEU_SessionManager.GetString(_assetInfo.versionSH, session) + "\n" +
								"    Unique Node Id: " + _nodeInfo.uniqueHoudiniNodeId + "\n" +
								"    Internal Node Path: " + HEU_SessionManager.GetString(_nodeInfo.internalNodePathSH, session) + "\n" +
								"    Asset Library File: " + HEU_SessionManager.GetString(_assetInfo.filePathSH, session) + "\n");

			if (RootGameObject.name.Equals(HEU_Defines.HEU_DEFAULT_ASSET_NAME))
			{
				RootGameObject.name = _assetName;
			}

			// Add input connections
			CreateAssetInputs(session);

			// Build the parameters
			GenerateParameters(session);

			// TODO: handle duplication with geo & preset?

			// Create objects in this asset. It will create object nodes, geometry, and anything else required.
			if (!CreateObjects(session))
			{
				// Failed to create objects means that this asset is not valid
				Debug.LogErrorFormat(HEU_Defines.HEU_NAME + ": Failed to create objects for asset {0}", _assetName);
				DeleteAllGeneratedData();
				return false;
			}

			GenerateObjectsGeometry(session);

			GenerateInstances(session);

			GenerateHandles(session);

			// Upload transform. This should happen after generating outputs above.
			if (HEU_PluginSettings.PushUnityTransformToHoudini && PushTransformToHoudini)
			{
				UploadUnityTransform(session, false);
			}

			// TODO: Now process dependent assets
			//if (bCookDownStreamAssets)
			//{
			//	ProcessDependentAssets(bSerializationRecoveryOnly, bForceReconnect, bIsDuplication);
			//}

			NotifyInputNodesCookFinished();

			// This is required in order to flag to Unity that the scene data has changed. Otherwise saving the scene does not work.
			HEU_EditorUtility.MarkSceneDirty();

			DoPostCookWork(session);

			return true;
		}

		/// <summary>
		/// Cook this asset in Houdini, then handle the outcome.
		/// Cooking is done asynchrnously.
		/// </summary>
		/// <param name="bCheckParamsChanged">If true, then will only cook if parameters have changed.</param>
		/// <param name="bSkipCookCheck">If true, will check if cooking is enabled.</param>
		/// <returns>True if cooking started.</returns>
		private bool RecookAsync(bool bCheckParamsChanged, bool bSkipCookCheck, bool bUploadParameters)
		{
#if HEU_PROFILER_ON
			_cookStartTime = Time.realtimeSinceStartup;
#endif

			bool bStarted = InternalStartRecook(bCheckParamsChanged, bSkipCookCheck, bUploadParameters);
			if(!bStarted)
			{
				SetCookStatus(AssetCookStatus.NONE, AssetCookResult.ERRORED);
				ExecutePostCookCallbacks();
			}

			return bStarted;
		}

		/// <summary>
		/// Cook this asset in Houdini, then handle the outcome.
		/// Cooking is done synchronously so this will block until finished.
		/// </summary>
		/// <param name="bCheckParamsChanged">If true, then will only cook if parameters have changed.</param>
		/// <param name="bSkipCookCheck">If true, will check if cooking is enabled.</param>
		/// <param name = "bUploadParameters" > If true, will upload parameter values before cooking.</param>
		/// <returns>True if cooking was done.</returns>
		private bool RecookBlocking(bool bCheckParamsChanged, bool bSkipCookCheck, bool bUploadParameters)
		{
#if HEU_PROFILER_ON
			_cookStartTime = Time.realtimeSinceStartup;
#endif

			bool bStarted = InternalStartRecook(bCheckParamsChanged, bSkipCookCheck, bUploadParameters);
			if (!bStarted)
			{
				SetCookStatus(AssetCookStatus.NONE, AssetCookResult.ERRORED);
				ExecutePostCookCallbacks();
			}
			else
			{
				ProcessHoudiniCookStatus(false);
			}
			return bStarted;
		}

		/// <summary>
		/// Do any post-cook work
		/// </summary>
		private void DoPostCookWork(HEU_SessionBase session)
		{
			foreach(HEU_ObjectNode objNode in _objectNodes)
			{
				objNode.ProcessUnityScriptAttributes(session);
			}

			// Update the Editor UI
			if (_refreshUIDelegate != null)
			{
				_refreshUIDelegate();
			}
		}

		/// <summary>
		/// Invoke the callbacks after a cook.
		/// </summary>
		private void ExecutePostCookCallbacks()
		{
			if (_cookedEvent != null)
			{
				List<GameObject> outputObjects = new List<GameObject>();
				GetOutputGameObjects(outputObjects);
				bool bCookSuccess = (_lastCookResult == AssetCookResult.SUCCESS);
				_cookedEvent.Invoke(this, bCookSuccess, outputObjects);
			}
		}

		/// <summary>
		/// Start the cooking process.
		/// </summary>
		/// <param name="bCheckParamsChanged">If true, then will only cook if parameters have changed.</param>
		/// <param name="bSkipCookCheck">If true, will check if cooking is enabled.</param>
		/// <param name="bUploadParameters">If true, will upload parameter values before cooking.</param>
		/// <returns></returns>
		private bool InternalStartRecook(bool bCheckParamsChanged, bool bSkipCookCheck, bool bUploadParameters)
		{
			HEU_SessionBase session = GetAssetSession(true);
			if (session == null)
			{
				return false;
			}

			if (!bSkipCookCheck && !HEU_PluginSettings.CookingEnabled)
			{
				return false;
			}

			// A recook is called when the asset has already been created previously.
			// We have to determine if the asset is in a valid state, upload its state,
			// then cook, and find out what has changed.
			//Debug.Log(HEU_Defines.HEU_NAME + ": Recooking " + AssetName);

			bool bResult = false;
			_isCookingAssetReloaded = false;

			// Not checking if parameters have changed implies we update everything
			// TODO: consolidate bCheckParamsChanged and _bForceUpdate
			_bForceUpdate = !bCheckParamsChanged;

			if ((_assetID < 0) || !IsAssetValidInHoudini(session))
			{
				// This asset does not exist in Houdini session.
				// This can happen after loading a scene with a saved HDA.
				// We'll need to reload asset into Houdini, upload the parameter preset, then cook.

				// Load and cook the HDA
				if (_assetType == HEU_AssetType.TYPE_HDA)
				{
					// Asset ID isn't valid so do full rebuild
					if (!HasValidAssetPath())
					{
						Debug.LogError(HEU_Defines.HEU_NAME + ": Recook failed: asset needs to be reloaded but does not have valid asset path. Recommend instantiating new asset.");
						return false;
					}

					HAPI_NodeId newAssetID = HEU_Defines.HEU_INVALID_NODE_ID;
					bResult = LoadAndCookHDA(session, out newAssetID, HEU_PluginSettings.CookTemplatedGeos);
					if (!bResult)
					{
						// Asset load failed
						// TODO: notify user with popup?
						return false;
					}

					_assetID = newAssetID;
				}
				else if(_assetType == HEU_AssetType.TYPE_CURVE)
				{
					HAPI_NodeId newAssetID = HEU_Defines.HEU_INVALID_NODE_ID;
					bResult = CreateAndCookCurveAsset(session, out newAssetID, HEU_PluginSettings.CookTemplatedGeos);
					if (!bResult)
					{
						// Asset load failed
						// TODO: notify user with popup?
						return false;
					}

					_assetID = newAssetID;
				}
				else if(_assetType == HEU_AssetType.TYPE_INPUT)
				{
					HAPI_NodeId newAssetID = HEU_Defines.HEU_INVALID_NODE_ID;
					if(!CreateAndCookInputAsset(session, out newAssetID, HEU_PluginSettings.CookTemplatedGeos))
					{
						return false;
					}

					_assetID = newAssetID;
				}
				else
				{
					Debug.LogErrorFormat(HEU_Defines.HEU_NAME + ": Recook failed: unsupported asset type {0}!", _assetType);
					// TODO: notify user with popup?
					return false;
				}

				// Flag it to show that the asset was reloaded in Houdini, and therefore requires extra setup
				_isCookingAssetReloaded = true;

				// Force updating everything on asset reload
				_bForceUpdate = true;

				session.GetNodeInfo(_assetID, ref _nodeInfo);
				session.GetAssetInfo(_assetID, ref _assetInfo);

				// Cache asset info
				_assetName = HEU_SessionManager.GetString(_assetInfo.nameSH, session);
				_assetOpName = HEU_SessionManager.GetString(_assetInfo.fullOpNameSH, session);
				_assetHelp = HEU_SessionManager.GetString(_assetInfo.helpTextSH, session);

				// This makes sure to have upstream connected assets cooked in current session
				// so that the parameter upload that comes next should work properly.
				CookUpstreamConnections(session);

				// Upload parameter presets if exists
				UploadParameterPresetToHoudini(session);

				// Upload connected data for input node parameters. Required because after loading scene
				// Houdini wouldn't have the input geometry.
				UpdateParameterInputsToHoudini(session, _bForceUpdate);

				// Continue on with rest of recook...
			}

			// At this point, the asset exists in Houdini.
			// We will upload our parameters, cook (again), then handle any changes.

			// Upload this asset's Unity transform if it has changed
			// Note that uploading transforms before uploading parameters is important
			// since Houdini Engine will update the parameter values automatically.
			if (HEU_PluginSettings.PushUnityTransformToHoudini && PushTransformToHoudini)
			{
				UploadUnityTransform(session, !_isCookingAssetReloaded);
			}

			// Let's try to upload existing parameter values.
			// It might fail if the parameters have changed in the HDA since last loaded in Unity.
			if (_parameters != null && !_parameters.RequiresRegeneration && bUploadParameters)
			{
				Debug.Assert(_assetID == _parameters._nodeID, HEU_Defines.HEU_NAME + ": Our parameter object must have our asset ID.\n"
					+ "If this fails, something went wrong earlier and need to catch it!");

				// Do parameter modifiers first. These change number of parameters (eg. multiparm).
				// If there are no modifiers, we can upload any changes to the actual values.
				if (_parameters.HasModifiersPending())
				{
					_parameters.ProcessModifiers(session);
				}
				else
				{
					if (!_parameters.UploadValuesToHoudini(session, this, bCheckParamsChanged))
					{
						Debug.LogWarningFormat(HEU_Defines.HEU_NAME + ": Failed to upload parameter changes to Houdini for asset {0}", AssetName);
					}
				}
			}

			// Similarly for curves, upload their parameters if asset is not being reloaded
			if (!_isCookingAssetReloaded)
			{
				UploadCurves(session, bCheckParamsChanged);
			}

			// Upload attributes. For edit nodes, this will be a cumulative update. So if the source geo has
			// changed earlier in the graph, it will most likely be ignored here since the edit node has its own
			// version of the geo with custom attributes. The only way to resolve it would be to blow away the custom
			// attribute data (reset all edit node changes). Currently not enabled, though could be added as a 
			// button that invokes edit node's Reste All Changes.
			UploadAttributeValues(session);

			// Upload asset inputs
			UploadInputNodes(session, _bForceUpdate);

			// Triggers cook of the ouputs of the asset in Houdini.

			// Rebuild geometry if 
			//	-geoInfo has changed
			//	-no geometry / mesh
			//	-if material infos flag has changed is set

			// GetMaterialNodeIDsOnFaces has to be queried after every cook to see if material assignments have changed.
			// Also query each material individual (GetMaterialInfo) to see if its dirty and needs to be re-imported.
			// MaterialInfo::hasChanged -> render / extract texture/material (no re-fetch of material assignment or geo query)
			// GeoInfo::hasGeoChanged -> material assignments could have changed; existing materials have assignments but material
			//							itself might not have

			bResult = StartHoudiniCookNode(session);
			if (!bResult)
			{
				// Cooking failed.
				Debug.LogErrorFormat(HEU_Defines.HEU_NAME + ": Failed to cook asset {0}!", AssetName);
				return false;
			}

#if HEU_PROFILER_ON
			_hapiCookEndTime = Time.realtimeSinceStartup;
#endif

			return true;
		}

		private void SetCookStatus(AssetCookStatus status, AssetCookResult result)
		{
			_cookStatus = status;
			_lastCookResult = result;
		}

		public AssetCookStatus GetCookStatus()
		{
			return _cookStatus;
		}

		/// <summary>
		/// After cook has finished in Houdini, process the output and update/generate Unity side.
		/// </summary>
		private void ProcessPoskCook()
		{
#if HEU_PROFILER_ON
			_postCookStartTime = Time.realtimeSinceStartup;
#endif

			HEU_SessionBase session = GetAssetSession(false);
			if (session == null)
			{
				SetCookStatus(AssetCookStatus.NONE, _lastCookResult = AssetCookResult.ERRORED);
				return;
			}

			// Refresh our node and asset infos again just in case anything changed after cooking
			session.GetNodeInfo(_assetID, ref _nodeInfo);
			session.GetAssetInfo(_assetID, ref _assetInfo);

			// We will always regenerate parameters after cooking to make sure we're in sync.
			GenerateParameters(session);

			// Download & save the parameter preset
			DownloadParameterPresetFromHoudini(session);

			//Debug.LogFormat("Node Input Count: {0}", _nodeInfo.inputCount);
			//Debug.LogFormat("Asset Input Count: {0}", _assetInfo.geoInputCount);

			// Update the Houdini materials in use by this asset.
			// This should be done before update the objects as below.
			UpdateHoudiniMaterials(session);

			// Note that on HDA reload, we need to check all the objects to make sure
			// they have updated ObjectInfos.
			// _assetInfo.haveObjectsChanged is false on HDA reload so we check _bForceUpdate as well
			if (_assetInfo.haveObjectsChanged || _isCookingAssetReloaded || _bForceUpdate)
			{
				// Number of objects might have changed.
				// This gets latest object infos, adds and removes objects, then refreshes them
				UpdateAllObjectNodes(session);
			}
			else
			{
				// Go through each object and refresh its state in order to handle changes
				foreach (HEU_ObjectNode objNode in _objectNodes)
				{
					objNode.UpdateObject(session, _bForceUpdate);
				}
			}

			GenerateObjectsGeometry(session);

			GenerateInstances(session);

			GenerateHandles(session);

			// After all the objects have been processed, go through our materials list
			// and remove any unused materials.
			RemoveUnusedMaterials();

			// This forces the attribute editor to recache
			_toolsInfo._recacheRequired = true;

			// This is required in order to flag to Unity that the scene data has changed.
			// Otherwise saving the scene does not work.
			// Should we make this more specific by checking if there were any changes above?
			HEU_EditorUtility.MarkSceneDirty();

			DoPostCookWork(session);

			// Notify listeners that we've cooked!
			List<GameObject> outputObjects = new List<GameObject>();
			GetOutputGameObjects(outputObjects);
			if (_downstreamConnectionCookedEvent != null)
			{
				_downstreamConnectionCookedEvent.Invoke(this, true, outputObjects);
			}

			SetCookStatus(AssetCookStatus.NONE, AssetCookResult.SUCCESS);

#if HEU_PROFILER_ON
			Debug.LogFormat("RECOOK PROFILE:: TOTAL={0}, HAPI={1}, POST={2}", (Time.realtimeSinceStartup - _cookStartTime), (_hapiCookEndTime - _cookStartTime), (Time.realtimeSinceStartup - _postCookStartTime));
#endif
		}

		private bool StartHoudiniCookNode(HEU_SessionBase session)
		{
			bool bResult = session.CookNode(AssetID, HEU_PluginSettings.CookTemplatedGeos);
			if (bResult)
			{
				SetCookStatus(AssetCookStatus.COOKING, AssetCookResult.NONE);
			}
			return bResult;
		}

		private void ProcessHoudiniCookStatus(bool bAsync)
		{
			HAPI_State statusCode = HAPI_State.HAPI_STATE_STARTING_LOAD;

			HEU_SessionBase session = GetAssetSession(false);
			if(session == null)
			{
				Debug.LogWarning(HEU_Defines.HEU_NAME + ": No valid session for cooking!");
				SetCookStatus(AssetCookStatus.NONE, AssetCookResult.ERRORED);
			}
			else
			{
				bool bResult = true;
				do
				{
					bResult = session.GetStatus(HAPI_StatusType.HAPI_STATUS_COOK_STATE, out statusCode);
					if (bResult && (statusCode > HAPI_State.HAPI_STATE_MAX_READY_STATE))
					{
						// Still cooking. If async, we'll return, otherwise busy wait.
						if(bAsync)
						{
							return;
						}
					}
					else
					{
						break;
					}
				} while (bResult);

				// Check cook results for any errors
				if (statusCode == HAPI_State.HAPI_STATE_READY_WITH_FATAL_ERRORS)
				{
					string statusString = session.GetStatusString(HAPI_StatusType.HAPI_STATUS_COOK_RESULT, HAPI_StatusVerbosity.HAPI_STATUSVERBOSITY_ERRORS);
					Debug.LogError(string.Format(HEU_Defines.HEU_NAME + ": Cooking failed for asset: {0}\n{1}", AssetName, statusString));

					SetCookStatus(AssetCookStatus.NONE, AssetCookResult.ERRORED);
				}
				else
				{
					if (statusCode == HAPI_State.HAPI_STATE_READY_WITH_COOK_ERRORS)
					{
						// We should be able to continue even with these errors, but at least notify user.
						string statusString = session.GetStatusString(HAPI_StatusType.HAPI_STATUS_COOK_RESULT, HAPI_StatusVerbosity.HAPI_STATUSVERBOSITY_WARNINGS);
						Debug.LogWarning(string.Format(HEU_Defines.HEU_NAME + ": Cooking finished with some errors for asset: {0}\n{1}", AssetName, statusString));
					}
					else
					{
						//Debug.LogFormat(HEU_Defines.HEU_NAME + ": Cooking result {0} for asset: {1}", (HAPI_State)statusCode, AssetName);
					}

					SetCookStatus(AssetCookStatus.POSTCOOK, AssetCookResult.SUCCESS);

					ProcessPoskCook();
				}
			}

			// We do callbacks after everything to flag both success and error
			ExecutePostCookCallbacks();
		}

		/// <summary>
		/// Returns true if asset requires a recook.
		/// </summary>
		/// <returns>True if asset requires a recook.</returns>
		public bool DoesAssetRequireRecook()
		{
			if(_parameters.RequiresRegeneration || _parameters.HaveParametersChanged() || _parameters.HasModifiersPending())
			{
				return true;
			}

			// Check curves
			foreach(HEU_Curve curve in _curves)
			{
				if(curve.Parameters.HaveParametersChanged())
				{
					return true;
				}
			}

			foreach(HEU_InputNode inputNode in _inputNodes)
			{
				if (inputNode.InputType != HEU_InputNode.InputNodeType.PARAMETER && (inputNode.RequiresUpload || inputNode.HasInputNodeTransformChanged()))
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Deletes session only data. Does not delete persistent data as part of project.
		/// It deletes the asset node from Houdini session.
		/// </summary>
		public void DeleteSessionDataOnly()
		{
			if (_assetID != HEU_Defines.HEU_INVALID_NODE_ID)
			{
				//Debug.LogFormat(HEU_Defines.HEU_NAME + ": Deleting asset {0} in Houdini session.", _assetName);
				HEU_SessionBase session = GetAssetSession(false);
				if (session != null)
				{
					session.DeleteNode(_assetID);
				}
			}
		}

		/// <summary>
		/// Delete generated data used by this asset.
		/// </summary>
		public void DeleteAllGeneratedData()
		{
			if (_assetID != HEU_Defines.HEU_INVALID_NODE_ID)
			{
				//Debug.LogFormat(HEU_Defines.HEU_NAME + ": Deleting asset {0}'s persistent data.", _assetName);
			}

			DeleteSessionDataOnly();
			_assetID = HEU_Defines.HEU_INVALID_NODE_ID;

			// Clean up object nodes which in turns cleans up meshes.
			if (_objectNodes != null)
			{
				for(int i = 0; i < _objectNodes.Count; ++i)
				{
					if (_objectNodes[i] != null)
					{
						_objectNodes[i].DestroyAllData();
						HEU_GeneralUtility.DestroyImmediate(_objectNodes[i]);
					}
				}
				_objectNodes.Clear();
			}

			// The materials for this asset will be deleted when we delete the asset cache.
			// So we'll just clear the material cache without actually deleting them.
			ClearMaterialCache();

			// Clear out connection callbacks using parameter input nodes
			ClearAllUpstreamConnections();

			if (_parameters != null)
			{
				_parameters.CleanUp();
				_parameters = null;
			}

			CleanUpInputNodes();

			CleanUpHandles();
		}

		private void CleanUpInputNodes()
		{
			if (_inputNodes != null && _inputNodes.Count > 0)
			{
				HEU_SessionBase session = GetAssetSession(false);

				List<HEU_InputNode> tempNodes = new List<HEU_InputNode>();

				for(int i = 0; i < _inputNodes.Count; ++i)
				{
					// Only cleaning up connections as those are the ones this asset creates. The other types
					// are handled by those that created them (geo node, parameter).
					if (_inputNodes[i] != null && _inputNodes[i].InputType == HEU_InputNode.InputNodeType.CONNECTION)
					{
						tempNodes.Add(_inputNodes[i]);
					}
				}

				for (int i = 0; i < tempNodes.Count; ++i)
				{
					_inputNodes.Remove(tempNodes[i]);

					tempNodes[i].DestroyAllData(session);
					HEU_GeneralUtility.DestroyImmediate(tempNodes[i]);
				}
			}
		}

		public void DestroyGeneratedMeshData(bool bRegisterUndo)
		{
			if (_assetID != HEU_Defines.HEU_INVALID_NODE_ID)
			{
				Debug.LogFormat(HEU_Defines.HEU_NAME + ": Deleting asset {0}'s geometry!", _assetName);
			}

			// Clean up object nodes which in turns cleans up meshes.
			if (_objectNodes != null)
			{
				foreach (HEU_ObjectNode objectNode in _objectNodes)
				{
					objectNode.DestroyGeneratedMeshData(bRegisterUndo: bRegisterUndo);
				}
			}

			ClearMaterialCache();

			// TODO: fix up asset cache folder deletion and undo
			// Delete the asset cache
			if (!string.IsNullOrEmpty(_assetCacheFolderPath))
			{
				HEU_AssetDatabase.DeleteAssetCacheFolder(_assetCacheFolderPath);
				_assetCacheFolderPath = null;
			}
		}

		/// <summary>
		/// Generate all the parameters for this asset based on information from HAPI.
		/// </summary>
		private void GenerateParameters(HEU_SessionBase session)
		{
			//Debug.Log(HEU_Defines.HEU_NAME + ": Generating parameters!");

#if HEU_PROFILER_ON
			float parameterGenStartTime = Time.realtimeSinceStartup;
#endif

			// Store the previous folder and input node parameters so we can transfer them over to new parameters
			Dictionary<string, HEU_ParameterData> previousParamFolders = new Dictionary<string, HEU_ParameterData>();
			Dictionary<string, HEU_InputNode> previousParamInputNodes = new Dictionary<string, HEU_InputNode>();

			if (_parameters != null)
			{
				_parameters.GetParameterDataForUIRestore(previousParamFolders, previousParamInputNodes);

				// If parameter exists, just clean it up. Don't nullify or destroy it as it loses Undo history.
				_parameters.CleanUp();
			}
			else
			{
				_parameters = ScriptableObject.CreateInstance<HEU_Parameters>();
			}

			bool bResult = _parameters.Initialize(session, _assetID, ref _nodeInfo, previousParamFolders, previousParamInputNodes, this);
			if (!bResult)
			{
				Debug.LogWarningFormat(HEU_Defines.HEU_NAME + ": Parameter generate failed for asset {0}.", AssetName);
				_parameters.CleanUp();
			}

#if HEU_PROFILER_ON
			Debug.LogFormat("PARAMETERS GENERATION TIME:: {0}", (Time.realtimeSinceStartup - parameterGenStartTime));
#endif
		}

		private void DownloadParameterPresetFromHoudini(HEU_SessionBase session)
		{
			if(HEU_EditorUtility.IsEditorPlaying())
			{
				return;
			}

			if(_parameters != null)
			{
				_parameters.DownloadPresetData(session);
			}

			// Note that we aren't downloading presets for our curves here as thats done after
			// the curve is re-generated.
		}

		private void UploadParameterPresetToHoudini(HEU_SessionBase session)
		{
			if(_parameters != null)
			{
				// Make sure that the parameters object has the latest node ID of our asset
				_parameters._nodeID = _assetID;

				_parameters.UploadPresetData(session);
			}

			List<HEU_Curve> curves = GetCurves();
			foreach (HEU_Curve curve in curves)
			{
				HEU_Parameters curveParams = curve.Parameters;
				if(curveParams != null)
				{
					// See note in HEU_Curve::UploadParameterPreset
					curve.SetUploadParameterPreset(true);
				}
			}
		}

		private void UpdateParameterInputsToHoudini(HEU_SessionBase session, bool bForceUpdate)
		{
			if (_parameters != null)
			{
				_parameters.UploadParameterInputs(session, this, bForceUpdate);
			}
		}

		private string GetAssetPath()
		{
			if(_assetFileObject != null)
			{
				return HEU_AssetDatabase.GetAssetPath(_assetFileObject);
			}
			return null;
		}

		/// <summary>
		/// Loads this asset in Houdini and cooks it.
		/// </summary>
		/// <param name="newAssetID">Will contain this asset's valid asset ID after loading</param>
		/// <param name="bCookTemplatedGeos">Whether to cook templated geos</param>
		/// <returns>Returns true if cooking succeeded</returns>
		private bool LoadAndCookHDA(HEU_SessionBase session, out HAPI_NodeId newAssetID, bool bCookTemplatedGeos)
		{
			newAssetID = HEU_Defines.HEU_INVALID_NODE_ID;

			if (_assetType != HEU_AssetType.TYPE_HDA)
			{
				throw new HEU_HoudiniEngineError(string.Format("Trying to build asset type: {0}. Expected type: {1}.", _assetType, HEU_AssetType.TYPE_HDA));
			}

			// Load the file
			// First try using assigned object if its valid. Otherwise use the set path or name.
			string validAssetPath = HEU_HAPIUtility.LocateValidFilePath(_assetFileObject);
			if(string.IsNullOrEmpty(validAssetPath))
			{
				validAssetPath = HEU_HAPIUtility.LocateValidFilePath(OwnerGameObject.name, _assetPath);
			}

			if (!string.IsNullOrEmpty(validAssetPath))
			{
				_assetPath = validAssetPath;

				// Set object reference if it hasn't been set. In future, user can re-link this via UI or code if reference is ever lost.
				if (_assetFileObject == null)
				{
					_assetFileObject = HEU_AssetDatabase.LoadAssetAtPath(_assetPath, typeof(UnityEngine.Object));
				}

				HAPI_AssetLibraryId libraryID = 0;
				bool bResult = session.LoadAssetLibraryFromFile(_assetPath, false, out libraryID);
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

				string[] assetNames = new string[assetCount];
				for (int i = 0; i < assetCount; ++i)
				{
					assetNames[i] = HEU_SessionManager.GetString(assetNameLengths[i], session);
				}

				// Create top level node. Note that CreateNode will cook the node if HAPI was initialized with threaded cook setting on.
				string topNodeName = assetNames[0];
				bResult = session.CreateNode(-1, topNodeName, "", false, out newAssetID);
				if (!bResult)
				{
					return false;
				}

				// Make sure cooking is successfull before proceeding. Any licensing or file data issues will be caught here.
				if (!HEU_HAPIUtility.ProcessHoudiniCookStatus(session, AssetName))
				{
					return false;
				}

				// In case the cooking wasn't done previously, force it now.
				bResult = HEU_HAPIUtility.CookNodeInHoudini(session, newAssetID, bCookTemplatedGeos, AssetName);
				if (!bResult)
				{
					// When cook failed, delete the node created earlier
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
							"asset definitions. You might need to load other (dependent) HDAs first.", "Ok");

						// Change to allowing the asset to continue to load, as long as the user is notified.
						//return false;
					}
				}
			}

			return true;
		}

		private bool CreateAndCookCurveAsset(HEU_SessionBase session, out HAPI_NodeId newAssetID, bool bCookTemplatedGeos)
		{
			newAssetID = HEU_Defines.HEU_INVALID_NODE_ID;
			if (!session.CreateNode(HEU_Defines.HEU_INVALID_NODE_ID, "SOP/curve", "Curve", true, out newAssetID))
			{
				return false;
			}

			// Make sure cooking is successfull before proceeding. Any licensing or file data issues will be caught here.
			if (!HEU_HAPIUtility.ProcessHoudiniCookStatus(session, AssetName))
			{
				return false;
			}

			// In case the cooking wasn't done previously, force it now.
			bool bResult = HEU_HAPIUtility.CookNodeInHoudini(session, newAssetID, bCookTemplatedGeos, AssetName);
			if (!bResult)
			{
				// When cook failed, deleted the node created earlier
				session.DeleteNode(_assetID);
				newAssetID = HEU_Defines.HEU_INVALID_NODE_ID;
				return false;
			}

			return true;
		}

		private bool CreateAndCookInputAsset(HEU_SessionBase session, out HAPI_NodeId newAssetID, bool bCookTemplatedGeos)
		{
			newAssetID = HEU_Defines.HEU_INVALID_NODE_ID;
			if(!session.CreateInputNode(out newAssetID, null))
			{
				return false;
			}

			// Make sure cooking is successfull before proceeding. Any licensing or file data issues will be caught here.
			if (!HEU_HAPIUtility.ProcessHoudiniCookStatus(session, ""))
			{
				return false;
			}

			// In case the cooking wasn't done previously, force it now.
			bool bResult = HEU_HAPIUtility.CookNodeInHoudini(session, newAssetID, bCookTemplatedGeos, "");
			if (!bResult)
			{
				// When cook failed, deleted the node created earlier
				session.DeleteNode(_assetID);
				newAssetID = HEU_Defines.HEU_INVALID_NODE_ID;
				return false;
			}

			// After cooking, set an empty partinfo
			HAPI_GeoInfo inputGeoInfo = new HAPI_GeoInfo();
			if (!session.GetDisplayGeoInfo(newAssetID, ref inputGeoInfo))
			{
				return false;
			}

			HAPI_PartInfo newPart = new HAPI_PartInfo();
			newPart.init();
			newPart.id = 0;
			newPart.vertexCount = 0;
			newPart.faceCount = 0;
			newPart.pointCount = 0;
			// TODO: always set to mesh type?
			newPart.type = HAPI_PartType.HAPI_PARTTYPE_MESH;

			if (!session.SetPartInfo(inputGeoInfo.nodeId, 0, ref newPart))
			{
				Debug.LogErrorFormat(HEU_Defines.HEU_NAME + ": Failed to set partinfo for input node!");
				return false;
			}

			return true;
		}

		/// <summary>
		/// Create and setup asset input nodes.
		/// </summary>
		/// <param name="session"></param>
		private void CreateAssetInputs(HEU_SessionBase session)
		{
			if(_assetType == HEU_AssetType.TYPE_INPUT || _assetType == HEU_AssetType.TYPE_CURVE)
			{
				// Not creating input nodes for purely Input or Curve type assets.
				// This is because an input type asset creates its own input nodes via its geo node.
				return;
			}

			int numInputs = _assetInfo.geoInputCount;

			if(_inputNodes.Count == numInputs)
			{
				// Nothing to do if size is same
				return;
			}

			if(numInputs == 0)
			{
				CleanUpInputNodes();
				return;
			}

			if(_nodeInfo.type == HAPI_NodeType.HAPI_NODETYPE_OBJ && _assetInfo.transformInputCount > 0)
			{
				// TODO: handle upstream transform connections for objects
			}

			if(numInputs > _inputNodes.Count)
			{
				// Increased inputs

				int startInput = _inputNodes.Count;
				for(int i = startInput; i < numInputs; ++i)
				{
					HAPI_StringHandle inputNameHandle = HEU_Defines.HEU_INVALID_NODE_ID;
					if (session.GetNodeInputName(_assetID, i, out inputNameHandle))
					{
						string inputName = HEU_SessionManager.GetString(inputNameHandle, session);
						HEU_InputNode inputNode = HEU_InputNode.CreateSetupInput(_assetID, i, inputName, HEU_InputNode.InputNodeType.CONNECTION, this);
						if(inputNode != null)
						{
							AddInputNode(inputNode);
						}
					}
					else
					{
						Debug.LogWarningFormat("Invalid name found on input node at index {0}", i);
					}
				}
			}
			else
			{
				// Decreased inputs

				while(_inputNodes.Count > numInputs)
				{
					HEU_InputNode inputNode = _inputNodes[_inputNodes.Count - 1];
					RemoveInputNode(inputNode);

					if (inputNode != null)
					{
						inputNode.DestroyAllData(session);
						HEU_GeneralUtility.DestroyImmediate(inputNode);
					}
				}
			}

			_showInputNodesSection = (_assetType == HEU_AssetType.TYPE_INPUT) || (numInputs > 0);
		}

		private void UploadCurves(HEU_SessionBase session, bool bCheckParamsChanged)
		{
			foreach (HEU_Curve curve in _curves)
			{
				if (curve.IsEditable())
				{
					HEU_Parameters curveParameters = curve.Parameters;
					if (curveParameters != null)
					{
						curveParameters.UploadValuesToHoudini(session, this, bCheckParamsChanged);
					}
				}
			}
		}

		private void UploadAttributeValues(HEU_SessionBase session)
		{
			// Normally only the attribute stores that are dirty will be uploaded to Houdini.
			// But if _toolsInfo._alwaysCookUpstream is true, we will upload all attributes
			// if there is at least one of them that is dirty. This is to handle case where
			// multiple editable nodes are being edited. In this case, each one will need
			// to have its modifications re-uploaded as each node will need to recook its
			// upstream inputs before doing so.

			bool bForceUpload = false;
			if (_toolsInfo._alwaysCookUpstream)
			{
				foreach (HEU_AttributesStore attributeStore in _attributeStores)
				{
					if (attributeStore.HasDirtyAttributes())
					{
						bForceUpload = true;
						break;
					}
				}
			}

			foreach (HEU_AttributesStore attributeStore in _attributeStores)
			{
				if(bForceUpload || attributeStore.HasDirtyAttributes())
				{
					if(_toolsInfo._alwaysCookUpstream)
					{
						attributeStore.RefreshUpstreamInputs(session);
					}

					attributeStore.SyncDirtyAttributesToHoudini(session);
				}
			}
		}

		private void UploadInputNodes(HEU_SessionBase session, bool bForceUpdate)
		{
			foreach (HEU_InputNode inputNode in _inputNodes)
			{
				// Upload all but parameter types, as those are taken care of in the parameter update
				if(inputNode.InputType != HEU_InputNode.InputNodeType.PARAMETER && (bForceUpdate || inputNode.RequiresUpload || inputNode.HasInputNodeTransformChanged()))
				{
					if(bForceUpdate)
					{
						inputNode.ResetConnectionForForceUpdate(session);
					}

					inputNode.UploadInput(session);
				}
			}
		}

		public bool HasInputNodeTransformChanged()
		{
			foreach (HEU_InputNode inputNode in _inputNodes)
			{
				if (inputNode.HasInputNodeTransformChanged())
				{
					return true;
				}
			}
			return false;
		}

		private void NotifyInputNodesCookFinished()
		{
			foreach (HEU_InputNode inputNode in _inputNodes)
			{
				if (inputNode.RequiresCook)
				{
					inputNode.RequiresCook = false;
				}
			}
		}

		/// <summary>
		/// Creates object nodes for this asset.
		/// In turn, geo nodes and parts will be created as well.
		/// </summary>
		/// <returns>True if successfully created all the asset's objects and their data</returns>
		private bool CreateObjects(HEU_SessionBase session)
		{
			Debug.Assert(_objectNodes.Count == 0, HEU_Defines.HEU_NAME + ": Object list must be empty!");

			// Fill in object infos and transforms based on node type and number of child objects
			HAPI_ObjectInfo[] objectInfos = null;
			HAPI_Transform[] objectTransforms = null;

			if (!GetObjectInfos(session, out objectInfos, out objectTransforms))
			{
				return false;
			}

			Debug.Assert(objectInfos.Length == objectTransforms.Length, HEU_Defines.HEU_NAME + ": Object info and object transform array mismatch!");

			// Create object nodes
			int numObjects = objectInfos.Length;
			for (int i = 0; i < numObjects; ++i)
			{
				_objectNodes.Add(CreateObjectNode(session, ref objectInfos[i], ref objectTransforms[i]));
			}

			return true;
		}

		/// <summary>
		/// Gets the object infos and transforms for this asset.
		/// </summary>
		/// <param name="objectInfos">Array of retrieved object infos</param>
		/// <param name="objectTransforms">Array of retrieved object transforms</param>
		/// <returns>True if succesfully retrieved object infos and transforms</returns>
		private bool GetObjectInfos(HEU_SessionBase session, out HAPI_ObjectInfo[] objectInfos, out HAPI_Transform[] objectTransforms)
		{
			objectInfos = new HAPI_ObjectInfo[0];
			objectTransforms = new HAPI_Transform[0];

			if (_nodeInfo.type == HAPI_NodeType.HAPI_NODETYPE_SOP)
			{
				// For SOP assets, we use the parent IDs to get the object info and geo info

				objectInfos = new HAPI_ObjectInfo[1];
				if (!session.GetObjectInfo(_nodeInfo.parentId, ref objectInfos[0]))
				{
					return false;
				}

				// Identity transform will be used for SOP assets, so not querying transform
				objectTransforms = new HAPI_Transform[1];
				objectTransforms[0] = new HAPI_Transform(true);
			}
			else if (_nodeInfo.type == HAPI_NodeType.HAPI_NODETYPE_OBJ)
			{
				int objectCount = 0;
				if (!session.ComposeObjectList(_assetID, out objectCount))
				{
					return false;
				}

				if (objectCount <= 0)
				{
					// Since this asset is an object type and has 0 object as children, we use the object itself

					objectInfos = new HAPI_ObjectInfo[1];
					if (!session.GetObjectInfo(_nodeInfo.id, ref objectInfos[0]))
					{
						return false;
					}

					// Identity transform will be used for single object assets, so not querying transform
					objectTransforms = new HAPI_Transform[1];
					objectTransforms[0] = new HAPI_Transform(true);
				}
				else
				{
					// This object has children, so use GetComposedObjectList to get list of HAPI_ObjectInfos

					objectInfos = new HAPI_ObjectInfo[objectCount];
					if (!session.GetComposedObjectList(_nodeInfo.parentId, objectInfos, 0, objectCount))
					{
						return false;
					}

					// Now get the object transforms
					objectTransforms = new HAPI_Transform[objectCount];
					if (!HEU_SessionManager.GetComposedObjectTransformsMemorySafe(session, _nodeInfo.parentId, HAPI_RSTOrder.HAPI_SRT, objectTransforms, 0, objectCount))
					{
						return false;
					}
				}
			}
			else
			{
				Debug.LogWarningFormat(HEU_Defines.HEU_NAME + ": Unsupported node type {0}", _nodeInfo.type);
				return false;
			}

			return true;
		}

		/// <summary>
		/// Synchronizes all local objects with Houdini session.
		/// Creates new objects, and removes old objects no longer in use.
		/// Refreshes each object to make their its internal state is
		/// synchronized.
		/// </summary>
		/// <returns>True if any changes were applied</returns>
		private void UpdateAllObjectNodes(HEU_SessionBase session)
		{
			// Fill in latest object infos and transforms based on node type and number of child objects
			HAPI_ObjectInfo[] objectInfos = null;
			HAPI_Transform[] objectTransforms = null;

			if (!GetObjectInfos(session, out objectInfos, out objectTransforms))
			{
				return;
			}

			// We need to go through the new list of object infos and 
			// check against our internal state. 
			// For new object infos, add new object nodes. 
			// Remove any unused object nodes.
			// Then refresh all object nodes.

			List<HEU_ObjectNode> newObjectNodes = new List<HEU_ObjectNode>();

			int numObjNodes = _objectNodes.Count;
			int numObjInfos = objectInfos.Length;
			for (int infoIndex = 0; infoIndex < numObjInfos; ++infoIndex)
			{
				// Find object
				bool bFound = false;

				for (int nodeIndex = 0; nodeIndex < numObjNodes; ++nodeIndex)
				{
					string objName = HEU_SessionManager.GetString(objectInfos[infoIndex].nameSH, session);
					if(objName.Equals(_objectNodes[nodeIndex].ObjectName))
					{
						// Update object info, add to new list
						_objectNodes[nodeIndex].SetObjectInfo(objectInfos[infoIndex]);
						newObjectNodes.Add(_objectNodes[nodeIndex]);
						bFound = true;
						break;
					}
				}

				if (!bFound)
				{
					// New object
					newObjectNodes.Add(CreateObjectNode(session, ref objectInfos[infoIndex], ref objectTransforms[infoIndex]));
				}
			}

			// Go through _objectNodes and remove any nodes not in new list
			numObjNodes = _objectNodes.Count;
			if (numObjNodes > 0)
			{
				for (int i = 0; i < numObjNodes; ++i)
				{
					if (!newObjectNodes.Contains(_objectNodes[i]))
					{
						_objectNodes[i].DestroyAllData();
						HEU_GeneralUtility.DestroyImmediate(_objectNodes[i]);
					}
				}
				_objectNodes.Clear();
			}

			//Debug.LogFormat(HEU_Defines.HEU_NAME + ": Replacing {0} old objects with {1} new objects!", numObjNodes, newObjectNodes.Count);

			// Update to new list
			_objectNodes = newObjectNodes;

			// Now refresh all object nodes
			foreach (HEU_ObjectNode objNode in _objectNodes)
			{
				objNode.UpdateObject(session, true);
			}
		}

		private HEU_ObjectNode CreateObjectNode(HEU_SessionBase session, ref HAPI_ObjectInfo objectInfo, ref HAPI_Transform objectTranform)
		{
			HEU_ObjectNode objectNode = ScriptableObject.CreateInstance<HEU_ObjectNode>();
			objectNode.Initialize(session, objectInfo, objectTranform, this);
			return objectNode;
		}

		/// <summary>
		/// Generate geometry (mesh, curves, terrain) for all object nodes.
		/// </summary>
		/// <param name="session">Current session</param>
		private void GenerateObjectsGeometry(HEU_SessionBase session)
		{
			foreach (HEU_ObjectNode objNode in _objectNodes)
			{
				objNode.GenerateGeometry(session);
			}
		}

		/// <summary>
		/// Generate instances for all object nodes.
		/// </summary>
		/// <param name="session"></param>
		private void GenerateInstances(HEU_SessionBase session)
		{
			// Instancing - process part instances first, then do object instances.
			// This assures that if objects being instanced have all their parts completed.

			foreach (HEU_ObjectNode objNode in _objectNodes)
			{
				objNode.GeneratePartInstances(session);
			}

			foreach (HEU_ObjectNode objNode in _objectNodes)
			{
				if (objNode.IsInstancer())
				{
					objNode.GenerateObjectInstances(session);
				}
			}
		}

		private void GenerateHandles(HEU_SessionBase session)
		{
			// This will get us an updated list of handles in this asset
			List<HEU_Handle> newHandles = HEU_GeneralUtility.FindOrGenerateHandles(session, ref _assetInfo, _assetID, _assetName, _parameters, _handles);

			// Clean up handles not in new list
			int numHandles = _handles.Count;
			for(int i = 0; i < numHandles; ++i)
			{
				if (!newHandles.Contains(_handles[i]))
				{
					_handles[i].CleanUp();
					HEU_GeneralUtility.DestroyImmediate(_handles[i]);
					_handles[i] = null;
				}
			}
			_handles.Clear();

			_handles = newHandles;
		}

		public void CleanUpHandles()
		{
			if (_handles != null)
			{
				for (int i = 0; i < _handles.Count; ++i)
				{
					_handles[i].CleanUp();
					HEU_GeneralUtility.DestroyImmediate(_handles[i]);
					_handles[i] = null;
				}
				_handles.Clear();
			}
		}

		public HEU_Handle GetHandleByName(string handleName)
		{
			foreach(HEU_Handle handle in _handles)
			{
				if(handle.HandleName.Equals(handleName))
				{
					return handle;
				}
			}
			return null;
		}

		public List<HEU_Handle> GetHandles()
		{
			return _handles;
		}

		public int NumHandles()
		{
			return _handles != null ? _handles.Count : 0;
		}

		/// <summary>
		/// Returns the given object's (OBJ) transform.
		/// The returned transform is based on what type of asset this is (SOP, OBJ with and
		/// without children)
		/// </summary>
		/// <param name="objectID">The ID of the object to query</param>
		/// <returns>A transform for this object</returns>
		public HAPI_Transform GetObjectTransform(HEU_SessionBase session, HAPI_NodeId objectID)
		{
			if (_nodeInfo.type == HAPI_NodeType.HAPI_NODETYPE_SOP)
			{
				return new HAPI_Transform(true);
			}
			else if (_nodeInfo.type == HAPI_NodeType.HAPI_NODETYPE_OBJ)
			{
				int objectCount = 0;
				if (!session.ComposeObjectList(AssetID, out objectCount))
				{
					return new HAPI_Transform(true);
				}

				if (objectCount <= 0)
				{
					return new HAPI_Transform(true);
				}
				else
				{
					HAPI_Transform hapiTransform = new HAPI_Transform();
					session.GetObjectTransform(objectID, AssetID, HAPI_RSTOrder.HAPI_SRT, ref hapiTransform);
					if (Mathf.Approximately(0f, hapiTransform.scale[0]) || Mathf.Approximately(0f, hapiTransform.scale[1]) || Mathf.Approximately(0f, hapiTransform.scale[2]))
					{
						Debug.LogWarning(string.Format(HEU_Defines.HEU_NAME + ": Object id {0} for asset {1} has scale components with 0 values!", objectID, AssetName));
					}
					else
					{
						return hapiTransform;
					}
				}
			}
			return new HAPI_Transform(true);
		}

		/// <summary>
		/// Returns the object node (OBJ) with specified ID
		/// </summary>
		/// <param name="objId">The object ID to match</param>
		/// <returns>Object node with specified ID</returns>
		public HEU_ObjectNode GetObjectWithID(HAPI_NodeId objId)
		{
			int numObjects = _objectNodes.Count;
			for (int i = 0; i < numObjects; ++i)
			{
				if (_objectNodes[i].ObjectID == objId)
				{
					return _objectNodes[i];
				}
			}
			return null;
		}

		private void InvokeBakedEvent(bool bSuccess, List<GameObject> outputObjects)
		{
			if (_bakedEvent != null)
			{
				_bakedEvent.Invoke(this, bSuccess, outputObjects);
			}
		}

		private void ProcessDependentAssets(bool bSerializationRecoveryOnly, bool bForceReconnect, bool isDuplication)
		{
			// TODO: build downstream assets
			Debug.Log(HEU_Defines.HEU_NAME + ": TODO: ProcessDependentAssets not implemented!");
		}

		/// <summary>
		/// Return a clone of this asset. The returned object might be a single
		/// gameobject containing relevant components such as mesh, collider, materials, and textures.
		/// It could also be a root object with several children underneath corresponding to an 
		/// asset with multiple objects and/or instances.
		/// </summary>
		/// <param name="bakedAssetPath">Reference to the new clone's asset path, or empty if not filled in.</param>
		/// <param name="bWriteMeshesToAssetDatabase">Whether to write meshes to persistant storage (asset database)</param>
		/// <returns>The new gameobject containing the cloned data</returns>
		private GameObject CloneAssetWithoutHDA(ref string bakedAssetPath, bool bWriteMeshesToAssetDatabase, bool bReconnectPrefabInstances)
		{
			GameObject newRoot = null;

			if (_rootGameObject == null)
			{
				Debug.LogErrorFormat("{0}: Unable to bake due to no HEU_HoudiniAssetRoot found!", HEU_Defines.HEU_NAME);
				return newRoot;
			}

			// If we're storing meshes in Asset Database, then we need  to create an asset object to store inside.
			UnityEngine.Object newAssetDBObject = null;
			string newAssetDBObjectFileName = HEU_AssetDatabase.AppendMeshesAssetFileName(_assetName);

			Transform rootTransform = _rootGameObject.transform;
			int numCreatedObjects = 0;

			// First get a list of clonable parts of the asset.
			// Then copy each part's gameobject, and place it under a common root if there are multiple objects. 
			// If just a single object, no separate root needed.

			// As we find meshes, we add to a map. The map helps track whether we already created
			// unique copy that can be shared.
			Dictionary<Mesh, Mesh> sourceToTargetMeshMap = new Dictionary<Mesh, Mesh>();

			List<HEU_PartData> clonableParts = new List<HEU_PartData>();
			GetClonableParts(clonableParts);

			if (clonableParts.Count > 0)
			{
				Transform newRootTransform = null;

				// Children go under a common root
				if (clonableParts.Count > 1)
				{
					newRoot = new GameObject();
					newRootTransform = newRoot.transform;
					HEU_GeneralUtility.CopyWorldTransformValues(rootTransform, newRootTransform);
				}

				foreach (HEU_PartData clonePart in clonableParts)
				{
					GameObject newChildGO = clonePart.BakePartToNewGameObject(newRootTransform, bWriteMeshesToAssetDatabase, ref bakedAssetPath, sourceToTargetMeshMap, ref newAssetDBObject, newAssetDBObjectFileName, bReconnectPrefabInstances);

					// In case of only 1 object being cloned, we'll set that as the newRoot
					if (newChildGO != null)
					{
						if (newRoot == null)
						{
							newRoot = newChildGO;
							newRootTransform = newRoot.transform;
							HEU_GeneralUtility.CopyWorldTransformValues(rootTransform, newRootTransform);
						}

						numCreatedObjects++;
					}
				}

				HEU_AssetDatabase.SaveAndRefreshDatabase();
			}

			if (newRoot != null)
			{
				if (numCreatedObjects != 0)
				{
					newRoot.name = _assetName + HEU_Defines.HEU_BAKED_HDA;
				}
				else
				{
					// Delete the root as nothing was generated
					HEU_GeneralUtility.DestroyImmediate(newRoot);
				}
			}

			if(numCreatedObjects == 0)
			{
				Debug.LogFormat("Nothing to bake as no geometry available!");
			}

			return newRoot;
		}

		/// <summary>
		/// Creates a prefab of this asset, without Houdini Engine data.
		/// </summary>
		public void BakeToNewPrefab()
		{
			// This creates a temporary clone of the asset without the HDA data
			// in the scene, then creates a prefab of the cloned object.

			string bakedAssetPath = null;
			bool bWriteMeshesToAssetDatabase = true;
			bool bReconnectPrefabInstances = false;
			GameObject newClonedRoot = CloneAssetWithoutHDA(ref bakedAssetPath, bWriteMeshesToAssetDatabase, bReconnectPrefabInstances);
			if (newClonedRoot != null)
			{
				try
				{
					if (string.IsNullOrEmpty(bakedAssetPath))
					{
						// Need to create the baked folder to store the prefab
						bakedAssetPath = HEU_AssetDatabase.CreateUniqueBakePath(_assetName);
					}

					string prefabPath = HEU_AssetDatabase.AppendPrefabPath(bakedAssetPath, _assetName);
					GameObject prefabGO = HEU_EditorUtility.CreatePrefab(prefabPath, newClonedRoot);
					if(prefabGO != null)
					{
						HEU_EditorUtility.SelectObject(prefabGO);

						InvokeBakedEvent(true, new List<GameObject>() { prefabGO });
					}
				}
				finally
				{
					// Don't need the new object anymore since its just prefab that's required
					HEU_GeneralUtility.DestroyImmediate(newClonedRoot);
				}
			}
		}

		/// <summary>
		/// Create a copy of this asset, without Houdini Engine data.
		/// </summary>
		public void BakeToNewStandalone()
		{
			string bakedAssetPath = null;

			// Make sure to write mesh to database because otherwise if user tries to make prefab after, it fails to create mesh.
			bool bWriteMeshesToAssetDatabase = true;
			bool bReconnectPrefabInstances = true;

			GameObject newClonedRoot = CloneAssetWithoutHDA(ref bakedAssetPath, bWriteMeshesToAssetDatabase, bReconnectPrefabInstances);
			if(newClonedRoot != null)
			{
				HEU_EditorUtility.SelectObject(newClonedRoot);

				InvokeBakedEvent(true, new List<GameObject>() { newClonedRoot });
			}
		}

		/// <summary>
		/// Bake out to an existing prefab, replacing its contents, including
		/// its persistance files like materials, textures, and meshes.
		/// </summary>
		/// <param name="bakeTargetGO">Must be the original prefab (ie. not an instance)</param>
		public void BakeToExistingPrefab(GameObject bakeTargetGO)
		{
			if(!HEU_EditorUtility.IsPrefabOriginal(bakeTargetGO))
			{
				Debug.LogErrorFormat("Unable to bake to existing prefab as specified object is not the original prefab!");
				return;
			}

			if(bakeTargetGO == this.gameObject || bakeTargetGO.GetComponent<HEU_HoudiniAssetRoot>() != null)
			{
				Debug.LogErrorFormat("Baking to a HoudiniAssetRoot gameobject is not supported!");
				return;
			}

			// Since the prefab would have persistent files on disk, we'll need to get
			// the existing prefab's asset folder, and delete relevant subfolders
			// such as: Materials, Textures, Meshes
			string existingPrefabFolder = HEU_AssetDatabase.GetAssetPath(bakeTargetGO);
			if (!string.IsNullOrEmpty(existingPrefabFolder))
			{
				existingPrefabFolder = HEU_Platform.GetFolderPath(existingPrefabFolder);
				existingPrefabFolder = HEU_Platform.TrimLastDirectorySeparator(existingPrefabFolder);

				string[] subFolders = HEU_AssetDatabase.GetAssetSubFolders();
				foreach(string subfolder in subFolders)
				{
					string folderPath = HEU_Platform.BuildPath(existingPrefabFolder, subfolder);
					HEU_AssetDatabase.DeleteAssetCacheFolder(folderPath);
				}
			}

			// Replace the specified prefab with a new cloned gameobject
			string bakedAssetPath = existingPrefabFolder;
			bool bWriteMeshesToAssetDatabase = true;
			bool bReconnectPrefabInstances = false;
			GameObject newClonedRoot = CloneAssetWithoutHDA(ref bakedAssetPath, bWriteMeshesToAssetDatabase, bReconnectPrefabInstances);
			if (newClonedRoot != null)
			{
				try
				{
					if (string.IsNullOrEmpty(bakedAssetPath))
					{
						// Need to create the baked folder to store the prefab
						bakedAssetPath = HEU_AssetDatabase.CreateUniqueBakePath(_assetName);
					}

					// Note using ReplacePrefabOptions.ReplaceNameBased will keep local transform values and other changes on instances.
					HEU_EditorUtility.ReplacePrefab(newClonedRoot, bakeTargetGO, HEU_EditorUtility.HEU_ReplacePrefabOptions.ReplaceNameBased);

					InvokeBakedEvent(true, new List<GameObject>() { bakeTargetGO });
				}
				finally
				{
					// Don't need the new object since its just prefab that's required
					HEU_GeneralUtility.DestroyImmediate(newClonedRoot);
				}
			}
		}

		/// <summary>
		/// Bake to an existing standalone gameobject. It will remove and replace existing
		/// Houdini Engine properties such as meshes, colliders, materials, and textures.
		/// </summary>
		/// <param name="bakeTargetGO">The target gameobject to bake out to</param>
		public void BakeToExistingStandalone(GameObject bakeTargetGO)
		{
			if(bakeTargetGO == this.gameObject || bakeTargetGO.GetComponent<HEU_HoudiniAssetRoot>() != null)
			{
				Debug.LogErrorFormat("Baking to a HoudiniAssetRoot gameobject is not supported!");
				return;
			}

			// Step through all the game objects that need to be cloned, clean up existing properties, 
			// and copy over new ones.

			// If the target is a prefab instance, we shouldn't delete the shared resources.
			// Instead new resources will be generated.
			bool bPrefabInstance = HEU_EditorUtility.IsPrefabInstance(bakeTargetGO);
			bool bDontDeletePersistantResources = bPrefabInstance;

			bool bWriteMeshesToAssetDatabase = false;
			bool bDeleteExistingComponents = true;
			bool bReconnectPrefabInstances = true;

			UnityEngine.Object targetAssetDBObject = null;

			string assetDBObjectFileName = HEU_AssetDatabase.AppendMeshesAssetFileName(_assetName);

			List<HEU_PartData> clonableParts = new List<HEU_PartData>();
			GetClonableParts(clonableParts);
			if (clonableParts.Count == 0)
			{
				Debug.LogFormat("Empty bake output. Not updating existing target gameobject as that would mean destroying the it.");
				return;
			}

			// As we find meshes, we add to a map. The map helps track whether we already created
			// unique copy that can be shared.
			Dictionary<Mesh, Mesh> sourceToTargetMeshMap = new Dictionary<Mesh, Mesh>();

			List<GameObject> unprocessedTargetChildren = HEU_GeneralUtility.GetNonInstanceChildObjects(bakeTargetGO);
			string targetAssetPath = null;

			List<GameObject> outputObjects = new List<GameObject>();
			bool bBakedSuccessful = false;

			if (clonableParts.Count == 1)
			{
				// Single object

				clonableParts[0].BakePartToGameObject(bakeTargetGO, bDeleteExistingComponents, bDontDeletePersistantResources, bWriteMeshesToAssetDatabase, ref targetAssetPath, sourceToTargetMeshMap, ref targetAssetDBObject, assetDBObjectFileName, bReconnectPrefabInstances);

				outputObjects.Add(bakeTargetGO);
				bBakedSuccessful = true;
			}
			else if(clonableParts.Count > 1)
			{
				// Multi objects
				// Leave root as is. Update each child object by matching via "name + HEU_Defines.HEU_BAKED_CLONE"

				Transform targetParentTransform = bakeTargetGO.transform;

				foreach (HEU_PartData partData in clonableParts)
				{
					if(partData._gameObject == null)
					{
						continue;
					}

					string targetGameObjectName = HEU_PartData.AppendBakedCloneName(partData._gameObject.name);
					GameObject targetObject = HEU_GeneralUtility.GetGameObjectByName(unprocessedTargetChildren, targetGameObjectName);
					if (targetObject == null)
					{
						targetObject = partData.BakePartToNewGameObject(targetParentTransform, bWriteMeshesToAssetDatabase, ref targetAssetPath, sourceToTargetMeshMap, ref targetAssetDBObject, assetDBObjectFileName, bReconnectPrefabInstances);
					}
					else
					{
						// Remove from target child list to avoid destroying it later when we process excess child gameobjects
						unprocessedTargetChildren.Remove(targetObject);

						partData.BakePartToGameObject(targetObject, bDeleteExistingComponents, bDontDeletePersistantResources, bWriteMeshesToAssetDatabase, ref targetAssetPath, sourceToTargetMeshMap, ref targetAssetDBObject, assetDBObjectFileName, bReconnectPrefabInstances);
					}

					outputObjects.Add(targetObject);
					bBakedSuccessful = true;
				}
			}

			// Clean up any children that we haven't processed
			if(unprocessedTargetChildren.Count > 0)
			{
				Debug.LogWarningFormat("Bake target has more children than bake output. GameObjects with names ending in {0} will be destroyed!", HEU_Defines.HEU_BAKED_CLONE);

				// Clean up any children that we haven't processed
				HEU_GeneralUtility.DestroyBakedGameObjectsWithEndName(unprocessedTargetChildren, HEU_Defines.HEU_BAKED_CLONE);
			}

			InvokeBakedEvent(bBakedSuccessful, outputObjects);
		}

		// EVENTS -------------------------------------------------------------------------------------------------

		/// <summary>
		/// Callback after upstream connection input node has cooked.
		/// </summary>
		/// <param name="asset">The asset that cooked and invoking us</param>
		/// <param name="bSuccess">Whether cook succeeded</param>
		/// <param name="outputs">Output gameobjects</param>
		public void NotifyUpstreamCooked(HEU_HoudiniAsset upstreamAsset, bool bSuccess, List<GameObject> outputs)
		{
			if(bSuccess)
			{
				//Debug.LogFormat("NotifyUpstreamCooked from {0}", asset.AssetName);

				// Recook after upstream cook
				// Check parameter changes otherwise can cause input nodes to be disconnected
				// then reconnected.
				RequestCook(true, true, false, true);
			}
		}

		public void ConnectToUpstream(HEU_HoudiniAsset upstreamAsset)
		{
			upstreamAsset.AddDownstreamConnection(this.NotifyUpstreamCooked);
		}

		public void DisconnectFromUpstream(HEU_HoudiniAsset upstreamAsset)
		{
			upstreamAsset.RemoveDownstreamConnection(this.NotifyUpstreamCooked);
		}

		private void AddDownstreamConnection(UnityEngine.Events.UnityAction<HEU_HoudiniAsset, bool, List<GameObject>> receiver)
		{
			// Doing remove first makes sure we don't have duplicate entries for the receiver
			_downstreamConnectionCookedEvent.RemoveListener(receiver);
			_downstreamConnectionCookedEvent.AddListener(receiver);
		}

		private void RemoveDownstreamConnection(UnityEngine.Events.UnityAction<HEU_HoudiniAsset, bool, List<GameObject>> receiver)
		{
			_downstreamConnectionCookedEvent.RemoveListener(receiver);
		}

		private void ClearAllUpstreamConnections()
		{
			if (_parameters != null)
			{
				List<GameObject> inputNodeObjects = new List<GameObject>();
				_parameters.GetInputNodeConnectionObjects(inputNodeObjects);
				foreach (GameObject go in inputNodeObjects)
				{
					HEU_HoudiniAssetRoot assetRoot = go.GetComponent<HEU_HoudiniAssetRoot>();
					if (assetRoot != null && assetRoot._houdiniAsset != null)
					{
						DisconnectFromUpstream(assetRoot._houdiniAsset);
					}
				}
			}
		}

		private void CookUpstreamConnections(HEU_SessionBase session)
		{
			foreach(HEU_InputNode inputNode in _inputNodes)
			{
				inputNode.CookUpstreamConnectedAsset(session);
			}
		}

		// TRANSFORMS -------------------------------------------------------------------------------------------------

		public void GetHoudiniTransformAndApply(HEU_SessionBase session)
		{
			int queryNodeID = _assetID;
			if (_nodeInfo.id != HEU_Defines.HEU_INVALID_NODE_ID)
			{
				if (_nodeInfo.type == HAPI_NodeType.HAPI_NODETYPE_SOP)
				{
					queryNodeID = _nodeInfo.parentId;
					Debug.Assert(queryNodeID != HEU_Defines.HEU_INVALID_NODE_ID, "Invalid parent ID for SOP!");
				}
				else if (_nodeInfo.type != HAPI_NodeType.HAPI_NODETYPE_OBJ)
				{
					return;
				}

				HAPI_Transform hapiTransform = new HAPI_Transform();
				session.GetObjectTransform(queryNodeID, -1, HAPI_RSTOrder.HAPI_SRT, ref hapiTransform);
				if (Mathf.Approximately(0f, hapiTransform.scale[0]) || Mathf.Approximately(0f, hapiTransform.scale[1]) || Mathf.Approximately(0f, hapiTransform.scale[2]))
				{
					Debug.LogWarningFormat("Asset id {0} with name {1} has scale components with 0 values!", AssetID, AssetName);
				}

				// Using root transform as that represents our asset in the world
				HEU_HAPIUtility.ApplyWorldTransfromFromHoudiniToUnity(hapiTransform, _rootGameObject.transform);

				// Save last sync'd transform
				_lastSyncedTransformMatrix = _rootGameObject.transform.localToWorldMatrix;
			}
		}

		public void UploadUnityTransform(HEU_SessionBase session, bool bOnlySendIfChangedFromLastSync)
		{
			int queryNodeID = _assetID;
			if (_nodeInfo.id != HEU_Defines.HEU_INVALID_NODE_ID)
			{
				if (_nodeInfo.type == HAPI_NodeType.HAPI_NODETYPE_SOP)
				{
					queryNodeID = _nodeInfo.parentId;
					Debug.Assert(queryNodeID != HEU_Defines.HEU_INVALID_NODE_ID, "Invalid parent ID for SOP!");
				}
				else if(_nodeInfo.type != HAPI_NodeType.HAPI_NODETYPE_OBJ)
				{
					return;
				}

				if(!session.IsSessionValid())
				{
					return;
				}

				Matrix4x4 transformMatrix = _rootGameObject.transform.localToWorldMatrix;

				if (bOnlySendIfChangedFromLastSync)
				{
					if(_lastSyncedTransformMatrix == transformMatrix)
					{
						return;
					}
				}

				HAPI_TransformEuler transformEuler = HEU_HAPIUtility.GetHAPITransformFromMatrix(ref transformMatrix);
				if (!session.SetObjectTransform(queryNodeID, ref transformEuler))
				{
					Debug.LogWarningFormat("Unable to upload transform for asset id {0} with name {1}!", AssetID, AssetName);
				}
				else
				{
					_lastSyncedTransformMatrix = transformMatrix;
				}

				// Not updating parameters after setting object transform as that is
				// updating the pre-transform, causing double transform issues.
				// Instead let user set pre - transform via the Parameters UI.
			}
		}


		// MATERIALS --------------------------------------------------------------------------------------------------

		public Dictionary<int, HEU_MaterialData> GetMaterialDataMap()
		{
			Dictionary<int, HEU_MaterialData> materialMap = new Dictionary<int, HEU_MaterialData>();
			foreach(HEU_MaterialData materialData in _materialCache)
			{
				if (materialData._materialKey == HEU_Defines.HEU_INVALID_MATERIAL)
				{
					Debug.LogWarningFormat("Invalid material key found! Recommend to reload HDA!");
				}
				else
				{
					materialMap.Add(materialData._materialKey, materialData);
				}
			}
			return materialMap;
		}

		public HEU_MaterialData GetUnitySubstanceMaterialDataFromCache(int materialKey)
		{
			foreach (HEU_MaterialData materialData in _materialCache)
			{
				if ((materialData._materialSource == HEU_MaterialData.Source.UNITY || materialData._materialSource == HEU_MaterialData.Source.SUBSTANCE) 
					&& materialData._materialKey == materialKey)
				{
					return materialData;
				}
			}
			return null;
		}

		public HEU_MaterialData GetMaterialDataFromCache(int materialKey)
		{
			foreach (HEU_MaterialData materialData in _materialCache)
			{
				if(materialData._materialKey == materialKey)
				{
					return materialData;
				}
			}
			return null;
		}

		public HEU_MaterialData GetMaterialData(Material material)
		{
			foreach (HEU_MaterialData materialData in _materialCache)
			{
				if (materialData._material == material)
				{
					return materialData;
				}
			}
			return null;
		}

		public static string GenerateDefaultMaterialName(HAPI_NodeId geoID, HAPI_PartId partID)
		{
			return string.Format("{0}_{1}_{2}", HEU_Defines.DEFAULT_MATERIAL, geoID, partID);
		}

		public HEU_MaterialData GetOrCreateDefaultMaterialInCache(HEU_SessionBase session, HAPI_NodeId geoID, HAPI_PartId partID, bool bWriteToFile)
		{
			string materialName = GenerateDefaultMaterialName(geoID, partID);
			int materialKey = HEU_MaterialFactory.MaterialNameToKey(materialName);
			HEU_MaterialData materialData = GetMaterialDataFromCache(materialKey);
			if(materialData == null)
			{
				materialData = CreateMaterialInCache(materialKey, materialName, HEU_MaterialData.Source.DEFAULT, bWriteToFile);
			}
			return materialData;
		}

		public HEU_MaterialData CreateMaterialInCache(int materialKey, string materialName, HEU_MaterialData.Source sourceType, bool bWriteToFile)
		{
			HEU_MaterialData materialData = ScriptableObject.CreateInstance<HEU_MaterialData>();
			materialData._materialSource = sourceType;
			materialData._materialKey = materialKey;

			materialData._material = HEU_MaterialFactory.CreateNewHoudiniStandardMaterial(GetValidAssetCacheFolderPath(), materialName, bWriteToFile);
			materialData._material.name = materialName;

			_materialCache.Add(materialData);
			return materialData;
		}

		public HEU_MaterialData CreateMaterialCopyInCache(Material sourceMaterial, int materialKey, string materialName, bool bWriteToFile)
		{
			Material newMaterial = Material.Instantiate(sourceMaterial) as Material;
			if(newMaterial != null)
			{
				HEU_MaterialData materialData = ScriptableObject.CreateInstance<HEU_MaterialData>();
				materialData._materialSource = HEU_MaterialData.Source.HOUDINI;
				materialData._materialKey = materialKey;

				materialData._material = newMaterial;
				materialData._material.name = materialName;

				if (bWriteToFile)
				{
					HEU_MaterialFactory.WriteMaterialToAssetCache(newMaterial, GetValidAssetCacheFolderPath(), materialName);
				}

				_materialCache.Add(materialData);
				return materialData;
			}
			else
			{
				return null;
			}
		}

		public HEU_MaterialData CreateUnitySubstanceMaterialData(int materialKey, string materialPath, string substanceName, int substanceIndex)
		{
			// Let's make sure we can find the Unity or Substance material first
			Material material = null;

			HEU_MaterialData.Source sourceType = HEU_MaterialData.Source.UNITY;
			if(!string.IsNullOrEmpty(substanceName))
			{
				sourceType = HEU_MaterialData.Source.SUBSTANCE;
				material = HEU_MaterialFactory.LoadSubstanceMaterialWithName(materialPath, substanceName);
			}
			else if(substanceIndex >= 0)
			{
				sourceType = HEU_MaterialData.Source.SUBSTANCE;
				material = HEU_MaterialFactory.LoadSubstanceMaterialWithIndex(materialPath, substanceIndex);
			}
			else if(!string.IsNullOrEmpty(materialPath))
			{
				material = HEU_MaterialFactory.LoadUnityMaterial(materialPath);
			}

			if (material != null)
			{
				HEU_MaterialData materialData = ScriptableObject.CreateInstance<HEU_MaterialData>();
				materialData._materialSource = sourceType;
				materialData._materialKey = materialKey;
				materialData._material = material;

				_materialCache.Add(materialData);
				return materialData;
			}
			else
			{
				// We can't find the material in Unity, so notify user and use a default one which allows to at least get the geometry in.
				if (string.IsNullOrEmpty(materialPath))
				{
					Debug.LogErrorFormat("Empty material name found. Using default material.");
				}
				else
				{
					Debug.LogErrorFormat("Unable to find {0} material {1}. Using a default material instead. Please check material exists in project and reload asset!", sourceType, materialPath);
				}

				// The materialKey helps uniquely identify this material for further look ups. But we also need to get a valid file name
				// to create the default material, so strip out just the file name.
				string strippedFileName = HEU_Platform.GetFileName(materialPath);
				return CreateMaterialInCache(materialKey, strippedFileName, HEU_MaterialData.Source.UNITY, false);
			}
		}

		public HEU_MaterialData CreateHoudiniMaterialData(HEU_SessionBase session, HAPI_NodeId materialID, HAPI_NodeId geoID, HAPI_PartId partID)
		{
			string materialName = "";

			if(materialID == HEU_Defines.HEU_INVALID_NODE_ID)
			{
				return GetOrCreateDefaultMaterialInCache(session, geoID, partID, false);
			}
			else
			{
				materialName = HEU_SessionManager.GetUniqueMaterialShopName(_assetID, materialID);
			}

			HEU_MaterialData materialData = ScriptableObject.CreateInstance<HEU_MaterialData>();
			materialData._materialSource = HEU_MaterialData.Source.HOUDINI;
			materialData._materialKey = materialID;

			materialData._material = HEU_MaterialFactory.CreateNewHoudiniStandardMaterial(GetValidAssetCacheFolderPath(), materialName, true);
			materialData._material.name = materialName;

			//Debug.LogFormat("New Material ID: {0} - {1}", materialID, materialName);

			if (materialID != HEU_Defines.HEU_INVALID_NODE_ID)
			{
				// Get material info from Houdini to populate the Unity material values

				HAPI_MaterialInfo materialInfo = new HAPI_MaterialInfo();
				if(session.GetMaterialInfo(materialID, ref materialInfo))
				{
					if(materialInfo.exists)
					{
						materialData.UpdateMaterialFromHoudini(materialInfo, GetValidAssetCacheFolderPath());
					}
				}
			}

			//Debug.LogFormat("Created new material with id={0} and name={1}", materialID, materialName);

			_materialCache.Add(materialData);
			return materialData;
		}

		public void ClearMaterialCache()
		{
			_materialCache.Clear();
		}

		/// <summary>
		/// Go through the materials in use and update them with values from Houdini.
		/// </summary>
		private void UpdateHoudiniMaterials(HEU_SessionBase session)
		{
			HAPI_MaterialInfo materialInfo = new HAPI_MaterialInfo();

			List<HEU_MaterialData> removeMaterialList = new List<HEU_MaterialData>();

			foreach(HEU_MaterialData materialData in _materialCache)
			{
				// Non-Houdini material so no need to update it.
				if(materialData._materialSource != HEU_MaterialData.Source.HOUDINI || materialData._materialKey == HEU_Defines.HEU_INVALID_MATERIAL
					|| materialData._materialKey == HEU_Defines.EDITABLE_MATERIAL_KEY)
				{
					continue;
				}

				if (session.GetMaterialInfo(materialData._materialKey, ref materialInfo))
				{
					//Debug.LogFormat("Material id={0}, exists={1}, changed={2}", materialData._materialID, materialInfo.exists, materialInfo.hasChanged);

					if(materialInfo.exists)
					{
						if(materialInfo.hasChanged)
						{
							materialData.UpdateMaterialFromHoudini(materialInfo, GetValidAssetCacheFolderPath());
						}
					}
					else
					{
						// No longer exists. Remove it from here.
						removeMaterialList.Add(materialData);
					}
				}
			}
		}

		private void RemoveUnusedMaterials()
		{
			// Find the unused materials
			List<HEU_MaterialData> materialsToRemove = new List<HEU_MaterialData>();
			foreach(HEU_MaterialData materialData in _materialCache)
			{
				bool bUsing = false;

				foreach(HEU_ObjectNode objectNode in _objectNodes)
				{
					if(objectNode.IsUsingMaterial(materialData))
					{
						bUsing = true;
						break;
					}
				}

				if(!bUsing)
				{
					materialsToRemove.Add(materialData);
				}
			}

			// Delete them
			for (int i = 0; i < materialsToRemove.Count; ++i)
			{
				HEU_MaterialData materialData = materialsToRemove[i];
				// Skipping editable materials as those are needed when painting
				if ((materialData._materialSource == HEU_MaterialData.Source.HOUDINI || materialData._materialSource == HEU_MaterialData.Source.DEFAULT) 
					&& materialData._materialKey != HEU_Defines.EDITABLE_MATERIAL_KEY)
				{
					// Houdini materials and default material were created dynamically so we can delete them
					// TODO: Currently not deleting any textures not in use. Revisit to address it
					HEU_MaterialFactory.DeleteAssetMaterial(materialData._material);
				}

				// Other materials (Unity, Substance) were presumably part of the project
				// already and not created by us, so we don't explicility delete them.

				// Now remove from our cache which should clear reference
				_materialCache.Remove(materialData);

				HEU_GeneralUtility.DestroyImmediate(materialData, false);
			}
		}

		public void RemoveMaterial(Material material)
		{
			HEU_MaterialData materialData = null;
			for(int i = 0; i < _materialCache.Count; ++i)
			{
				if(_materialCache[i]._material == material)
				{
					materialData = _materialCache[i];
					_materialCache.RemoveAt(i);

					HEU_GeneralUtility.DestroyImmediate(materialData, false);
					break;
				}
			}
		}

		// MISC -------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Returns true if the asset is valid in given Houdini session
		/// </summary>
		/// <returns>True if the asset is valid in Houdini</returns>
		public bool IsAssetValidInHoudini(HEU_SessionBase session)
		{
			return HEU_HAPIUtility.IsAssetValidInHoudini(session, _assetID);
		}

		/// <summary>
		/// Returns true if the current asset transform has changed from
		/// the last upload to HAPI.
		/// </summary>
		/// <returns>True if transform has changed since last upload</returns>
		public bool HasTransformChangedSinceLastUpdate()
		{
			return (_lastSyncedTransformMatrix != transform.localToWorldMatrix);
		}

		public void GetClonableParts(List<HEU_PartData> clonableParts)
		{
			foreach (HEU_ObjectNode objNode in _objectNodes)
			{
				if (!objNode.IsInstanced() || objNode.IsVisible())
				{
					objNode.GetClonableParts(clonableParts);
				}
			}
		}

		public HEU_PartData GetPartWithID(HAPI_PartId partID)
		{
			foreach (HEU_ObjectNode objNode in _objectNodes)
			{
				return objNode.GetPartWithID(partID);
			}
			return null;
		}

		/// <summary>
		/// Adds gameobjects that were output from this asset.
		/// </summary>
		/// <param name="outputObjects">List to add to</param>
		public void GetOutputGameObjects(List<GameObject> outputObjects)
		{
			foreach (HEU_ObjectNode objNode in _objectNodes)
			{
				objNode.GetOutputGameObjects(outputObjects);
			}
		}

		/// <summary>
		/// Returns the HEU_PartData with the given output gameobject.
		/// </summary>
		/// <param name="outputGameObject">The output gameobject associated with the part</param>
		/// <returns>Valid HEU_PartData or null if no match</returns>
		public HEU_PartData GetInternalHDAPartWithGameObject(GameObject outputGameObject)
		{
			HEU_PartData foundPart = null;
			foreach (HEU_ObjectNode objNode in _objectNodes)
			{
				foundPart = objNode.GetHDAPartWithGameObject(outputGameObject);
				if(foundPart != null)
				{
					return foundPart;
				}
			}
			return null;
		}

		public HEU_GeoNode GetInternalGeoNode(HAPI_NodeId nodeID)
		{
			HEU_GeoNode geoNode = null;
			foreach (HEU_ObjectNode objNode in _objectNodes)
			{
				geoNode = objNode.GetGeoNode(nodeID);
				if (geoNode != null)
				{
					return geoNode;
				}
			}
			return null;
		}

		public List<HEU_Curve> GetCurves()
		{
			return _curves;
		}

		public HEU_Curve GetCurve(string curveName)
		{
			foreach (HEU_Curve curve in _curves)
			{
				if (curve.CurveName.Equals(curveName))
				{
					return curve;
				}
			}
			return null;
		}

		public int GetEditableCurveCount()
		{
			int count = 0;
			foreach(HEU_Curve curve in _curves)
			{
				if(curve.IsEditable())
				{
					count++;
				}
			}
			return count;
		}

		public void AddCurve(HEU_Curve curve)
		{
			if (!_curves.Contains(curve))
			{
				_curves.Add(curve);
			}
		}

		public void RemoveCurve(HEU_Curve curve)
		{
			_curves.Remove(curve);
		}

		public void AddCurveDrawCollider(Collider newCollider)
		{
			if (!_curveDrawColliders.Contains(newCollider))
			{
				_curveDrawColliders.Add(newCollider);
			}
		}

		public void RemoveCurveDrawCollider(Collider collider)
		{
			if (_curveDrawColliders != null)
			{
				_curveDrawColliders.Remove(collider);
			}
		}

		public void ClearCurveDrawColliders()
		{
			if (_curveDrawColliders != null)
			{
				_curveDrawColliders.Clear();
			}
		}

		public List<HEU_InputNode> GetInputNodes()
		{
			return _inputNodes;
		}

		public HEU_InputNode GetInputNode(string inputName)
		{
			foreach (HEU_InputNode node in _inputNodes)
			{
				if (node.InputName.Equals(inputName))
				{
					return node;
				}
			}
			return null;
		}

		public List<HEU_InputNode> GetNonParameterInputNodes()
		{
			List<HEU_InputNode> nodes = new List<HEU_InputNode>();
			foreach(HEU_InputNode node in _inputNodes)
			{
				if(node.InputType != HEU_InputNode.InputNodeType.PARAMETER)
				{
					nodes.Add(node);
				}
			}
			return nodes;
		}

		public void AddInputNode(HEU_InputNode node)
		{
			if (!_inputNodes.Contains(node))
			{
				_inputNodes.Add(node);
			}
		}

		public void RemoveInputNode(HEU_InputNode node)
		{
			_inputNodes.Remove(node);
		}

		public List<HEU_AttributesStore> GetAttributesStores()
		{
			return _attributeStores;
		}

		public int NumAttributeStores()
		{
			return _attributeStores != null ? _attributeStores.Count : 0;
		}

		public HEU_AttributesStore GetAttributeStore(string geoName, HAPI_PartId partID)
		{
			foreach(HEU_AttributesStore attrStore in _attributeStores)
			{
				if(attrStore.GeoName.Equals(geoName) && attrStore.PartID == partID)
				{
					return attrStore;
				}
			}
			return null;
		}

		public void AddAttributeStore(HEU_AttributesStore attributeStore)
		{
			if (!_attributeStores.Contains(attributeStore))
			{
				// Add in alphabetical order of GeoName. This allows users to use editable node names to
				// set order of edit operations.
				int numAttrs = _attributeStores.Count;
				for (int i = 0; i < numAttrs; ++i)
				{
					if (string.Compare(attributeStore.GeoName, _attributeStores[i].GeoName) < 0)
					{
						_attributeStores.Insert(i, attributeStore);
						return;
					}
				}

				_attributeStores.Add(attributeStore);
			}
		}

		public void RemoveAttributeStore(HEU_AttributesStore attributeStore)
		{
			_attributeStores.Remove(attributeStore);
		}

		/// <summary>
		/// Move the attribute store at oldIndex to newIndex.
		/// </summary>
		/// <param name="oldIndex">The attribute store at this index will be moved</param>
		/// <param name="newIndex">The new index to move it to</param>
		public void ReorderAttributeStore(int oldIndex, int newIndex)
		{
			int count = _attributeStores.Count;

			if (oldIndex == newIndex || oldIndex < 0 || oldIndex >= count || newIndex < 0 || newIndex >= count)
			{
				return;
			}

			HEU_AttributesStore attrStore = _attributeStores[oldIndex];

			if ((oldIndex < newIndex) || (newIndex < count - 1))
			{
				_attributeStores.RemoveAt(oldIndex);
				_attributeStores.Insert(newIndex, attrStore);
			}
		}

		/// <summary>
		/// In the given scene, for the given gameobject, get the HEU_PartData that created it.
		/// </summary>
		/// <param name="outputGameObject">The output gameobject associated with the part</param>
		/// <returns>Valid HEU_PartData or null if no match</returns>
		public static HEU_PartData GetSceneHDAPartWithGameObject(GameObject outputGameObject)
		{
			// The structure of an HDA inside a Unity scene should be such that
			// outputGameObject should have parent with HEU_HoudiniAssetRoot component.
			// Then get the HEU_HoudiniAsset, and find the part with the matching gameobject.

			if (outputGameObject.transform.parent != null)
			{
				GameObject parentGO = outputGameObject.transform.parent.gameObject;
				HEU_HoudiniAssetRoot assetRoot = parentGO.GetComponent<HEU_HoudiniAssetRoot>();
				if(assetRoot != null && assetRoot._houdiniAsset != null)
				{
					return assetRoot._houdiniAsset.GetInternalHDAPartWithGameObject(outputGameObject);
				}
			}
			return null;
		}

		/// <summary>
		/// In the given scene, for the given gameobject, get the parent HEU_HoudiniAsset.
		/// </summary>
		/// <param name="outputGameObject">The output gameobject associated with the asset</param>
		/// <returns>Valid HEU_HoudiniAsset or null if no match</returns>
		public static HEU_HoudiniAsset GetSceneHDAAssetFromGameObject(GameObject outputGameObject)
		{
			if (outputGameObject.transform.parent != null)
			{
				GameObject parentGO = outputGameObject.transform.parent.gameObject;
				HEU_HoudiniAssetRoot assetRoot = parentGO.GetComponent<HEU_HoudiniAssetRoot>();
				if (assetRoot != null && assetRoot._houdiniAsset != null)
				{
					return assetRoot._houdiniAsset;
				}
			}
			return null;
		}

		/// <summary>
		/// Returns true if given object is an output of an HDA.
		/// </summary>
		/// <param name="go">GameObject to check if output</param>
		/// <returns>True if object is an output of an HDA</returns>
		public static bool IsHoudiniAssetOutput(GameObject go)
		{
			return (go.transform.parent != null) && (go.transform.parent.gameObject.GetComponent<HEU_HoudiniAssetRoot>() != null) 
				&& (go.GetComponent<HEU_HoudiniAsset>() == null);
		}

		/// <summary>
		/// Returns true if given object is the root of an HDA.
		/// </summary>
		/// <param name="go">GameObject to check</param>
		/// <returns>Returns true if given object is the root of an HDA</returns>
		public static bool IsHoudiniAssetRoot(GameObject go)
		{
			return go.GetComponent<HEU_HoudiniAssetRoot>() != null;
		}

		/// <summary>
		/// Fill in the objInstanceInfos list with the HEU_ObjectInstanceInfos used by this asset.
		/// </summary>
		/// <param name="objInstanceInfos">List to fill in</param>
		public void PopulateObjectInstanceInfos(List<HEU_ObjectInstanceInfo> objInstanceInfos)
		{
			foreach (HEU_ObjectNode objNode in _objectNodes)
			{
				objNode.PopulateObjectInstanceInfos(objInstanceInfos);
			}
		}

		/// <summary>
		/// Add given object to this asset's asset database cache.
		/// </summary>
		/// <param name="assetObjectFileName">File name of asset database object</param>
		/// <param name="objectToAdd">The object to add</param>
		/// <param name="targetAssetDBObject">Existing asset database object to overwrite or null. Returns valid written object.</param>
		public void AddToAssetDBCache(string assetObjectFileName, UnityEngine.Object objectToAdd, ref UnityEngine.Object targetAssetDBObject)
		{
			// Once the asset cache folder is set, CreateAddObjectInAssetCacheFolder will not update it
			string assetCacheFolder = GetValidAssetCacheFolderPath();
			HEU_AssetDatabase.CreateAddObjectInAssetCacheFolder(AssetName, assetObjectFileName, objectToAdd, ref assetCacheFolder, ref targetAssetDBObject);
		}

		/// <summary>
		/// Show or hide all curves in the current scene.
		/// </summary>
		/// <param name="bShow">True to show</param>
		public static void SetCurvesVisibilityInScene(bool bShow)
		{
			HEU_HoudiniAsset[] houdiniAssets = GameObject.FindObjectsOfType<HEU_HoudiniAsset>();
			foreach (HEU_HoudiniAsset asset in houdiniAssets)
			{
				List<HEU_Curve> curves = asset.GetCurves();
				foreach (HEU_Curve curve in curves)
				{
					curve.SetCurveGeometryVisibility(bShow);
				}
			}
		}

		/// <summary>
		/// Returns the session that this asset was created / resides in.
		/// null if no valid session, or if this asset hasn't been created in one yet.
		/// </summary>
		/// <param name="bCreateIfInvalid">If true and current session is invalid, will try creating a new session.</param>
		/// <returns>Session containing this asset or null if unable to get one</returns>
		public HEU_SessionBase GetAssetSession(bool bCreateIfInvalid)
		{
			HEU_SessionBase session = HEU_SessionManager.GetSessionWithID(_sessionID);
			if ((session == null || !session.IsSessionValid()) && bCreateIfInvalid)
			{
				// Invalid session could either mean that this asset hasn't been created in any session (after a Scene load)
				// or that we aren't able to create Houdini sessions (installation/license problems).
				// To handle former case, we ask again to get us a valid (and new if none exist) session
				session = HEU_SessionManager.GetOrCreateDefaultSession();
				if (session != null && session.IsSessionValid())
				{
					// Update this asset's session ID with the new session.
					_sessionID = session.GetSessionData().SessionID;
					return session;
				}
			}

			// Nullify the session if not valid so that callers don't need to check for validity themselves.
			if (session != null && !session.IsSessionValid())
			{
				session = null;
			}

			return session;
		}

		/// <summary>
		/// Returns a valid asset cache folder path for this asset.
		/// Creates the cache folder path if not already done so.
		/// </summary>
		/// <returns>Valid asset cache folder path</returns>
		private string GetValidAssetCacheFolderPath()
		{
			if(string.IsNullOrEmpty(_assetCacheFolderPath))
			{
				// Create folder based on asset name + unique in plugin cache folder
				// Store materials and textures in folder
				// Delete folder when asset is deleted

				string suggestedFileName = _assetPath;

				if (string.IsNullOrEmpty(suggestedFileName)
					&& (_assetType == HEU_AssetType.TYPE_CURVE || _assetType == HEU_AssetType.TYPE_INPUT))
				{
					// Since curves and input nodes are not loaded from an asset file, we
					// set the asset 
					suggestedFileName = _rootGameObject.name;

				}

				_assetCacheFolderPath = HEU_AssetDatabase.CreateAssetCacheFolder(suggestedFileName);
			}
			return _assetCacheFolderPath;
		}

		/// <summary>
		/// Hide all geometry contained within
		/// </summary>
		public void HideAllGeometry()
		{
			foreach (HEU_ObjectNode objNode in _objectNodes)
			{
				objNode.HideAllGeometry();
			}
		}

		/// <summary>
		/// Calculate visiblity of all geometry within
		/// </summary>
		public void CalculateVisibility()
		{
			foreach (HEU_ObjectNode objNode in _objectNodes)
			{
				objNode.CalculateVisibility();
			}
		}

		public void DisableAllColliders()
		{
			foreach (HEU_ObjectNode objNode in _objectNodes)
			{
				objNode.DisableAllColliders();
			}
		}

		/// <summary>
		/// Calculate visiblity of all geometry within
		/// </summary>
		public void CalculateColliderState()
		{
			foreach (HEU_ObjectNode objNode in _objectNodes)
			{
				objNode.CalculateColliderState();
			}
		}

		/// <summary>
		/// Create a copy of this asset in the Scene and returns it.
		/// </summary>
		public GameObject DuplicateAsset()
		{
			string goName = _rootGameObject.name + "_copy";
			HEU_HoudiniAssetRoot sourceRoot = _rootGameObject.GetComponent<HEU_HoudiniAssetRoot>();

			Debug.Log("Duplicating asset: " + goName);
			
			bool bBuildAsync = false;
			HEU_SessionBase session = GetAssetSession(true);
			Transform thisParentTransform = _rootGameObject.transform.parent;

			GameObject newRootGO = null;
			if (_assetType == HEU_AssetType.TYPE_HDA)
			{
				newRootGO = HEU_HAPIUtility.InstantiateHDA(_assetPath, _rootGameObject.transform.position, session, bBuildAsync);
			}
			else if(_assetType == HEU_AssetType.TYPE_CURVE)
			{
				newRootGO = HEU_HAPIUtility.CreateNewCurveAsset(thisParentTransform, session, bBuildAsync);
			}
			else if(_assetType == HEU_AssetType.TYPE_INPUT)
			{
				newRootGO = HEU_HAPIUtility.CreateNewInputAsset(thisParentTransform, session, bBuildAsync);
			}
			else
			{
				Debug.LogErrorFormat("Unsupported asset type {0} for duplication.", _assetType);
				return null;
			}

			HEU_HoudiniAssetRoot newRoot = newRootGO.GetComponent<HEU_HoudiniAssetRoot>();
			HEU_HoudiniAsset newAsset = newRoot._houdiniAsset;

			Transform newRootTransform = newRootGO.transform;
			newRootTransform.parent = thisParentTransform;
			newRootTransform.localPosition = _rootGameObject.transform.localPosition;
			newRootTransform.localRotation = _rootGameObject.transform.localRotation;
			newRootTransform.localScale = _rootGameObject.transform.localScale;

			// Set parameter preset for asset
			newAsset.Parameters.SetPresetData(_parameters.GetPresetData());

			// Set parameter preset for curves
			int numCurves = newAsset._curves.Count;
			for (int i = 0; i < numCurves; ++i)
			{
				HEU_Curve srcCurve = GetCurve(newAsset._curves[i].CurveName);
				if (srcCurve != null)
				{
					newAsset._curves[i].Parameters.SetPresetData(srcCurve.Parameters.GetPresetData());
				}
			}

			// Upload parameter preset
			newAsset.UploadParameterPresetToHoudini(newAsset.GetAssetSession(false));

			// Copy over asset options
			newAsset._showInstanceInputs	= this._showInstanceInputs;
			newAsset._showHDAOptions		= this._showHDAOptions;
			newAsset._showGenerateSection	= this._showGenerateSection;
			newAsset._showBakeSection		= this._showBakeSection;
			newAsset._showEventsSection		= this._showEventsSection;
			newAsset._showCurvesSection		= this._showCurvesSection;
			newAsset._showInputNodesSection = this._showInputNodesSection;
			newAsset._showToolsSection		= this._showToolsSection;

			// Copy over tools state
			newAsset._editableNodesToolsEnabled = this._editableNodesToolsEnabled;
			newAsset._toolsInfo = ScriptableObject.Instantiate(this._toolsInfo) as HEU_ToolsInfo;

			// Copy events
			newAsset._reloadEvent = this._reloadEvent;
			newAsset._cookedEvent = this._cookedEvent;
			newAsset._bakedEvent = this._bakedEvent;

			newAsset._downstreamConnectionCookedEvent = this._downstreamConnectionCookedEvent;

			// Copy and upload attribute values
			int numAttributeStores = newAsset._attributeStores.Count;
			for (int i = 0; i < numAttributeStores; ++i)
			{
				HEU_AttributesStore newAttrStore = newAsset._attributeStores[i];

				HEU_AttributesStore srcAttrStore = this.GetAttributeStore(newAttrStore.GeoName, newAttrStore.PartID);
				if(srcAttrStore != null)
				{
					srcAttrStore.CopyAttributeValuesTo(newAttrStore);
				}
			}

			// Copy and upload asset inputs
			int numInputNodes = newAsset._inputNodes.Count;
			for (int i = 0; i < numInputNodes; ++i)
			{
				HEU_InputNode newInputNode = newAsset._inputNodes[i];

				HEU_InputNode srcInputNode = this.GetInputNode(newInputNode.InputName);
				if (srcInputNode != null)
				{
					srcInputNode.CopyInputValuesTo(session, newInputNode);

					newInputNode.RequiresCook = srcInputNode.RequiresCook;
					newInputNode.RequiresUpload = srcInputNode.RequiresUpload;
				}
			}

			newAsset.RequestCook(false, false, false, false);


			// Select it
			HEU_EditorUtility.SelectObject(newRootGO);

			return newRootGO;
		}
	}

}   // HoudiniEngineUnity
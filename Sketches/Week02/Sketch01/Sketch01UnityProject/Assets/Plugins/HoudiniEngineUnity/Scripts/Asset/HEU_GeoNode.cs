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

// Uncomment to profile
//#define HEU_PROFILER_ON

#define TERRAIN_SUPPORTED

using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HoudiniEngineUnity
{
	/////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	// Typedefs (copy these from HEU_Common.cs)
	using HAPI_NodeId = System.Int32;
	using HAPI_PartId = System.Int32;


	/// <summary>
	/// Represents a Geometry (SOP) node.
	/// </summary>
	public class HEU_GeoNode : ScriptableObject
	{
		//	DATA ------------------------------------------------------------------------------------------------------

		public HAPI_NodeId GeoID { get { return _geoInfo.nodeId; } }

		[SerializeField]
		private HAPI_GeoInfo _geoInfo;

		[SerializeField]
		private string _geoName;
		public string GeoName { get { return _geoName; } }

		public HAPI_GeoType GeoType { get { return _geoInfo.type; } }

		public bool Editable { get { return _geoInfo.isEditable; } }

		public bool Displayable { get { return _geoInfo.isDisplayGeo; } }

		public bool IsVisible() { return _containerObjectNode.IsVisible(); }

		public bool IsIntermediateOrEditable() { return (_geoInfo.type == HAPI_GeoType.HAPI_GEOTYPE_INTERMEDIATE || (_geoInfo.type == HAPI_GeoType.HAPI_GEOTYPE_DEFAULT && _geoInfo.isEditable)); }

		public bool IsGeoInputType() { return _geoInfo.isEditable && _geoInfo.type == HAPI_GeoType.HAPI_GEOTYPE_INPUT;  }

		public bool IsGeoCurveType() { return _geoInfo.type == HAPI_GeoType.HAPI_GEOTYPE_CURVE; }

		[SerializeField]
		private List<HEU_PartData> _parts;

		[SerializeField]
		private HEU_ObjectNode _containerObjectNode;

		public HEU_HoudiniAsset ParentAsset { get { return (_containerObjectNode != null) ? _containerObjectNode.ParentAsset : null; } }

		[SerializeField]
		private HEU_InputNode _inputNode;

		[SerializeField]
		private HEU_Curve _geoCurve;


		//  LOGIC -----------------------------------------------------------------------------------------------------

		public HEU_GeoNode()
		{
			Reset();
		}

		/// <summary>
		/// Destroy all generated data.
		/// </summary>
		public void DestroyAllData()
		{
			HEU_PartData.DestroyParts(_parts);

			if(_inputNode != null)
			{
				HEU_SessionBase session = null;
				if (ParentAsset != null)
				{
					ParentAsset.RemoveInputNode(_inputNode);
					session = ParentAsset.GetAssetSession(false);
				}

				_inputNode.DestroyAllData(session);
				HEU_GeneralUtility.DestroyImmediate(_inputNode);
				_inputNode = null;
			}

			if (_geoCurve != null)
			{
				if (ParentAsset != null)
				{
					ParentAsset.RemoveCurve(_geoCurve);
				}
				_geoCurve.DestroyAllData();
				HEU_GeneralUtility.DestroyImmediate(_geoCurve);
				_geoCurve = null;
			}
		}

		public void RemoveAndDestroyPart(HEU_PartData part)
		{
			_parts.Remove(part);
			HEU_PartData.DestroyPart(part);
		}

		/// <summary>
		/// Destroy the generated mesh data.
		/// </summary>
		/// <param name="bRegisterUndo">Register Undo action</param>
		public void DestroyGeneratedMeshData(bool bRegisterUndo)
		{
			int numParts = _parts.Count;
			for (int i = 0; i < numParts; ++i)
			{
				_parts[i].DestroyGeneratedMeshData(bRegisterUndo: bRegisterUndo);
			}
		}

		public void Reset()
		{
			_geoName = "";

			_geoInfo = new HAPI_GeoInfo();
			_geoInfo.nodeId = -1;
			_geoInfo.type = HAPI_GeoType.HAPI_GEOTYPE_DEFAULT;
			_parts = new List<HEU_PartData>();
		}

		public void Initialize(HEU_SessionBase session, HAPI_GeoInfo geoInfo, HEU_ObjectNode containerObjectNode)
		{
			_containerObjectNode = containerObjectNode;
			_geoInfo = geoInfo;
			_geoName = HEU_SessionManager.GetString(_geoInfo.nameSH, session);

			//Debug.Log(string.Format("GeoNode initialized with ID={0}, name={1}, type={2}", GeoID, GeoName, geoInfo.type));
		}

		public bool DoesThisRequirePotentialCook()
		{
			if((_geoInfo.type == HAPI_GeoType.HAPI_GEOTYPE_INPUT) 
				|| (_geoInfo.isTemplated && !HEU_PluginSettings.CookTemplatedGeos && !_geoInfo.isEditable)
				|| (!_geoInfo.hasGeoChanged)
				|| (!_geoInfo.isDisplayGeo && (_geoInfo.type != HAPI_GeoType.HAPI_GEOTYPE_CURVE)))
			{
				return false;
			}
			return true;
		}

		public void UpdateGeo(HEU_SessionBase session)
		{
			// Create or recreate parts.

			bool bObjectInstancer = _containerObjectNode.IsInstancer();

			// Save list of old parts. We'll destroy these after creating new parts.
			// The reason for temporarily keeping these is to transfer data (eg. instance overrides, attribute data)
			List<HEU_PartData> oldParts = new List<HEU_PartData>(_parts);
			_parts.Clear();

			try
			{
				// TODO: FIXME: When a heightfield erode node is in the network, it causes multiple display + editable
				// nodes to appear. This is a hack to reduce those down to just the single display node. But should be fixed properly!
				//if (!_geoInfo.isDisplayGeo || (_geoInfo.isDisplayGeo && _geoInfo.isEditable && _geoInfo.type == HAPI_GeoType.HAPI_GEOTYPE_DEFAULT))

				if(!_geoInfo.isDisplayGeo)
				{
					if(ParentAsset.IgnoreNonDisplayNodes)
					{
						return;
					}
					else if (!_geoInfo.isEditable 
							|| (_geoInfo.type != HAPI_GeoType.HAPI_GEOTYPE_DEFAULT 
								&& _geoInfo.type != HAPI_GeoType.HAPI_GEOTYPE_INTERMEDIATE
								&& _geoInfo.type != HAPI_GeoType.HAPI_GEOTYPE_CURVE))
					{
						return;
					}
				}

				if (IsGeoCurveType())
				{
					ProcessGeoCurve(session);
				}
				else
				{
					int numParts = _geoInfo.partCount;
					//Debug.Log("Number of parts: " + numParts);
					//Debug.LogFormat("GeoNode type {0}, isTemplated: {1}, isDisplayGeo: {2}, isEditable: {3}", _geoInfo.type, _geoInfo.isTemplated, _geoInfo.isDisplayGeo, _geoInfo.isEditable);
					for (int i = 0; i < numParts; ++i)
					{
						HAPI_PartInfo partInfo = new HAPI_PartInfo();
						if (!session.GetPartInfo(GeoID, i, ref partInfo))
						{
							Debug.LogErrorFormat("Unable to get PartInfo for geo node {0} and part {1}.", GeoID, i);
							continue;
						}

						// Find the old part for this new part.
						HEU_PartData part = null;
						HEU_PartData oldMatchedPart = null;

						foreach (HEU_PartData oldPart in oldParts)
						{
							string partName = HEU_SessionManager.GetString(partInfo.nameSH, session);
							if (oldPart.PartName.Equals(partName))
							{
								oldMatchedPart = oldPart;
							}
						}

						if (oldMatchedPart != null)
						{
							//Debug.Log("Found matched part: " + oldMatchedPart.name);

							List<HEU_ObjectInstanceInfo> sourceObjectInstanceInfos = null;
							if (bObjectInstancer)
							{
								// ProcessPart will clear out the object instances, so hence why
								// we keep a copy here, then restore after processing the parts.
								sourceObjectInstanceInfos = oldMatchedPart.GetObjectInstanceInfos();
							}

							// Clear out old generated data
							oldMatchedPart.ClearGeneratedData();

							part = oldMatchedPart;
							oldParts.Remove(oldMatchedPart);

							ProcessPart(session, i, ref partInfo, ref part);

							if (part != null && bObjectInstancer && sourceObjectInstanceInfos != null)
							{
								// Set object instances from old part into new. This keeps the user set object inputs around.
								part.SetObjectInstanceInfos(sourceObjectInstanceInfos);
							}
						}
						else
						{
							ProcessPart(session, i, ref partInfo, ref part);
						}
					}
				}
			}
			finally
			{
				HEU_PartData.DestroyParts(oldParts);
			}
		}

		/// <summary>
		/// Process custom attribute with Unity script name, and attach any scripts found.
		/// </summary>
		/// <param name="session">Session to use</param>
		public void ProcessUnityScriptAttribute(HEU_SessionBase session)
		{
			if(_parts == null || _parts.Count == 0)
			{
				return;
			}

			HAPI_AttributeInfo scriptAttributeInfo = new HAPI_AttributeInfo();
			int[] scriptAttr = new int[0];

			HEU_GeneralUtility.GetAttribute(session, GeoID, 0, HEU_PluginSettings.UnityScriptAttributeName, ref scriptAttributeInfo, ref scriptAttr, session.GetAttributeStringData);
			if (scriptAttributeInfo.exists)
			{
				if (scriptAttributeInfo.owner != HAPI_AttributeOwner.HAPI_ATTROWNER_DETAIL)
				{
					Debug.LogWarningFormat("Houdini Engine for Unity only supports {0} as detail attributes!", HEU_PluginSettings.UnityScriptAttributeName);
				}
				else if (scriptAttr.Length > 0)
				{
					string scriptToAttach = HEU_SessionManager.GetString(scriptAttr[0]);
					AttachScriptWithInvokeFunction(scriptToAttach);
				}
			}
		}

		/// <summary>
		/// Attach a script in Unity to the asset root gameobject, and optionally
		/// invoke a function with an optional argument.
		/// </summary>
		/// <param name="scriptToAttach">A string with format: scriptname:function:msg</param>
		public void AttachScriptWithInvokeFunction(string scriptToAttach)
		{
			// Script will be attached to the asset root.
			// Then if set, te function will be invoked on the script passing in the message.
			// Format: scriptname:function:msg
			string expectedFormat = "scriptname:function:argument";

			int scriptColon = scriptToAttach.IndexOf(":");
			if(scriptColon <= 0)
			{
				Debug.LogFormat("Invalid script attribute value. Expected format {0}, but got {1}", expectedFormat, scriptToAttach);
				return;
			}

			string scriptTypeName = scriptToAttach.Substring(0, scriptColon).Trim();
			System.Type scriptType = HEU_GeneralUtility.GetSystemTypeByName(scriptTypeName);
			if(scriptType == null)
			{
				Debug.LogFormat("Script with name {0} not found! Unable to attach script from attribute: {1}", scriptTypeName, scriptToAttach);
				return;
			}

			GameObject rootGameObject = ParentAsset.RootGameObject;

			Component component = rootGameObject.GetComponent(scriptType);
			if (component == null)
			{
				Debug.LogFormat("Attaching script {0} to root gameobject", scriptType);
				component = rootGameObject.AddComponent(scriptType);
				if (component == null)
				{
					Debug.LogFormat("Unable to attach script component with type '{0}' from script attribute: {1}", scriptType.ToString(), scriptToAttach);
					return;
				}
			}

			if(scriptColon + 1 >= scriptToAttach.Length)
			{
				// No function
				return;
			}

			int functionColon = scriptToAttach.IndexOf(":", scriptColon + 1);
			int functionNameLength = functionColon - (scriptColon + 1);
			if(functionNameLength > 0)
			{
				string scriptFunction = scriptToAttach.Substring(scriptColon + 1, functionNameLength).Trim();

				if (functionColon + 1 < scriptToAttach.Length)
				{
					// Get argument
					string scriptArgument = scriptToAttach.Substring(functionColon + 1).Trim();
					//Debug.LogFormat("Invoking script function {0} with argument {1}", scriptFunction, scriptArgument);
					component.SendMessage(scriptFunction, scriptArgument, SendMessageOptions.DontRequireReceiver);
				}
				else
				{
					// No argument
					//Debug.LogFormat("Invoking script function {0}", scriptFunction);
					component.SendMessage(scriptFunction, SendMessageOptions.DontRequireReceiver);
				}
			}
		}

		/// <summary>
		/// Process the part at the given index, creating its data (geometry),
		/// and adding it to the list of parts.
		/// </summary>
		/// <param name="session"></param>
		/// <param name="partID"></param>
		/// <returns>A valid HEU_PartData if it has been successfully processed.</returns>
		private void ProcessPart(HEU_SessionBase session, int partID, ref HAPI_PartInfo partInfo, ref HEU_PartData partData)
		{
			HEU_HoudiniAsset parentAsset = ParentAsset;
			bool bResult = true;
			//Debug.LogFormat("Part: name={0}, id={1}, type={2}, instanced={3}, instance count={4}, instance part count={5}", HEU_SessionManager.GetString(partInfo.nameSH, session), partID, partInfo.type, partInfo.isInstanced, partInfo.instanceCount, partInfo.instancedPartCount);

#if HEU_PROFILER_ON
			float processPartStartTime = Time.realtimeSinceStartup;
#endif

			bool isPartEditable = IsIntermediateOrEditable();

			if (IsGeoInputType())
			{
				// Setup for input node to accept inputs
				if (_inputNode == null)
				{
					string partName = HEU_SessionManager.GetString(partInfo.nameSH, session);
					_inputNode = HEU_InputNode.CreateSetupInput(GeoID, 0, partName, HEU_InputNode.InputNodeType.NODE, ParentAsset);
					if(_inputNode != null)
					{
						ParentAsset.AddInputNode(_inputNode);
					}
				}

				if(partInfo.type == HAPI_PartType.HAPI_PARTTYPE_MESH && partInfo.vertexCount == 0)
				{
					// No geometry for input asset

					if (partData != null)
					{
						// Clean up existing part
						HEU_PartData.DestroyPart(partData);
						partData = null;
					}

					// No need to process further since we don't have geometry
					return;
				}
			}

			if(partInfo.type == HAPI_PartType.HAPI_PARTTYPE_INVALID)
			{
				// Clean up invalid parts
				if (partData != null)
				{
					HEU_PartData.DestroyPart(partData);
					partData = null;
				}
			}
			else if (partInfo.type < HAPI_PartType.HAPI_PARTTYPE_MAX)
			{
				// Process the part based on type. Keep or ignore.

				// We treat parts of type curve as curves, along with geo nodes that are editable and type curves
				if (partInfo.type == HAPI_PartType.HAPI_PARTTYPE_CURVE)
				{
					if(partData == null)
					{
						partData = ScriptableObject.CreateInstance<HEU_PartData>();
					}

					partData.Initialize(session, partID, GeoID, _containerObjectNode.ObjectID, this, ref partInfo, HEU_PartData.PartOutputType.CURVE, isPartEditable);
					SetupGameObjectAndTransform(partData, parentAsset);
					partData.ProcessCurvePart(session);
				}
				else if (partInfo.type == HAPI_PartType.HAPI_PARTTYPE_VOLUME)
				{
					// We only process "height" volume parts. Other volume parts are ignored for now.

#if TERRAIN_SUPPORTED
					HAPI_VolumeInfo volumeInfo = new HAPI_VolumeInfo();
					bResult = session.GetVolumeInfo(GeoID, partID, ref volumeInfo);
					if (!bResult)
					{
						Debug.LogErrorFormat("Unable to get volume info for geo node {0} and part {1} ", GeoID, partID);
					}
					else
					{
						if(Displayable && !IsIntermediateOrEditable())
						{
							if (partData == null)
							{
								partData = ScriptableObject.CreateInstance<HEU_PartData>();
							}

							partData.Initialize(session, partID, GeoID, _containerObjectNode.ObjectID, this, ref partInfo, HEU_PartData.PartOutputType.VOLUME, isPartEditable);
							SetupGameObjectAndTransform(partData, ParentAsset);
						}
					}
#else
					Debug.LogWarningFormat("Terrain (heightfield volume) is not yet supported.");
#endif
				}
				else if (partInfo.type == HAPI_PartType.HAPI_PARTTYPE_MESH)
				{
					if (partData == null)
					{
						partData = ScriptableObject.CreateInstance<HEU_PartData>();
					}

					partData.Initialize(session, partID, GeoID, _containerObjectNode.ObjectID, this, ref partInfo, HEU_PartData.PartOutputType.MESH, isPartEditable);
					SetupGameObjectAndTransform(partData, parentAsset);
				}
				else if (partInfo.type == HAPI_PartType.HAPI_PARTTYPE_INSTANCER)
				{
					if (partData == null)
					{
						partData = ScriptableObject.CreateInstance<HEU_PartData>();
					}

					partData.Initialize(session, partID, GeoID, _containerObjectNode.ObjectID, this, ref partInfo, HEU_PartData.PartOutputType.INSTANCER, isPartEditable);
					SetupGameObjectAndTransform(partData, parentAsset);
				}
				else
				{
					Debug.LogWarningFormat("Unsupported part type {0}", partInfo.type);
				}

				if (partData != null)
				{
					// Success!
					_parts.Add(partData);

					// Set unique name for the part
					string partFullName = GeneratePartFullName(partData.PartName);
					partFullName = HEU_EditorUtility.GetUniqueNameForSibling(ParentAsset.RootGameObject.transform, partFullName);
					partData.SetGameObjectName(partFullName);

					// For intermediate or default-type editable nodes, setup the HEU_AttributeStore
					if(isPartEditable)
					{
						partData.SyncAttributesStore(session, _geoInfo.nodeId, ref partInfo);
					}
					else
					{
						// Remove attributes store if it has it
						partData.DestroyAttributesStore();
					}

					HEU_GeneralUtility.AssignUnityTag(session, GeoID, partData.PartID, partData._gameObject);
					HEU_GeneralUtility.MakeStaticIfHasAttribute(session, GeoID, partData.PartID, partData._gameObject);
				}
			}

#if HEU_PROFILER_ON
			Debug.LogFormat("PART PROCESS TIME:: NAME={0}, TIME={1}", HEU_SessionManager.GetString(partInfo.nameSH, session), (Time.realtimeSinceStartup - processPartStartTime));
#endif
		}

		private void SetupGameObjectAndTransform(HEU_PartData partData, HEU_HoudiniAsset parentAsset)
		{
			// Set a valid gameobject for this part
			if(partData._gameObject == null)
			{
				partData._gameObject = new GameObject();
			}

			// The parent is either the asset root, OR if this is instanced and not visible, then the HDA data is the parent
			// The parent transform is either the asset root (for a display node),
			// or the HDA_Data gameobject (for instanced, not visible, intermediate, editable non-display nodes)
			Transform partTransform = partData._gameObject.transform;
			if (partData.IsPartInstanced() 
				|| (_containerObjectNode.IsInstanced() && !_containerObjectNode.IsVisible())
				|| partData.IsPartCurve()
				|| (IsIntermediateOrEditable() && !Displayable))
			{
				partTransform.parent = parentAsset.OwnerGameObject.transform;
			}
			else
			{
				partTransform.parent = parentAsset.RootGameObject.transform;
			}

			partData._gameObject.isStatic = partTransform.parent.gameObject.isStatic;

			// Reset to origin
			partTransform.localPosition = Vector3.zero;
			partTransform.localRotation = Quaternion.identity;
			partTransform.localScale = Vector3.one;
		}

		public void GetPartsByOutputType(List<HEU_PartData> meshParts, List<HEU_PartData> volumeParts)
		{
			int numParts = _parts.Count;
			for (int i = 0; i < numParts; ++i)
			{
				HEU_PartData part = _parts[i];
				if (part.IsPartMesh() || part.IsPartCurve())
				{
					meshParts.Add(part);
				}
				else if (part.IsPartVolume())
				{
					volumeParts.Add(part);
				}
			}
		}

		/// <summary>
		/// Generate the instances of instancer parts.
		/// </summary>
		public void GeneratePartInstances(HEU_SessionBase session)
		{
			int numParts = _parts.Count;
			for (int i = 0; i < numParts; ++i)
			{
				if (_parts[i].IsPartInstancer() && !_parts[i].HaveInstancesBeenGenerated())
				{
					_parts[i].GeneratePartInstances(session);
				}
			}
		}

		private void ProcessGeoCurve(HEU_SessionBase session)
		{
			HEU_HoudiniAsset parentAsset = ParentAsset;

			string curveName = GenerateGeoCurveName();
			curveName = HEU_EditorUtility.GetUniqueNameForSibling(ParentAsset.RootGameObject.transform, curveName);

			if (_geoCurve == null)
			{
				// New geo curve
				_geoCurve = HEU_Curve.CreateSetupCurve(parentAsset, Editable, curveName, GeoID, true);
			}
			else
			{
				_geoCurve.UploadParameterPreset(session, GeoID, parentAsset);
			}

			SetupGeoCurveGameObjectAndTransform(_geoCurve);
			_geoCurve.SetCurveName(curveName);

			_geoCurve.SyncFromParameters(session, parentAsset);

			// If geo node has part, generate the mesh using position attribute. 
			// Note that without any parts we can't generate a mesh so we pass in an invalid part ID
			// to at least set default values.
			HAPI_PartId partID = _geoInfo.partCount > 0 ? 0 : HEU_Defines.HEU_INVALID_NODE_ID;
			_geoCurve.UpdateCurve(session, partID);
			_geoCurve.GenerateMesh(_geoCurve._targetGameObject);

			bool bIsVisible = IsVisible() && HEU_PluginSettings.Curves_ShowInSceneView;
			_geoCurve.SetCurveGeometryVisibility(bIsVisible);
		}

		private void SetupGeoCurveGameObjectAndTransform(HEU_Curve curve)
		{
			if (curve._targetGameObject == null)
			{
				curve._targetGameObject = new GameObject();
			}

			// For geo curve, the parent is the HDA_Data
			Transform curveTransform = curve._targetGameObject.transform;
			curveTransform.parent = ParentAsset.OwnerGameObject.transform;

			curve._targetGameObject.isStatic = curveTransform.parent.gameObject.isStatic;

			// Reset to origin
			curveTransform.localPosition = Vector3.zero;
			curveTransform.localRotation = Quaternion.identity;
			curveTransform.localScale = Vector3.one;
		}

		/// <summary>
		/// Clear object instances so that they can be re-created
		/// </summary>
		public void ClearObjectInstances()
		{
			int numParts = _parts.Count;
			for (int i = 0; i < numParts; ++i)
			{
				_parts[i].ObjectInstancesBeenGenerated = false;
			}
		}

		public void SetGeoInfo(HAPI_GeoInfo geoInfo)
		{
			_geoInfo = geoInfo;
		}

		/// <summary>
		/// Returns the part's full name to set on the gamepboject
		/// </summary>
		/// <param name="partName"></param>
		/// <returns></returns>
		public string GeneratePartFullName(string partName)
		{
			return _containerObjectNode.ObjectName + "_" + GeoName + "_" + partName;
		}

		public string GenerateGeoCurveName()
		{
			return "GeoCurve";
		}

		/// <summary>
		/// Returns true if any of the geo data has changed and 
		/// therefore requires regeneration.
		/// </summary>
		/// <returns>True if changes occurred internal to geo data</returns>
		public bool HasGeoNodeChanged(HEU_SessionBase session)
		{
			// Note: _geoInfo.hasMaterialChanged has been deprecated so we're not checking that

			if (!session.GetGeoInfo(GeoID, ref _geoInfo))
			{
				return false;
			}
			else if (_geoInfo.type == HAPI_GeoType.HAPI_GEOTYPE_INPUT)
			{
				return (_inputNode != null) ? _inputNode.RequiresCook : false;
			}
			else if (_geoInfo.isTemplated && !HEU_PluginSettings.CookTemplatedGeos && !_geoInfo.isEditable)
			{
				return false;
			}
			else if (!_geoInfo.hasGeoChanged)
			{
				return false;
			}
			// Commented out to allow non-display geo to be updated
			//else if (!_geoInfo.isDisplayGeo && (_geoInfo.type != HAPI_GeoType.HAPI_GEOTYPE_CURVE && !HEU_PluginSettings.CookTemplatedGeos && _geoInfo.isTemplated))
			//{
			//	return false;
			//}

			return true;
		}

		/// <summary>
		/// Apply the given HAPI transform to all parts of this node.
		/// </summary>
		/// <param name="hapiTransform">HAPI transform to apply</param>
		public void ApplyHAPITransform(ref HAPI_Transform hapiTransform)
		{
			foreach(HEU_PartData part in _parts)
			{
				part.ApplyHAPITransform(ref hapiTransform);
			}
		}

		/// <summary>
		/// Get debug info for this geo
		/// </summary>
		public void GetDebugInfo(StringBuilder sb)
		{
			int numParts = _parts != null ? _parts.Count : 0;

			sb.AppendFormat("GeoID: {0}, Name: {1}, Type: {2}, Displayable: {3}, Editable: {4}, Parts: {5}, Parent: {6}\n", GeoID, GeoName, GeoType, Displayable, Editable, numParts, ParentAsset);

			if (_parts != null)
			{
				foreach (HEU_PartData part in _parts)
				{
					part.GetDebugInfo(sb);
				}
			}
		}

		/// <summary>
		/// Returns true if this geonode is using the given material.
		/// </summary>
		/// <param name="materialData">Material data containing the material to check</param>
		/// <returns>True if this geonode is using the given material</returns>
		public bool IsUsingMaterial(HEU_MaterialData materialData)
		{
			foreach (HEU_PartData part in _parts)
			{
				if(part.IsUsingMaterial(materialData))
				{
					return true;
				}
			}
			return false;
		}

		public void GetClonableParts(List<HEU_PartData> clonableParts)
		{
			foreach (HEU_PartData part in _parts)
			{
				part.GetClonableParts(clonableParts);
			}
		}

		/// <summary>
		/// Adds gameobjects that were output from this geo node.
		/// </summary>
		/// <param name="outputObjects">List to add to</param>
		public void GetOutputGameObjects(List<GameObject> outputObjects)
		{
			foreach (HEU_PartData part in _parts)
			{
				part.GetOutputGameObjects(outputObjects);
			}
		}

		/// <summary>
		/// Returns the HEU_PartData with the given output gameobject.
		/// </summary>
		/// <param name="outputGameObject">The output gameobject to check</param>
		/// <returns>Valid HEU_PartData or null if no match</returns>
		public HEU_PartData GetHDAPartWithGameObject(GameObject outputGameObject)
		{
			HEU_PartData foundPart = null;
			foreach (HEU_PartData part in _parts)
			{
				foundPart = part.GetHDAPartWithGameObject(outputGameObject);
				if (foundPart != null)
				{
					return foundPart;
				}
			}
			return null;
		}

		/// <summary>
		/// Returns contained part with specified partID
		/// </summary>
		/// <param name="partID">The node ID to match</param>
		/// <returns>The part with partID</returns>
		public HEU_PartData GetPartFromPartID(HAPI_NodeId partID)
		{
			int numParts = _parts.Count;
			for (int i = 0; i < numParts; ++i)
			{
				if (_parts[i].PartID == partID)
				{
					return _parts[i];
				}
			}
			return null;
		}

		public void GetCurves(List<HEU_Curve> curves, bool bEditableOnly)
		{
			if (_geoCurve != null && (!bEditableOnly || _geoCurve.IsEditable()))
			{
				curves.Add(_geoCurve);
			}

			foreach (HEU_PartData part in _parts)
			{
				HEU_Curve partCurve = part.GetCurve(bEditableOnly);
				if(partCurve != null)
				{
					curves.Add(partCurve);
				}
			}
		}

		public List<HEU_PartData> GetParts()
		{
			return _parts;
		}

		/// <summary>
		/// Calculate the visibility of this geo node and its parts, based on whether the parent is visible.
		/// </summary>
		/// <param name="bParentVisibility">True if parent is visible</param>
		public void CalculateVisiblity(bool bParentVisibility)
		{
			if (_geoCurve != null)
			{
				bool curveVisiblity = bParentVisibility && HEU_PluginSettings.Curves_ShowInSceneView;
				_geoCurve.SetCurveGeometryVisibility(curveVisiblity);
			}

			int numParts = _parts.Count;
			for (int i = 0; i < numParts; ++i)
			{
				_parts[i].CalculateVisibility(bParentVisibility, Displayable);
			}
		}

		/// <summary>
		/// Hide all geometry contained within
		/// </summary>
		public void HideAllGeometry()
		{
			int numParts = _parts.Count;
			for (int i = 0; i < numParts; ++i)
			{
				_parts[i].SetVisiblity(false);
			}
		}

		public void CalculateColliderState()
		{
			int numParts = _parts.Count;
			for (int i = 0; i < numParts; ++i)
			{
				_parts[i].CalculateColliderState();
			}
		}

		public void DisableAllColliders()
		{
			int numParts = _parts.Count;
			for (int i = 0; i < numParts; ++i)
			{
				_parts[i].SetColliderState(false);
			}
		}

		public override string ToString()
		{
			return (!string.IsNullOrEmpty(_geoName) ? ("GeoNode: " + _geoName) : base.ToString());
		}
	}

}   // HoudiniEngineUnity
						 
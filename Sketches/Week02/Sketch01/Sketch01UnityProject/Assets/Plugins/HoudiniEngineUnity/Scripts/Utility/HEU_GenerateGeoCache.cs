﻿/*
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

using UnityEngine;
using System;
using System.Collections.Generic;


namespace HoudiniEngineUnity
{
	using HAPI_NodeId = System.Int32;
	using HAPI_PartId = System.Int32;
	using HAPI_StringHandle = System.Int32;


	/// <summary>
	/// Stores geometry and material info for a part that is then used to generate Unity geometry.
	/// </summary>
	public class HEU_GenerateGeoCache
	{
		public HAPI_NodeId GeoID { get { return _geoInfo.nodeId; } }
		public HAPI_PartId PartID { get { return _partInfo.id; } }

		public HAPI_GeoInfo _geoInfo;
		public HAPI_PartInfo _partInfo;

		public string _partName;

		public int[] _vertexList;

		public HAPI_NodeId[] _houdiniMaterialIDs;

		public bool _singleFaceUnityMaterial;
		public bool _singleFaceHoudiniMaterial;

		public Dictionary<int, HEU_UnityMaterialInfo> _unityMaterialInfos;
		public HAPI_AttributeInfo _unityMaterialAttrInfo;
		public HAPI_StringHandle[] _unityMaterialAttrName;
		public Dictionary<HAPI_StringHandle, string> _unityMaterialAttrStringsMap = new Dictionary<HAPI_StringHandle, string>();

		public HAPI_AttributeInfo _substanceMaterialAttrNameInfo;
		public HAPI_StringHandle[] _substanceMaterialAttrName;
		public Dictionary<HAPI_StringHandle, string> _substanceMaterialAttrStringsMap = new Dictionary<HAPI_StringHandle, string>();

		public HAPI_AttributeInfo _substanceMaterialAttrIndexInfo;
		public int[] _substanceMaterialAttrIndex;

		public List<HEU_MaterialData> _inUseMaterials = new List<HEU_MaterialData>();

		public HAPI_AttributeInfo _posAttrInfo;
		public HAPI_AttributeInfo _uvAttrInfo;
		public HAPI_AttributeInfo _uv2AttrInfo;
		public HAPI_AttributeInfo _uv3AttrInfo;
		public HAPI_AttributeInfo _normalAttrInfo;
		public HAPI_AttributeInfo _colorAttrInfo;
		public HAPI_AttributeInfo _alphaAttrInfo;
		public HAPI_AttributeInfo _tangentAttrInfo;

		public float[] _posAttr;
		public float[] _uvAttr;
		public float[] _uv2Attr;
		public float[] _uv3Attr;
		public float[] _normalAttr;
		public float[] _colorAttr;
		public float[] _alphaAttr;
		public float[] _tangentAttr;

		public string[] _groups;
		public bool _hasGroupGeometry;

		public Dictionary<string, int[]> _groupSplitVertexIndices = new Dictionary<string, int[]>();
		public Dictionary<string, List<int>> _groupSplitFaceIndices = new Dictionary<string, List<int>>();

		public int[] _allCollisionVertexList;
		public int[] _allCollisionFaceIndices;

		public List<VertexEntry>[] _sharedNormalIndices;
		public float _cosineThreshold;



		public static HEU_GenerateGeoCache GetPopulatedGeoCache(HEU_SessionBase session, HAPI_NodeId geoID, HAPI_PartId partID)
		{
#if HEU_PROFILER_ON
			float generateGeoCacheStartTime = Time.realtimeSinceStartup;
#endif

			HEU_GenerateGeoCache geoCache = new HEU_GenerateGeoCache();

			Debug.Assert(geoID != HEU_Defines.HEU_INVALID_NODE_ID, "Invalid Geo ID! Unable to update materials!");
			Debug.Assert(partID != HEU_Defines.HEU_INVALID_NODE_ID, "Invalid Part ID! Unable to update materials!");

			geoCache._geoInfo = new HAPI_GeoInfo();
			if (!session.GetGeoInfo(geoID, ref geoCache._geoInfo))
			{
				return null;
			}

			geoCache._partInfo = new HAPI_PartInfo();
			if (!session.GetPartInfo(geoID, partID, ref geoCache._partInfo))
			{
				return null;
			}

			geoCache._partName = HEU_SessionManager.GetString(geoCache._partInfo.nameSH, session);

			// Unity max indices limitation!
#if UNITY_2017_3_OR_NEWER
			uint UNITY_MAX_VERTS = uint.MaxValue;
#else
			uint UNITY_MAX_VERTS = ushort.MaxValue;
#endif
			uint maxVertexCount = Convert.ToUInt32(geoCache._partInfo.vertexCount);
			if (maxVertexCount > UNITY_MAX_VERTS)
			{
				Debug.LogErrorFormat("Part {0} has vertex count of {1} which is above Unity maximum of {2}.\nUse Unity 2017.3+ or reduce this in Houdini.",
					geoCache._partName, maxVertexCount, UNITY_MAX_VERTS);
				return null;
			}

			geoCache._vertexList = new int[geoCache._partInfo.vertexCount];
			if (!HEU_GeneralUtility.GetArray2Arg(geoID, partID, session.GetVertexList, geoCache._vertexList, 0, geoCache._partInfo.vertexCount))
			{
				return null;
			}

			geoCache._houdiniMaterialIDs = new HAPI_NodeId[geoCache._partInfo.faceCount];
			if (!session.GetMaterialNodeIDsOnFaces(geoID, partID, ref geoCache._singleFaceHoudiniMaterial, geoCache._houdiniMaterialIDs, geoCache._partInfo.faceCount))
			{
				return null;
			}

			geoCache.PopulateUnityMaterialData(session);

			if (!geoCache.PopulateGeometryData(session))
			{
				return null;
			}

#if HEU_PROFILER_ON
			Debug.LogFormat("GENERATE GEO CACHE TIME:: {0}", (Time.realtimeSinceStartup - generateGeoCacheStartTime));
#endif

			return geoCache;
		}

		public void PopulateUnityMaterialData(HEU_SessionBase session)
		{
			// First we look for Unity and Substance material attributes on faces.
			// We fill up the following dictionary with unique Unity + Substance material information
			_unityMaterialInfos = new Dictionary<int, HEU_UnityMaterialInfo>();

			_unityMaterialAttrInfo = new HAPI_AttributeInfo();
			_unityMaterialAttrName = new HAPI_StringHandle[0];
			HEU_GeneralUtility.GetAttribute(session, GeoID, PartID, HEU_PluginSettings.UnityMaterialAttribName, ref _unityMaterialAttrInfo, ref _unityMaterialAttrName, session.GetAttributeStringData);

			// Store a local copy of the actual string values since the indices get overwritten by the next call to session.GetAttributeStringData.
			// Using a dictionary to only query the unique strings, as doing all of them is very slow and unnecessary.
			_unityMaterialAttrStringsMap = new Dictionary<HAPI_StringHandle, string>();
			foreach (HAPI_StringHandle strHandle in _unityMaterialAttrName)
			{
				if (!_unityMaterialAttrStringsMap.ContainsKey(strHandle))
				{
					string materialName = HEU_SessionManager.GetString(strHandle, session);
					if (string.IsNullOrEmpty(materialName))
					{
						// Warn user of empty string, but add it anyway to our map so we don't keep trying to parse it
						Debug.LogWarningFormat("Found empty material attribute value for part {0}.", _partName);
					}
					_unityMaterialAttrStringsMap.Add(strHandle, materialName);
					//Debug.LogFormat("Added Unity material: " + materialName);
				}
			}

			_substanceMaterialAttrNameInfo = new HAPI_AttributeInfo();
			_substanceMaterialAttrName = new HAPI_StringHandle[0];
			HEU_GeneralUtility.GetAttribute(session, GeoID, PartID, HEU_PluginSettings.UnitySubMaterialAttribName, ref _substanceMaterialAttrNameInfo, ref _substanceMaterialAttrName, session.GetAttributeStringData);

			_substanceMaterialAttrStringsMap = new Dictionary<HAPI_StringHandle, string>();
			foreach (HAPI_StringHandle strHandle in _substanceMaterialAttrName)
			{
				if (!_substanceMaterialAttrStringsMap.ContainsKey(strHandle))
				{
					string substanceName = HEU_SessionManager.GetString(strHandle, session);
					if (string.IsNullOrEmpty(substanceName))
					{
						// Warn user of empty string, but add it anyway to our map so we don't keep trying to parse it
						Debug.LogWarningFormat("Found invalid substance material attribute value ({0}) for part {1}.",
							_partName, substanceName);
					}
					_substanceMaterialAttrStringsMap.Add(strHandle, substanceName);
					//Debug.LogFormat("Added Substance material: " + substanceName);
				}
			}

			_substanceMaterialAttrIndexInfo = new HAPI_AttributeInfo();
			_substanceMaterialAttrIndex = new int[0];
			HEU_GeneralUtility.GetAttribute(session, GeoID, PartID, HEU_PluginSettings.UnitySubMaterialIndexAttribName, ref _substanceMaterialAttrIndexInfo, ref _substanceMaterialAttrIndex, session.GetAttributeIntData);


			if (_unityMaterialAttrInfo.exists)
			{
				if (_unityMaterialAttrInfo.owner == HAPI_AttributeOwner.HAPI_ATTROWNER_DETAIL && _unityMaterialAttrName.Length > 0)
				{
					CreateMaterialInfoEntryFromAttributeIndex(this, 0);

					// Detail unity material attribute means we can treat it as single material
					_singleFaceUnityMaterial = true;
				}
				else
				{
					for(HAPI_StringHandle i = 0; i < _unityMaterialAttrName.Length; ++i)
					{
						CreateMaterialInfoEntryFromAttributeIndex(this, i);
					}
				}
			}
		}

		public static int GetMaterialKeyFromAttributeIndex(HEU_GenerateGeoCache geoCache, int attributeIndex, out string unityMaterialName, out string substanceName, out int substanceIndex)
		{
			unityMaterialName = null;
			substanceName = null;
			substanceIndex = -1;
			if (attributeIndex < geoCache._unityMaterialAttrName.Length && geoCache._unityMaterialAttrStringsMap.TryGetValue(geoCache._unityMaterialAttrName[attributeIndex], out unityMaterialName))
			{
				if (geoCache._substanceMaterialAttrNameInfo.exists && geoCache._substanceMaterialAttrName.Length > 0)
				{
					geoCache._substanceMaterialAttrStringsMap.TryGetValue(geoCache._substanceMaterialAttrName[attributeIndex], out substanceName);
				}

				if (geoCache._substanceMaterialAttrIndexInfo.exists && string.IsNullOrEmpty(substanceName) && geoCache._substanceMaterialAttrIndex[attributeIndex] >= 0)
				{
					substanceIndex = geoCache._substanceMaterialAttrIndex[attributeIndex];
				}

				return HEU_MaterialFactory.GetUnitySubstanceMaterialKey(unityMaterialName, substanceName, substanceIndex);
			}
			return HEU_Defines.HEU_INVALID_MATERIAL;
		}

		public static void CreateMaterialInfoEntryFromAttributeIndex(HEU_GenerateGeoCache geoCache, int materialAttributeIndex)
		{
			string unityMaterialName = null;
			string substanceName = null;
			int substanceIndex = -1;
			int materialKey = GetMaterialKeyFromAttributeIndex(geoCache, materialAttributeIndex, out unityMaterialName, out substanceName, out substanceIndex);
			if (!geoCache._unityMaterialInfos.ContainsKey(materialKey))
			{
				geoCache._unityMaterialInfos.Add(materialKey, new HEU_UnityMaterialInfo(unityMaterialName, substanceName, substanceIndex));
			}
		}

		public bool PopulateGeometryData(HEU_SessionBase session)
		{
			// Get vertex position
			HAPI_AttributeInfo posAttrInfo = new HAPI_AttributeInfo();
			_posAttr = new float[0];
			HEU_GeneralUtility.GetAttribute(session, GeoID, PartID, HEU_Defines.HAPI_ATTRIB_POSITION, ref posAttrInfo, ref _posAttr, session.GetAttributeFloatData);
			if (!posAttrInfo.exists)
			{
				return false;
			}
			else if (posAttrInfo.owner != HAPI_AttributeOwner.HAPI_ATTROWNER_POINT)
			{
				Debug.LogErrorFormat("{0} only supports position as POINT attribute. Position attribute of {1} type not supported!", HEU_Defines.HEU_PRODUCT_NAME, posAttrInfo.owner);
				return false;
			}

			// Get UV attributes
			_uvAttrInfo = new HAPI_AttributeInfo();
			_uvAttrInfo.tupleSize = 2;
			_uvAttr = new float[0];
			HEU_GeneralUtility.GetAttribute(session, GeoID, PartID, HEU_Defines.HAPI_ATTRIB_UV, ref _uvAttrInfo, ref _uvAttr, session.GetAttributeFloatData);

			// Get UV2 attributes
			_uv2AttrInfo = new HAPI_AttributeInfo();
			_uv2AttrInfo.tupleSize = 2;
			_uv2Attr = new float[0];
			HEU_GeneralUtility.GetAttribute(session, GeoID, PartID, HEU_Defines.HAPI_ATTRIB_UV2, ref _uv2AttrInfo, ref _uv2Attr, session.GetAttributeFloatData);

			// Get UV3 attributes
			_uv3AttrInfo = new HAPI_AttributeInfo();
			_uv3AttrInfo.tupleSize = 2;
			_uv3Attr = new float[0];
			HEU_GeneralUtility.GetAttribute(session, GeoID, PartID, HEU_Defines.HAPI_ATTRIB_UV3, ref _uv3AttrInfo, ref _uv3Attr, session.GetAttributeFloatData);

			// Get normal attributes
			_normalAttrInfo = new HAPI_AttributeInfo();
			_normalAttr = new float[0];
			HEU_GeneralUtility.GetAttribute(session, GeoID, PartID, HEU_Defines.HAPI_ATTRIB_NORMAL, ref _normalAttrInfo, ref _normalAttr, session.GetAttributeFloatData);

			// Get colour attributes
			_colorAttrInfo = new HAPI_AttributeInfo();
			_colorAttr = new float[0];
			HEU_GeneralUtility.GetAttribute(session, GeoID, PartID, HEU_Defines.HAPI_ATTRIB_COLOR, ref _colorAttrInfo, ref _colorAttr, session.GetAttributeFloatData);

			// Get alpha attributes
			_alphaAttrInfo = new HAPI_AttributeInfo();
			_alphaAttr = new float[0];
			HEU_GeneralUtility.GetAttribute(session, GeoID, PartID, HEU_Defines.HAPI_ATTRIB_ALPHA, ref _alphaAttrInfo, ref _alphaAttr, session.GetAttributeFloatData);

			// Get tangent attributes
			_tangentAttrInfo = new HAPI_AttributeInfo();
			_tangentAttr = new float[0];
			HEU_GeneralUtility.GetAttribute(session, GeoID, PartID, HEU_Defines.HAPI_ATTRIB_TANGENT, ref _tangentAttrInfo, ref _tangentAttr, session.GetAttributeFloatData);

			// Warn user since we are splitting points by attributes, might prevent some attrributes
			// to be transferred over properly
			if (_normalAttrInfo.exists && _normalAttrInfo.owner != HAPI_AttributeOwner.HAPI_ATTROWNER_POINT
									&& _normalAttrInfo.owner != HAPI_AttributeOwner.HAPI_ATTROWNER_VERTEX)
			{
				Debug.LogWarningFormat("{0}: Normals are not declared as point or vertex attributes.\nSet them as per point or vertices in HDA.", _partName);
			}

			if (_tangentAttrInfo.exists && _tangentAttrInfo.owner != HAPI_AttributeOwner.HAPI_ATTROWNER_POINT
									&& _tangentAttrInfo.owner != HAPI_AttributeOwner.HAPI_ATTROWNER_VERTEX)
			{
				Debug.LogWarningFormat("{0}: Tangents are not declared as point or vertex attributes.\nSet them as per point or vertices in HDA.", _partName);
			}

			if (_colorAttrInfo.exists && _colorAttrInfo.owner != HAPI_AttributeOwner.HAPI_ATTROWNER_POINT
									&& _colorAttrInfo.owner != HAPI_AttributeOwner.HAPI_ATTROWNER_VERTEX)
			{
				Debug.LogWarningFormat("{0}: Colours are not declared as point or vertex attributes."
					+ "\nCurrently set as owner type {1}. Set them as per point or vertices in HDA.", _partName, _colorAttrInfo.owner);
			}

			_groups = HEU_SessionManager.GetGroupNames(GeoID, HAPI_GroupType.HAPI_GROUPTYPE_PRIM);

			_allCollisionVertexList = new int[_vertexList.Length];
			_allCollisionFaceIndices = new int[_partInfo.faceCount];

			_hasGroupGeometry = false;

			// We go through each group, building up a triangle list of indices that belong to it
			// For strictly colliders (ie. non-rendering), we only create geometry colliders 
			for (int g = 0; g < _groups.Length; ++g)
			{
				string groupName = _groups[g];

				// Query HAPI to get the group membership. 
				// This is returned as an array of 1s for vertices that belong to this group.
				int[] membership = null;
				HEU_SessionManager.GetGroupMembership(session, GeoID, PartID, HAPI_GroupType.HAPI_GROUPTYPE_PRIM, groupName, ref membership);

				bool bIsCollidable = groupName.Contains(HEU_PluginSettings.CollisionGroupName);
				bool bIsRenderCollidable = groupName.Contains(HEU_PluginSettings.RenderedCollisionGroupName);

				if (bIsCollidable || bIsRenderCollidable)
				{
					// Extract vertex indices for this collision group

					int[] groupVertexList = new int[_vertexList.Length];
					groupVertexList.Init<int>(-1);

					int groupVertexListCount = 0;

					List<int> allFaceList = new List<int>();
					for (int f = 0; f < membership.Length; ++f)
					{
						if (membership[f] > 0)
						{
							// This face is a member of the specified group

							allFaceList.Add(f);

							groupVertexList[f * 3 + 0] = _vertexList[f * 3 + 0];
							groupVertexList[f * 3 + 1] = _vertexList[f * 3 + 1];
							groupVertexList[f * 3 + 2] = _vertexList[f * 3 + 2];

							// Mark vertices as used
							_allCollisionVertexList[f * 3 + 0] = 1;
							_allCollisionVertexList[f * 3 + 1] = 1;
							_allCollisionVertexList[f * 3 + 2] = 1;

							// Mark face as used
							_allCollisionFaceIndices[f] = 1;

							groupVertexListCount += 3;
						}
					}

					if (groupVertexListCount > 0)
					{
						_groupSplitVertexIndices.Add(groupName, groupVertexList);
						_groupSplitFaceIndices.Add(groupName, allFaceList);

						_hasGroupGeometry = true;

						//Debug.Log("Adding collision group: " + groupName + " with index count: " + _groupVertexList.Length);
					}
				}
			}

			if (_hasGroupGeometry)
			{
				// Construct vertex list for all other vertices that are not part of collision geometry
				int[] remainingGroupSplitFaces = new int[_vertexList.Length];
				remainingGroupSplitFaces.Init<int>(-1);
				bool bMainSplitGroup = false;

				List<int> remainingGroupSplitFaceIndices = new List<int>();

				for (int cv = 0; cv < _allCollisionVertexList.Length; ++cv)
				{
					if (_allCollisionVertexList[cv] == 0)
					{
						// Unused index, so add it to unused vertex list
						remainingGroupSplitFaces[cv] = _vertexList[cv];
						bMainSplitGroup = true;
					}
				}

				for (int cf = 0; cf < _allCollisionFaceIndices.Length; ++cf)
				{
					if (_allCollisionFaceIndices[cf] == 0)
					{
						remainingGroupSplitFaceIndices.Add(cf);
					}
				}

				if (bMainSplitGroup)
				{
					_groupSplitVertexIndices.Add(HEU_Defines.HEU_DEFAULT_GEO_GROUP_NAME, remainingGroupSplitFaces);
					_groupSplitFaceIndices.Add(HEU_Defines.HEU_DEFAULT_GEO_GROUP_NAME, remainingGroupSplitFaceIndices);

					//Debug.Log("Adding remaining group with index count: " + remainingGroupSplitFaces.Length);
				}
			}
			else
			{
				_groupSplitVertexIndices.Add(HEU_Defines.HEU_DEFAULT_GEO_GROUP_NAME, _vertexList);

				List<int> allFaces = new List<int>();
				for (int f = 0; f < _partInfo.faceCount; ++f)
				{
					allFaces.Add(f);
				}
				_groupSplitFaceIndices.Add(HEU_Defines.HEU_DEFAULT_GEO_GROUP_NAME, allFaces);

				//Debug.Log("Adding single non-group with index count: " + _vertexList.Length);
			}

			// We'll be generating the normals if they're not provided by Houdini
			// For every vertex, we'll hold list of other vertices that it shares faces with (ie. connected to)
			_sharedNormalIndices = null;
			_cosineThreshold = 0f;
			if (!_normalAttrInfo.exists)
			{
				_sharedNormalIndices = new List<VertexEntry>[_vertexList.Length];
				_cosineThreshold = Mathf.Cos(HEU_PluginSettings.NormalGenerationThresholdAngle * Mathf.Deg2Rad);

				for (int i = 0; i < _vertexList.Length; ++i)
				{
					_sharedNormalIndices[i] = new List<VertexEntry>();
				}
			}

			return true;
		}

		public static int TransferRegularPointAttributesToVertices(int[] vertexList, ref HAPI_AttributeInfo attribInfo, float[] inData, ref float[] outData)
		{
			int validWedgeCount = 0;
			if (attribInfo.exists && attribInfo.tupleSize > 0)
			{
				int wedgeCount = vertexList.Length;

				// Re-indexed wedges
				outData = new float[wedgeCount * attribInfo.tupleSize];

				int lastValidWedgeIndex = 0;
				for (int wedgeIndex = 0; wedgeIndex < wedgeCount; ++wedgeIndex)
				{
					int vertexIndex = vertexList[wedgeIndex];
					if (vertexIndex == -1)
					{
						continue;
					}

					validWedgeCount++;

					int primIndex = wedgeIndex / 3;
					int saveIndex = 0;
					float value = 0;

					for (int attribIndex = 0; attribIndex < attribInfo.tupleSize; ++attribIndex)
					{
						switch (attribInfo.owner)
						{
							case HAPI_AttributeOwner.HAPI_ATTROWNER_POINT:
							{
								value = inData[vertexIndex * attribInfo.tupleSize + attribIndex];
								break;
							}
							case HAPI_AttributeOwner.HAPI_ATTROWNER_PRIM:
							{
								value = inData[primIndex * attribInfo.tupleSize + attribIndex];
								break;
							}
							case HAPI_AttributeOwner.HAPI_ATTROWNER_DETAIL:
							{
								value = inData[attribIndex];
								break;
							}
							case HAPI_AttributeOwner.HAPI_ATTROWNER_VERTEX:
							{
								value = inData[wedgeIndex * attribInfo.tupleSize + attribIndex];
								break;
							}
							default:
							{
								Debug.LogAssertion("Unsupported attribute owner " + attribInfo.owner);
								continue;
							}
						}

						saveIndex = lastValidWedgeIndex * attribInfo.tupleSize + attribIndex;
						outData[saveIndex] = value;
					}

					lastValidWedgeIndex++;
				}
			}

			return validWedgeCount;
		}

		public static bool GenerateMeshUsingGeoCache(HEU_SessionBase session, HEU_HoudiniAsset asset, GameObject gameObject,
			HEU_GenerateGeoCache geoCache, bool bGenerateUVs, bool bGenerateTangents, bool bPartInstanced)
		{
#if HEU_PROFILER_ON
			float generateMeshTime = Time.realtimeSinceStartup;
#endif

			string collisionGroupName = HEU_PluginSettings.CollisionGroupName;
			string renderCollisionGroupName = HEU_PluginSettings.RenderedCollisionGroupName;

			// Stores submesh data based on material key (ie. a submesh for each unique material)

			// Unity requires that if using multiple materials in the same GameObject, then we
			// need to create corresponding number of submeshes as materials.
			// So we'll create a submesh for each material in use. 
			// Each submesh will have a list of vertices and their attributes which
			// we'll collect in a helper class (HEU_MeshData).
			// Once we collected all the submesh data, we create a CombineInstance for each
			// submesh, then combine it while perserving the submeshes.
			Dictionary<int, HEU_MeshData> subMeshesMap = new Dictionary<int, HEU_MeshData>();

			string defaultMaterialName = HEU_HoudiniAsset.GenerateDefaultMaterialName(geoCache.GeoID, geoCache.PartID);
			int defaultMaterialKey = HEU_MaterialFactory.MaterialNameToKey(defaultMaterialName);

			int singleFaceUnityMaterialKey = HEU_Defines.HEU_INVALID_MATERIAL;
			int singleFaceHoudiniMaterialKey = HEU_Defines.HEU_INVALID_MATERIAL;

			// Now go through each group data and acquire the vertex data.
			// We'll create the collider mesh rightaway and assign to the gameobject.
			int numCollisionMeshes = 0;
			foreach (KeyValuePair<string, int[]> groupSplitFacesPair in geoCache._groupSplitVertexIndices)
			{
				string groupName = groupSplitFacesPair.Key;
				int[] groupVertexList = groupSplitFacesPair.Value;

				bool bIsCollidable = groupName.Contains(collisionGroupName);
				bool bIsRenderCollidable = groupName.Contains(renderCollisionGroupName);
				if (bIsCollidable || bIsRenderCollidable)
				{
					if (numCollisionMeshes > 0)
					{
						Debug.LogWarningFormat("More than 1 collision mesh detected for part {0}.\nOnly a single collision mesh is supported per part.", geoCache._partName);
					}

					if (geoCache._partInfo.type == HAPI_PartType.HAPI_PARTTYPE_BOX)
					{
						// Box collider

						HAPI_BoxInfo boxInfo = new HAPI_BoxInfo();
						if (session.GetBoxInfo(geoCache.GeoID, geoCache.PartID, ref boxInfo))
						{
							BoxCollider boxCollider = HEU_GeneralUtility.GetOrCreateComponent<BoxCollider>(gameObject);

							boxCollider.center = new Vector3(-boxInfo.center[0], boxInfo.center[1], boxInfo.center[2]);
							boxCollider.size = new Vector3(boxInfo.size[0] * 2f, boxInfo.size[1] * 2f, boxInfo.size[2] * 2f);
							// TODO: Should we apply the box info rotation here to the box collider?
							//		 If so, it should be in its own gameobject?
						}
					}
					else if (geoCache._partInfo.type == HAPI_PartType.HAPI_PARTTYPE_SPHERE)
					{
						// Sphere collider

						HAPI_SphereInfo sphereInfo = new HAPI_SphereInfo();
						if (session.GetSphereInfo(geoCache.GeoID, geoCache.PartID, ref sphereInfo))
						{
							SphereCollider sphereCollider = HEU_GeneralUtility.GetOrCreateComponent<SphereCollider>(gameObject);

							sphereCollider.center = new Vector3(-sphereInfo.center[0], sphereInfo.center[1], sphereInfo.center[2]);
							sphereCollider.radius = sphereInfo.radius;
						}
					}
					else
					{
						// Mesh collider

						List<Vector3> collisionVertices = new List<Vector3>();
						for (int v = 0; v < groupVertexList.Length; ++v)
						{
							int index = groupVertexList[v];
							if (index >= 0 && index < geoCache._posAttr.Length)
							{
								collisionVertices.Add(new Vector3(-geoCache._posAttr[index * 3], geoCache._posAttr[index * 3 + 1], geoCache._posAttr[index * 3 + 2]));
							}
						}

						int[] collisionIndices = new int[collisionVertices.Count];
						for (int i = 0; i < collisionIndices.Length; ++i)
						{
							collisionIndices[i] = i;
						}

						Mesh collisionMesh = new Mesh();
#if UNITY_2017_3_OR_NEWER
						collisionMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
#endif
						collisionMesh.name = groupName;
						collisionMesh.vertices = collisionVertices.ToArray();
						collisionMesh.triangles = collisionIndices;
						collisionMesh.RecalculateBounds();

						MeshCollider meshCollider = HEU_GeneralUtility.GetOrCreateComponent<MeshCollider>(gameObject);
						meshCollider.sharedMesh = collisionMesh;
					}

					numCollisionMeshes++;
				}


				if (bIsCollidable && !bIsRenderCollidable)
				{
					continue;
				}

				// After this point, we'll be only processing renderable geometry

				// Transfer indices for each attribute from the single large list into group lists

				float[] groupColorAttr = new float[0];
				HEU_GenerateGeoCache.TransferRegularPointAttributesToVertices(groupVertexList, ref geoCache._colorAttrInfo, geoCache._colorAttr, ref groupColorAttr);

				float[] groupAlphaAttr = new float[0];
				HEU_GenerateGeoCache.TransferRegularPointAttributesToVertices(groupVertexList, ref geoCache._alphaAttrInfo, geoCache._alphaAttr, ref groupAlphaAttr);

				float[] groupNormalAttr = new float[0];
				HEU_GenerateGeoCache.TransferRegularPointAttributesToVertices(groupVertexList, ref geoCache._normalAttrInfo, geoCache._normalAttr, ref groupNormalAttr);

				float[] groupTangentsAttr = new float[0];
				HEU_GenerateGeoCache.TransferRegularPointAttributesToVertices(groupVertexList, ref geoCache._tangentAttrInfo, geoCache._tangentAttr, ref groupTangentsAttr);

				float[] groupUVAttr = new float[0];
				HEU_GenerateGeoCache.TransferRegularPointAttributesToVertices(groupVertexList, ref geoCache._uvAttrInfo, geoCache._uvAttr, ref groupUVAttr);

				float[] groupUV2Attr = new float[0];
				HEU_GenerateGeoCache.TransferRegularPointAttributesToVertices(groupVertexList, ref geoCache._uv2AttrInfo, geoCache._uv2Attr, ref groupUV2Attr);

				float[] groupUV3Attr = new float[0];
				HEU_GenerateGeoCache.TransferRegularPointAttributesToVertices(groupVertexList, ref geoCache._uv3AttrInfo, geoCache._uv3Attr, ref groupUV3Attr);

				// Unity mesh creation requires # of vertices must equal # of attributes (color, normal, uvs).
				// HAPI gives us point indices. Since our attributes are via vertex, we need to therefore
				// create new indices of vertices that correspond to our attributes.

				// To reindex, we go through each index, add each attribute corresponding to that index to respective lists.
				// Then we set the index of where we added those attributes as the new index.

				int numIndices = groupVertexList.Length;
				for (int vertexIndex = 0; vertexIndex < numIndices; vertexIndex += 3)
				{
					// groupVertexList contains -1 for unused indices, and > 0 for used
					if (groupVertexList[vertexIndex] == -1)
					{
						continue;
					}

					int faceIndex = vertexIndex / 3;
					int faceMaterialID = geoCache._houdiniMaterialIDs[faceIndex];

					// Get the submesh ID for this face. Depends on whether it is a Houdini or Unity material.
					// Using default material as failsafe
					int submeshID = HEU_Defines.HEU_INVALID_MATERIAL;

					if (geoCache._unityMaterialAttrInfo.exists)
					{
						// This face might have a Unity or Substance material attribute. 
						// Formulate the submesh ID by combining the material attributes.

						if (geoCache._singleFaceUnityMaterial)
						{
							if (singleFaceUnityMaterialKey == HEU_Defines.HEU_INVALID_MATERIAL && geoCache._unityMaterialInfos.Count > 0)
							{
								// Use first material
								var unityMaterialMapEnumerator = geoCache._unityMaterialInfos.GetEnumerator();
								if (unityMaterialMapEnumerator.MoveNext())
								{
									singleFaceUnityMaterialKey = unityMaterialMapEnumerator.Current.Key;
								}
							}
							submeshID = singleFaceUnityMaterialKey;
						}
						else
						{
							int attrIndex = faceIndex;
							if (geoCache._unityMaterialAttrInfo.owner == HAPI_AttributeOwner.HAPI_ATTROWNER_PRIM || geoCache._unityMaterialAttrInfo.owner == HAPI_AttributeOwner.HAPI_ATTROWNER_POINT)
							{
								if (geoCache._unityMaterialAttrInfo.owner == HAPI_AttributeOwner.HAPI_ATTROWNER_POINT)
								{
									attrIndex = groupVertexList[vertexIndex];
								}

								string unityMaterialName = "";
								string substanceName = "";
								int substanceIndex = -1;
								submeshID = HEU_GenerateGeoCache.GetMaterialKeyFromAttributeIndex(geoCache, attrIndex, out unityMaterialName, out substanceName, out substanceIndex);
							}
							else
							{
								// (geoCache._unityMaterialAttrInfo.owner == HAPI_AttributeOwner.HAPI_ATTROWNER_DETAIL) should have been handled as geoCache._singleFaceMaterial above

								Debug.LogErrorFormat("Unity material attribute not supported for attribute type {0}!", geoCache._unityMaterialAttrInfo.owner);
							}
						}
					}

					if (submeshID == HEU_Defines.HEU_INVALID_MATERIAL)
					{
						// Check if has Houdini material assignment

						if (geoCache._houdiniMaterialIDs.Length > 0)
						{
							if (geoCache._singleFaceHoudiniMaterial)
							{
								if (singleFaceHoudiniMaterialKey == HEU_Defines.HEU_INVALID_MATERIAL)
								{
									singleFaceHoudiniMaterialKey = geoCache._houdiniMaterialIDs[0];
								}
								submeshID = singleFaceHoudiniMaterialKey;
							}
							else if (faceMaterialID > 0)
							{
								submeshID = faceMaterialID;
							}
						}

						if (submeshID == HEU_Defines.HEU_INVALID_MATERIAL)
						{
							// Use default material
							submeshID = defaultMaterialKey;
						}
					}

					if (!subMeshesMap.ContainsKey(submeshID))
					{
						// New submesh
						subMeshesMap.Add(submeshID, new HEU_MeshData());
					}

					HEU_MeshData subMeshData = subMeshesMap[submeshID];

					for (int triIndex = 0; triIndex < 3; ++triIndex)
					{
						int vertexTriIndex = vertexIndex + triIndex;
						int positionIndex = groupVertexList[vertexTriIndex];

						// Position
						Vector3 position = new Vector3(-geoCache._posAttr[positionIndex * 3 + 0], geoCache._posAttr[positionIndex * 3 + 1], geoCache._posAttr[positionIndex * 3 + 2]);
						subMeshData._vertices.Add(position);

						// Color
						if (geoCache._colorAttrInfo.exists)
						{
							Color tempColor = new Color();
							tempColor.r = Mathf.Clamp01(groupColorAttr[vertexTriIndex * geoCache._colorAttrInfo.tupleSize + 0]);
							tempColor.g = Mathf.Clamp01(groupColorAttr[vertexTriIndex * geoCache._colorAttrInfo.tupleSize + 1]);
							tempColor.b = Mathf.Clamp01(groupColorAttr[vertexTriIndex * geoCache._colorAttrInfo.tupleSize + 2]);

							if (geoCache._alphaAttrInfo.exists)
							{
								tempColor.a = Mathf.Clamp01(groupAlphaAttr[vertexTriIndex]);
							}
							else if (geoCache._colorAttrInfo.tupleSize == 4)
							{
								tempColor.a = Mathf.Clamp01(groupColorAttr[vertexTriIndex * geoCache._colorAttrInfo.tupleSize + 3]);
							}
							else
							{
								tempColor.a = 1f;
							}
							subMeshData._colors.Add(tempColor);
						}
						else
						{
							subMeshData._colors.Add(Color.white);
						}

						// Normal
						if (vertexTriIndex < groupNormalAttr.Length)
						{
							// Flip the x
							Vector3 normal = new Vector3(-groupNormalAttr[vertexTriIndex * 3 + 0], groupNormalAttr[vertexTriIndex * 3 + 1], groupNormalAttr[vertexTriIndex * 3 + 2]);
							subMeshData._normals.Add(normal);
						}
						else
						{
							// We'll be calculating normals later
							subMeshData._normals.Add(Vector3.zero);
						}

						// UV1
						if (vertexTriIndex < groupUVAttr.Length)
						{
							Vector2 uv = new Vector2(groupUVAttr[vertexTriIndex * 2 + 0], groupUVAttr[vertexTriIndex * 2 + 1]);
							subMeshData._UVs.Add(uv);
						}

						// UV2
						if (vertexTriIndex < groupUV2Attr.Length)
						{
							Vector2 uv = new Vector2(groupUV2Attr[vertexTriIndex * 2 + 0], groupUV2Attr[vertexTriIndex * 2 + 1]);
							subMeshData._UV2s.Add(uv);
						}

						// UV3
						if (vertexTriIndex < groupUV3Attr.Length)
						{
							Vector2 uv = new Vector2(groupUV3Attr[vertexTriIndex * 2 + 0], groupUV3Attr[vertexTriIndex * 2 + 1]);
							subMeshData._UV3s.Add(uv);
						}

						// Tangents
						if (bGenerateTangents && vertexTriIndex < groupTangentsAttr.Length)
						{
							Vector4 tangent = Vector4.zero;
							if (geoCache._tangentAttrInfo.tupleSize == 4)
							{
								tangent = new Vector4(-groupTangentsAttr[vertexTriIndex * 4 + 0], groupTangentsAttr[vertexTriIndex * 4 + 1], groupTangentsAttr[vertexTriIndex * 4 + 2], groupTangentsAttr[vertexTriIndex * 4 + 3]);
							}
							else if (geoCache._tangentAttrInfo.tupleSize == 3)
							{
								tangent = new Vector4(-groupTangentsAttr[vertexTriIndex * 3 + 0], groupTangentsAttr[vertexTriIndex * 3 + 1], groupTangentsAttr[vertexTriIndex * 3 + 2], 1);
							}

							subMeshData._tangents.Add(tangent);
						}

						subMeshData._indices.Add(subMeshData._vertices.Count - 1);
						//Debug.LogFormat("Submesh index mat {0} count {1}", faceMaterialID, subMeshData._indices.Count);
					}

					if (!geoCache._normalAttrInfo.exists)
					{
						// To generate normals after all the submeshes have been defined, we
						// calculate and store each triangle normal, along with the list
						// of connected vertices for each vertex

						int triIndex = subMeshData._indices.Count - 3;
						int i1 = subMeshData._indices[triIndex + 0];
						int i2 = subMeshData._indices[triIndex + 1];
						int i3 = subMeshData._indices[triIndex + 2];

						// Triangle normal
						Vector3 p1 = subMeshData._vertices[i2] - subMeshData._vertices[i1];
						Vector3 p2 = subMeshData._vertices[i3] - subMeshData._vertices[i1];
						Vector3 normal = Vector3.Cross(p1, p2).normalized;
						subMeshData._triangleNormals.Add(normal);
						int normalIndex = subMeshData._triangleNormals.Count - 1;

						// Connected vertices
						geoCache._sharedNormalIndices[groupVertexList[vertexIndex + 0]].Add(new VertexEntry(submeshID, i1, normalIndex));
						geoCache._sharedNormalIndices[groupVertexList[vertexIndex + 1]].Add(new VertexEntry(submeshID, i2, normalIndex));
						geoCache._sharedNormalIndices[groupVertexList[vertexIndex + 2]].Add(new VertexEntry(submeshID, i3, normalIndex));
					}
				}
			}

			int numSubmeshes = subMeshesMap.Keys.Count;

			bool bGenerated = false;
			if (numSubmeshes > 0)
			{
				if (!geoCache._normalAttrInfo.exists)
				{
					// Normal calculation
					// Go throuch each vertex for the entire geometry and calculate the normal vector based on connected
					// vertices. This includes vertex connections between submeshes so we should get smooth transitions across submeshes.

					int numSharedNormals = geoCache._sharedNormalIndices.Length;
					for (int a = 0; a < numSharedNormals; ++a)
					{
						for (int b = 0; b < geoCache._sharedNormalIndices[a].Count; ++b)
						{
							Vector3 sumNormal = new Vector3();
							VertexEntry leftEntry = geoCache._sharedNormalIndices[a][b];
							HEU_MeshData leftSubMesh = subMeshesMap[leftEntry._meshKey];

							List<VertexEntry> rightList = geoCache._sharedNormalIndices[a];
							for (int c = 0; c < rightList.Count; ++c)
							{
								VertexEntry rightEntry = rightList[c];
								HEU_MeshData rightSubMesh = subMeshesMap[rightEntry._meshKey];

								if (leftEntry._vertexIndex == rightEntry._vertexIndex)
								{
									sumNormal += rightSubMesh._triangleNormals[rightEntry._normalIndex];
								}
								else
								{
									float dot = Vector3.Dot(leftSubMesh._triangleNormals[leftEntry._normalIndex],
										rightSubMesh._triangleNormals[rightEntry._normalIndex]);
									if (dot >= geoCache._cosineThreshold)
									{
										sumNormal += rightSubMesh._triangleNormals[rightEntry._normalIndex];
									}
								}
							}

							leftSubMesh._normals[leftEntry._vertexIndex] = sumNormal.normalized;
						}
					}
				}


				// Go through each valid submesh data and upload into a CombineInstance for combining.
				// Each CombineInstance represents a submesh in the final mesh.
				// And each submesh in that final mesh corresponds to a material.

				// Filter out only the submeshes with valid geometry
				List<Material> validMaterials = new List<Material>();
				List<int> validSubmeshes = new List<int>();

				Dictionary<int, HEU_MaterialData> assetMaterialMap = asset.GetMaterialDataMap();

				foreach (KeyValuePair<int, HEU_MeshData> meshPair in subMeshesMap)
				{
					HEU_MeshData meshData = meshPair.Value;
					if (meshData._indices.Count > 0)
					{
						int materialKey = meshPair.Key;

						// Find the material or create it
						HEU_MaterialData materialData = null;

						HEU_UnityMaterialInfo unityMaterialInfo = null;
						if (geoCache._unityMaterialInfos.TryGetValue(materialKey, out unityMaterialInfo))
						{
							if (!assetMaterialMap.TryGetValue(materialKey, out materialData))
							{
								// Create the material
								materialData = asset.CreateUnitySubstanceMaterialData(materialKey, unityMaterialInfo._unityMaterialPath, unityMaterialInfo._substancePath, unityMaterialInfo._substanceIndex);
								assetMaterialMap.Add(materialData._materialKey, materialData);
							}
						}
						else if (!assetMaterialMap.TryGetValue(materialKey, out materialData))
						{
							if (materialKey == defaultMaterialKey)
							{
								materialData = asset.GetOrCreateDefaultMaterialInCache(session, geoCache.GeoID, geoCache.PartID, false);
							}
							else
							{
								materialData = asset.CreateHoudiniMaterialData(session, materialKey, geoCache.GeoID, geoCache.PartID);
							}
						}

						if (materialData != null)
						{
							validSubmeshes.Add(meshPair.Key);
							validMaterials.Add(materialData._material);

							if (materialData != null && bPartInstanced)
							{
								// Handle GPU instancing on material for instanced meshes

								if (materialData._materialSource != HEU_MaterialData.Source.UNITY && materialData._materialSource != HEU_MaterialData.Source.SUBSTANCE)
								{
									// Always enable GPU instancing for material generated from Houdini
									HEU_MaterialFactory.EnableGPUInstancing(materialData._material);
								}
							}
						}
					}
				}

				int validNumSubmeshes = validSubmeshes.Count;
				CombineInstance[] meshCombiner = new CombineInstance[validNumSubmeshes];
				for (int submeshIndex = 0; submeshIndex < validNumSubmeshes; ++submeshIndex)
				{
					HEU_MeshData submesh = subMeshesMap[validSubmeshes[submeshIndex]];

					CombineInstance combine = new CombineInstance();
					combine.mesh = new Mesh();
#if UNITY_2017_3_OR_NEWER
					combine.mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
#endif

					combine.mesh.SetVertices(submesh._vertices);

					combine.mesh.SetIndices(submesh._indices.ToArray(), MeshTopology.Triangles, 0);

					if (submesh._colors.Count > 0)
					{
						combine.mesh.SetColors(submesh._colors);
					}

					if (submesh._normals.Count > 0)
					{
						combine.mesh.SetNormals(submesh._normals);
					}

					if (submesh._tangents.Count > 0)
					{
						combine.mesh.SetTangents(submesh._tangents);
					}

					if (bGenerateUVs)
					{
						// TODO: revisit to test this out
						Vector2[] generatedUVs = HEU_GeometryUtility.GeneratePerTriangle(combine.mesh);
						if (generatedUVs != null)
						{
							combine.mesh.uv = generatedUVs;
						}
					}
					else if (submesh._UVs.Count > 0)
					{
						combine.mesh.SetUVs(0, submesh._UVs);
					}

					if (submesh._UV2s.Count > 0)
					{
						combine.mesh.SetUVs(1, submesh._UV2s);
					}

					if (submesh._UV3s.Count > 0)
					{
						combine.mesh.SetUVs(2, submesh._UV3s);
					}

					combine.transform = Matrix4x4.identity;
					combine.mesh.RecalculateBounds();

					//Debug.LogFormat("Number of submeshes {0}", combine.mesh.subMeshCount);

					meshCombiner[submeshIndex] = combine;
				}

				// Geometry data
				MeshFilter meshFilter = HEU_GeneralUtility.GetOrCreateComponent<MeshFilter>(gameObject);
				meshFilter.sharedMesh = new Mesh();
#if UNITY_2017_3_OR_NEWER
				meshFilter.sharedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
#endif
				meshFilter.sharedMesh.name = geoCache._partName + "_mesh";
				meshFilter.sharedMesh.CombineMeshes(meshCombiner, false, false);

				meshFilter.sharedMesh.RecalculateBounds();

				if (!geoCache._tangentAttrInfo.exists && bGenerateTangents)
				{
					HEU_GeometryUtility.CalculateMeshTangents(meshFilter.sharedMesh);
				}

				meshFilter.sharedMesh.UploadMeshData(true);

				// Render data
				MeshRenderer meshRenderer = HEU_GeneralUtility.GetOrCreateComponent<MeshRenderer>(gameObject);
				meshRenderer.sharedMaterials = validMaterials.ToArray();

				bGenerated = true;
			}

#if HEU_PROFILER_ON
			Debug.LogFormat("GENERATE MESH TIME:: {0}", (Time.realtimeSinceStartup - generateMeshTime));
#endif

			return bGenerated;
		}

	}

	/// <summary>
	/// Helper used for storing vertex connections for normal generation
	/// </summary>
	public class VertexEntry
	{
		public int _meshKey;
		public int _vertexIndex;
		public int _normalIndex;

		public VertexEntry(int meshKey, int vertexIndex, int normalIndex)
		{
			_meshKey = meshKey;
			_vertexIndex = vertexIndex;
			_normalIndex = normalIndex;
		}
	}

}   // HoudiniEngineUnity
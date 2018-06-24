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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HoudiniEngineUnity
{
	public class HEU_VolumeData
	{
		public HEU_PartData _partData;
		public HAPI_VolumeInfo _volumeInfo;
	}

	/// <summary>
	/// Creates terrain out of volume parts.
	/// </summary>
	public class HEU_VolumeCache
	{
		// Refers to the volume data that will be used to generate the heightmap
		private HEU_VolumeData _heightMapVolumeData;

		// Refers to list of volume datas that will be used to generate textures
		private List<HEU_VolumeData> _textureVolumeDatas;
		
		public void GenerateTerrainFromParts(HEU_SessionBase session, List<HEU_PartData> volumeParts, HEU_HoudiniAsset houdiniAsset, out HEU_PartData heightLayerPart)
		{
			_heightMapVolumeData = null;
			_textureVolumeDatas = new List<HEU_VolumeData>();

			ParseVolumeDatas(session, volumeParts);

			TerrainData terrainData = null;
			Vector3 terrainOffsetPosition = Vector3.zero;
			Generate(session, houdiniAsset, out terrainData, out terrainOffsetPosition);

			if(_heightMapVolumeData != null && _heightMapVolumeData._partData != null && terrainData != null)
			{
				UnityEngine.Object terrainDataObject = null;
				houdiniAsset.AddToAssetDBCache(string.Format("Asset_{0}_TerrainData", _heightMapVolumeData._partData.ParentGeoNode.GeoName), terrainData, ref terrainDataObject);

				_heightMapVolumeData._partData.SetTerrainPart(terrainDataObject, terrainOffsetPosition);

				heightLayerPart = _heightMapVolumeData._partData;
			}
			else
			{
				heightLayerPart = null;
			}
		}

		private void ParseVolumeDatas(HEU_SessionBase session, List<HEU_PartData> volumeParts)
		{
			bool bResult;
			foreach (HEU_PartData part in volumeParts)
			{
				HEU_GeoNode geoNode = part.ParentGeoNode;

				HAPI_VolumeInfo volumeInfo = new HAPI_VolumeInfo();
				bResult = session.GetVolumeInfo(geoNode.GeoID, part.PartID, ref volumeInfo);
				if(!bResult || volumeInfo.tupleSize != 1 || volumeInfo.zLength != 1 || volumeInfo.storage != HAPI_StorageType.HAPI_STORAGETYPE_FLOAT)
				{
					continue;
				}

				string volumeName = HEU_SessionManager.GetString(volumeInfo.nameSH, session);

				//Debug.LogFormat("Part name: {0}, GeoName: {1}, Volume Name: {2}, Display: {3}", part.PartName, geoNode.GeoName, volumeName, geoNode.Displayable);

				if(volumeName.Equals("height"))
				{
					if (_heightMapVolumeData == null)
					{
						_heightMapVolumeData = new HEU_VolumeData();
						_heightMapVolumeData._partData = part;
						_heightMapVolumeData._volumeInfo = volumeInfo;
					}
				}
				else
				{
					HEU_VolumeData volumeData = new HEU_VolumeData();
					volumeData._partData = part;
					volumeData._volumeInfo = volumeInfo;
					_textureVolumeDatas.Add(volumeData);
				}
			}
		}

		private void Generate(HEU_SessionBase session, HEU_HoudiniAsset houdiniAsset, out TerrainData terrainData, out Vector3 terrainOffsetPosition)
		{
			terrainData = null;
			terrainOffsetPosition = Vector3.zero;

			if (_heightMapVolumeData == null)
			{
				Debug.LogError("Unable to generate terrain due to not finding heightfield with display flag!");
				return;
			}

			// Generate the terrain and terrain data from the heightmap
			bool bResult = HEU_GeometryUtility.GenerateTerrainFromVolume(session, ref _heightMapVolumeData._volumeInfo, _heightMapVolumeData._partData.ParentGeoNode.GeoID,
				_heightMapVolumeData._partData.PartID, _heightMapVolumeData._partData._gameObject, out terrainData, out terrainOffsetPosition);
			if(!bResult)
			{
				return;
			}

			int terrainSize = terrainData.heightmapResolution;

			/*
			// Now set the alphamaps (textures) for the other layers
			// First, preprocess all volumes to get heightfield arrays, converted to proper size
			// Then, merge into a float[x,y,map]
			List<float[]> heightFields = new List<float[]>();
			foreach(HEU_VolumeData volumeData in _textureVolumeDatas)
			{
				float[] hf = GetHeightfield(session, volumeData, terrainSize);
				if(hf != null && hf.Length > 0)
				{
					heightFields.Add(hf);
				}
			}
			
			// Assign floats to map
			float[,,] alphamap = new float[terrainSize, terrainSize, heightFields.Count];
			for (int y = 0; y < terrainSize; ++y)
			{
				for (int x = 0; x < terrainSize; ++x)
				{
					for(int m = 0; m < terrainSize; ++m)
					{
						alphamap[x, y, m] = heightFields[m][y * terrainSize + x];
					}
				}
			}

			terrainData.SetAlphamaps(0, 0, alphamap);
			*/
		}

		private float[] GetHeightfield(HEU_SessionBase session, HEU_VolumeData volumeData, int terrainSize)
		{
			int xLength = volumeData._volumeInfo.xLength;
			int yLength = volumeData._volumeInfo.yLength;

			// Number of heightfield values
			int totalHeightValues = xLength * yLength;

			float[] heightValues = new float[totalHeightValues];
			bool bResult = session.GetHeightFieldData(volumeData._partData.ParentGeoNode.GeoID, volumeData._partData.PartID, heightValues, 0, totalHeightValues);
			if (!bResult)
			{
				Debug.LogErrorFormat("Unable to get heightfield data from part {0}", volumeData._partData.PartName);
				return heightValues;
			}

			// Convert to terrain size
			if(xLength == terrainSize && yLength == terrainSize)
			{
				return heightValues;
			}
			else
			{
				int paddingWidth = terrainSize - xLength;
				int paddingLeft = Mathf.CeilToInt(paddingWidth * 0.5f);
				int paddingRight = terrainSize - paddingLeft;
				//Debug.LogFormat("Padding: Width={0}, Left={1}, Right={2}", paddingWidth, paddingLeft, paddingRight);

				int paddingHeight = terrainSize - yLength;
				int paddingTop = Mathf.CeilToInt(paddingHeight * 0.5f);
				int paddingBottom = terrainSize - paddingTop;
				//Debug.LogFormat("Padding: Height={0}, Top={1}, Bottom={2}", paddingHeight, paddingTop, paddingBottom);

				// Set height values at centre of the terrain, with padding on the sides if we resized
				float[] resizedHeightValues = new float[terrainSize * terrainSize];
				for (int y = 0; y < terrainSize; ++y)
				{
					for (int x = 0; x < terrainSize; ++x)
					{
						if (y >= paddingTop && y < (paddingBottom) && x >= paddingLeft && x < (paddingRight))
						{
							int ay = x - paddingLeft;
							int ax = y - paddingTop;

							//float f = heightValues[ay + ax * xLength];

							// Flip for right-hand to left-handed coordinate system
							//int ix = x;
							//int iy = squareSizePlusOne - (y + 1);

							// Unity expects height array indexing to be [y, x].
							//unityHeights[ix, iy] = f;
						}
					}
				}

				return resizedHeightValues;
			}
		}

		
	}

}   // HoudiniEngineUnity
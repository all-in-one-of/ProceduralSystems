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

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace HoudiniEngineUnity
{
	/////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	// Typedefs (copy these from HEU_Common.cs)
	using HAPI_NodeId = System.Int32;
	using HAPI_StringHandle = System.Int32;
	using HAPI_ParmId = System.Int32;
	using HAPI_PartId = System.Int32;


	/// <summary>
	/// Holds all parameter data for an asset.
	/// </summary>
	public class HEU_Parameters : ScriptableObject
	{
		//	DATA ------------------------------------------------------------------------------------------------------

		public HAPI_NodeId _nodeID;

		public string _uiLabel = "ASSET PARAMETERS";

		public int[] _paramInts;
		public float[] _paramFloats;
		public string[] _paramStrings;
		public HAPI_ParmChoiceInfo[] _paramChoices;

		// Hierarychy list (for UI)		
		[SerializeField]
		private List<int> _rootParameters = new List<int>();

		[SerializeField]
		private List<HEU_ParameterData> _parameterList = new List<HEU_ParameterData>();

		[SerializeField]
		private List<HEU_ParameterModifier> _parameterModifiers = new List<HEU_ParameterModifier>();

		// If true, need to recreate the parameters by querying HAPI.
		// Should be called after inserting or removing an multiparm instance.
		[SerializeField]
		private bool _regenerateParameters;

		public bool RequiresRegeneration { get { return _regenerateParameters; } set { _regenerateParameters = value; } }

		// Cache the parameter preset. This is reloaded back into Houdini after scene deserialization.
		[SerializeField]
		private byte[] _presetData;

		public byte[] GetPresetData() { return _presetData; }

		public void SetPresetData(byte[] data) { _presetData = data; }

		// Specifies whether the parameters are in a valid state to interact with Houdini
		[SerializeField]
		private bool _validParameters;

		public bool AreParametersValid() { return _validParameters; }

		// Disable the warning for unused variable. We're accessing this as a SerializedProperty.
#pragma warning disable 0414

		[SerializeField]
		private bool _showParameters = true;

#pragma warning restore 0414

		// Flag that the UI needs to be recached. Should be done whenever any of the parameters change.
		//[SerializeField]
		private bool _recacheUI = true;

		public bool RecacheUI { get { return _recacheUI; } set { _recacheUI = value; } }


		//	LOGIC -----------------------------------------------------------------------------------------------------

		public void CleanUp()
		{
			//Debug.Log("Cleaning up parameters!");
			_validParameters = false;
			_regenerateParameters = false;

			_rootParameters = new List<int>();
			_parameterList = new List<HEU_ParameterData>();
			_parameterModifiers = new List<HEU_ParameterModifier>();

			_paramInts = null;
			_paramFloats = null;
			_paramStrings = null;
			_paramChoices = null;

			_presetData = null;
		}

		public bool Initialize(HEU_SessionBase session, HAPI_NodeId nodeID, ref HAPI_NodeInfo nodeInfo, 
			Dictionary<string, HEU_ParameterData> previousParamFolders, Dictionary<string, HEU_InputNode> previousParamInputNodes,
			HEU_HoudiniAsset parentAsset)
		{
			_nodeID = nodeID;

			HAPI_ParmInfo[] parmInfos = new HAPI_ParmInfo[nodeInfo.parmCount];
			if (!HEU_GeneralUtility.GetArray1Arg(nodeID, session.GetParams, parmInfos, 0, nodeInfo.parmCount))
			{
				return false;
			}

			_rootParameters = new List<int>();
			_parameterList = new List<HEU_ParameterData>();
			Dictionary<HAPI_NodeId, HEU_ParameterData> parameterMap = new Dictionary<HAPI_NodeId, HEU_ParameterData>();

			// Load in all the parameter values.

			_paramInts = new int[nodeInfo.parmIntValueCount];
			if (!HEU_GeneralUtility.GetArray1Arg(nodeID, session.GetParamIntValues, _paramInts, 0, nodeInfo.parmIntValueCount))
			{
				return false;
			}

			_paramFloats = new float[nodeInfo.parmFloatValueCount];
			if (!HEU_GeneralUtility.GetArray1Arg(nodeID, session.GetParamFloatValues, _paramFloats, 0, nodeInfo.parmFloatValueCount))
			{
				return false;
			}

			HAPI_StringHandle[] parmStringHandles = new HAPI_StringHandle[nodeInfo.parmStringValueCount];
			if (!HEU_GeneralUtility.GetArray1Arg(nodeID, session.GetParamStringValues, parmStringHandles, 0, nodeInfo.parmStringValueCount))
			{
				return false;
			}
			// Convert to actual strings
			_paramStrings = new string[nodeInfo.parmStringValueCount];
			for (int s = 0; s < nodeInfo.parmStringValueCount; ++s)
			{
				_paramStrings[s] = HEU_SessionManager.GetString(parmStringHandles[s], session);
			}

			_paramChoices = new HAPI_ParmChoiceInfo[nodeInfo.parmChoiceCount];
			if (!HEU_GeneralUtility.GetArray1Arg(nodeID, session.GetParamChoiceValues, _paramChoices, 0, nodeInfo.parmChoiceCount))
			{
				return false;
			}

			// Store ramps temporarily to post-process them later
			List<HEU_ParameterData> rampParameters = new List<HEU_ParameterData>();

			Stack<HEU_ParameterData> folderListParameters = new Stack<HEU_ParameterData>();
			HEU_ParameterData currentFolderList = null;

			// Parse each param info and build up the local representation of the hierarchy.
			// Note that this assumes that parmInfos is ordered as specified in the docs.
			// Specifically, a child parameter will always be listed after the containing parent's folder.
			for (int i = 0; i < nodeInfo.parmCount; ++i)
			{
				HAPI_ParmInfo parmInfo = parmInfos[i];

				if(currentFolderList != null)
				{
					// We're in a folder list. Check if all its children have been processed. If not, increment children processed.

					while (currentFolderList._folderListChildrenProcessed >= currentFolderList._parmInfo.size)
					{
						// Already processed all folders in folder list, so move to previous folder list or nullify if none left
						if(folderListParameters.Count > 0)
						{
							currentFolderList = folderListParameters.Pop();
						}
						else
						{
							currentFolderList = null;
							break;
						}
					}

					if (currentFolderList != null)
					{
						// This is part of a folder list, so mark as processed
						currentFolderList._folderListChildrenProcessed++;
						//Debug.LogFormat("Updating folder list children to {0} for {1}", currentFolderList._folderListChildrenProcessed, currentFolderList._name);

						// Sanity check because folders must come right after the folder list
						if (parmInfo.type != HAPI_ParmType.HAPI_PARMTYPE_FOLDER)
						{
							Debug.LogErrorFormat("Expected {0} type but got {1} for parameter {2}", HAPI_ParmType.HAPI_PARMTYPE_FOLDER, parmInfo.type, HEU_SessionManager.GetString(parmInfo.nameSH, session));
						}
					}
				}

				if (parmInfo.id < 0 || parmInfo.childIndex < 0)
				{
					Debug.LogWarningFormat("Corrupt parameter detected with name {0}. Skipping it.", HEU_SessionManager.GetString(parmInfo.nameSH, session));
					continue;
				}

				//Debug.LogFormat("Param: name={0}, type={1}, size={2}, invisible={3}, parentID={4}, instanceNum={5}, childIndex={6}", 
				//	HEU_SessionManager.GetString(parmInfo.nameSH, session), parmInfo.type, parmInfo.size, parmInfo.invisible, parmInfo.parentId,
				//	parmInfo.instanceNum, parmInfo.childIndex);

				if(parmInfo.invisible)
				{
					continue;
				}

				// Skip this param if any of the parm's parent folders are invisible
				bool bSkipParam = false;
				HAPI_ParmId parentID = parmInfo.parentId;
				while (parentID > 0 && !bSkipParam)
				{
					int parentIndex = Array.FindIndex(parmInfos, p => p.id == parentID);
					if (parentIndex >= 0)
					{
						if (parmInfos[parentIndex].invisible &&
							(parmInfos[parentIndex].type == HAPI_ParmType.HAPI_PARMTYPE_FOLDER
							|| parmInfos[parentIndex].type == HAPI_ParmType.HAPI_PARMTYPE_FOLDERLIST))
						{
							bSkipParam = true;
						}

						parentID = parmInfos[parentIndex].parentId;
					}
					else
					{
						Debug.LogErrorFormat("Parent of parameter {0} not found!", parmInfo.id);
						bSkipParam = true;
					}
				}

				if (bSkipParam)
				{
					continue;
				}

				HEU_ParameterData newParameter = new HEU_ParameterData();

				if(parmInfo.type == HAPI_ParmType.HAPI_PARMTYPE_FOLDERLIST)
				{
					// For folder list, push the current container folder list in stack, and set the new one as current.
					if (currentFolderList != null)
					{
						folderListParameters.Push(currentFolderList);
					}
					currentFolderList = newParameter;
				}
				else if (parmInfo.type >= HAPI_ParmType.HAPI_PARMTYPE_CONTAINER_START && parmInfo.type <= HAPI_ParmType.HAPI_PARMTYPE_CONTAINER_END)
				{
					// Contains list of folders.
					// Do nothing for containers. We're just going to use the Folder to get the children (see next case).
					continue;
				}
				else if(parmInfo.type == HAPI_ParmType.HAPI_PARMTYPE_FOLDER || parmInfo.type == HAPI_ParmType.HAPI_PARMTYPE_MULTIPARMLIST)
				{
					// Contains other containers or regular parms. Handling below.
				}
				else
				{
					// Regular params (not a container). Handling below.
				}

				if (newParameter != null)
				{
					// Initialize with parm info
					newParameter._parmInfo = parmInfo;
					newParameter._name = HEU_SessionManager.GetString(parmInfo.nameSH, session);
					newParameter._labelName = HEU_SessionManager.GetString(parmInfo.labelSH, session);

					// Set its value based on type
					switch (parmInfo.type)
					{
						case HAPI_ParmType.HAPI_PARMTYPE_INT:
						{
							newParameter._intValues = new int[parmInfo.size];
							Array.Copy(_paramInts, parmInfo.intValuesIndex, newParameter._intValues, 0, parmInfo.size);

							if (parmInfo.choiceCount > 0)
							{
								// Choice list for Int

								// We need to add the user labels and their corresponding int values
								newParameter._choiceLabels = new GUIContent[parmInfo.choiceCount];

								// This is the list of values that Unity Inspector requires for dropdowns
								newParameter._choiceIntValues = new int[parmInfo.choiceCount];

								for (int c = 0; c < parmInfo.choiceCount; ++c)
								{
									// Store the user friendly labels for each choice
									string labelStr = HEU_SessionManager.GetString(_paramChoices[parmInfo.choiceIndex + c].labelSH, session);
									newParameter._choiceLabels[c] = new GUIContent(labelStr);

									// This will be the index of the above string value for Unity
									newParameter._choiceIntValues[c] = c;

									// Store the current chosen value's index. This is to let Unity know which option to display.
									if (_paramInts[parmInfo.intValuesIndex] == newParameter._choiceIntValues[c])
									{
										newParameter._choiceValue = newParameter._choiceIntValues[c];
									}
								}
							}

							break;
						}
						case HAPI_ParmType.HAPI_PARMTYPE_FLOAT:
						{
							//Debug.LogFormat("Param: name:{0}, size:{1}", parmInfo.label, parmInfo.size);

							newParameter._floatValues = new float[parmInfo.size];
							Array.Copy(_paramFloats, parmInfo.floatValuesIndex, newParameter._floatValues, 0, parmInfo.size);

							//Debug.LogFormat("Param float with name {0}. Value = {1}", parmInfo.label, newParameter._floatValues[parmInfo.size - 1]);

							break;
						}
						case HAPI_ParmType.HAPI_PARMTYPE_STRING:
						{
							newParameter._stringValues = new string[parmInfo.size];
							Array.Copy(_paramStrings, parmInfo.stringValuesIndex, newParameter._stringValues, 0, parmInfo.size);
							
							if (parmInfo.choiceCount > 0)
							{
								// Choice list

								// We need to add the user labels and their corresponding string values
								newParameter._choiceLabels = new GUIContent[parmInfo.choiceCount];
								// This is the list of values Houdini requires
								newParameter._choiceStringValues = new string[parmInfo.choiceCount];

								// This is the list of values that Unity Inspector requires.
								// The Inspector requires an int array so we give it one.
								newParameter._choiceIntValues = new int[parmInfo.choiceCount];

								for (int c = 0; c < parmInfo.choiceCount; ++c)
								{
									// Store the user friendly labels for each choice
									string labelStr = HEU_SessionManager.GetString(_paramChoices[parmInfo.choiceIndex + c].labelSH, session);
									newParameter._choiceLabels[c] = new GUIContent(labelStr);

									// Store the string value that Houdini requires
									newParameter._choiceStringValues[c] = HEU_SessionManager.GetString(_paramChoices[parmInfo.choiceIndex + c].valueSH, session);

									// This will be the index of the above string value
									newParameter._choiceIntValues[c] = c;

									// Store the current chosen value
									// We look up our list of stringValues, and set the index into _choiceStringValues where
									// the string values match.
									if (_paramStrings[parmInfo.stringValuesIndex] == newParameter._choiceStringValues[c])
									{
										newParameter._choiceValue = newParameter._choiceIntValues[c];
									}
								}
							}

							break;
						}
						case HAPI_ParmType.HAPI_PARMTYPE_TOGGLE:
						{
							newParameter._toggle = Convert.ToBoolean(_paramInts[parmInfo.intValuesIndex]);

							break;
						}
						case HAPI_ParmType.HAPI_PARMTYPE_COLOR:
						{
							if(parmInfo.size == 3)
							{
								newParameter._color = new Color(_paramFloats[parmInfo.floatValuesIndex], _paramFloats[parmInfo.floatValuesIndex + 1], _paramFloats[parmInfo.floatValuesIndex + 2], 1f);
							}
							else if(parmInfo.size == 4)
							{
								newParameter._color = new Color(_paramFloats[parmInfo.floatValuesIndex], _paramFloats[parmInfo.floatValuesIndex + 1], _paramFloats[parmInfo.floatValuesIndex + 2], _paramFloats[parmInfo.floatValuesIndex + 3]);
							}
							else
							{
								Debug.LogWarningFormat("Unsupported color parameter with label {0} and size {1}.", HEU_SessionManager.GetString(parmInfo.labelSH, session), parmInfo.size);
							}

							break;
						}
						case HAPI_ParmType.HAPI_PARMTYPE_FOLDER:
						case HAPI_ParmType.HAPI_PARMTYPE_FOLDERLIST:
						case HAPI_ParmType.HAPI_PARMTYPE_FOLDERLIST_RADIO:
						{
							// Sync up the show/hide and tab index states
							if(previousParamFolders != null)
							{
								HEU_ParameterData oldFolderParameterData = null;
								if(previousParamFolders.TryGetValue(newParameter._name, out oldFolderParameterData))
								{
									newParameter._showChildren = oldFolderParameterData._showChildren;
									newParameter._tabSelectedIndex = oldFolderParameterData._tabSelectedIndex;
								}
							}

							break;
						}
						case HAPI_ParmType.HAPI_PARMTYPE_MULTIPARMLIST:
						{
							// For multiparms, we treat them pretty similar to folder lists.
							// The difference is that the instances can be changed. This is handled in the UI drawing.

							// parmInfo.instanceLength - # of parameters per instance
							// parmInfo.instanceCount - total # of instances
							// parmInfo.instanceStartOffset - instance numbers' start
							// parmInfo.isChildOfMultiParm - flags whether this instance is a child of a multiparm (parent)
							// parmInfo.instanceNum - instance this child belongs to

							// Note: adding / removing multiparm instance requires a complete rebuild of parameters, and UI refresh

							//Debug.LogFormat("MultiParm: id: {5}, # param per instance: {0}, # instances: {1}, start offset: {2}, childOfMutli: {3}, instanceNum: {4}",
							//	parmInfo.instanceLength, parmInfo.instanceCount, parmInfo.instanceStartOffset, parmInfo.isChildOfMultiParm, parmInfo.instanceNum,
							//	HEU_SessionManager.GetString(parmInfo.nameSH, session));

							if (parmInfo.rampType > HAPI_RampType.HAPI_RAMPTYPE_INVALID && parmInfo.rampType < HAPI_RampType.HAPI_RAMPTYPE_MAX)
							{
								rampParameters.Add(newParameter);
							}

							break;
						}
						case HAPI_ParmType.HAPI_PARMTYPE_PATH_FILE:
						case HAPI_ParmType.HAPI_PARMTYPE_PATH_FILE_GEO:
						case HAPI_ParmType.HAPI_PARMTYPE_PATH_FILE_IMAGE:
						{
							newParameter._stringValues = new string[parmInfo.size];
							Array.Copy(_paramStrings, parmInfo.stringValuesIndex, newParameter._stringValues, 0, parmInfo.size);

							// Cache the file type
							newParameter._fileTypeInfo = HEU_SessionManager.GetString(parmInfo.typeInfoSH, session);

							break;
						}
						case HAPI_ParmType.HAPI_PARMTYPE_BUTTON:
						{
							newParameter._intValues = new int[parmInfo.size];
							Array.Copy(_paramInts, parmInfo.intValuesIndex, newParameter._intValues, 0, parmInfo.size);
							break;
						}
						case HAPI_ParmType.HAPI_PARMTYPE_SEPARATOR:
						case HAPI_ParmType.HAPI_PARMTYPE_LABEL:
						{
							// No need to do anything
							break;
						}
						case HAPI_ParmType.HAPI_PARMTYPE_NODE:
						{
							// Get the node value from Houdini session to then re-connect with the object in the Unity scene
							HAPI_NodeId inputNodeValue = HEU_Defines.HEU_INVALID_NODE_ID;
							if (!session.GetParamNodeValue(nodeID, newParameter._name, out inputNodeValue))
							{
								Debug.LogWarningFormat("Failed to get input node value for parameter {0}.", HEU_SessionManager.GetString(parmInfo.labelSH, session));
								// Should be okay to continue to set saved connection data
							}

							if (previousParamInputNodes != null)
							{
								HEU_InputNode foundInputNode = null;
								previousParamInputNodes.TryGetValue(newParameter._name, out foundInputNode);

								if (foundInputNode != null)
								{
									// It should be okay to set the saved input node data as long as its valid, regardless of whether
									// it matches Houdini session. The idea being that the user's saved state is more accurate than
									// whats in the Houdini session, though the session should take the correct value on recook.
									newParameter._paramInputNode = foundInputNode;
								}
							}

							if (newParameter._paramInputNode == null)
							{
								newParameter._paramInputNode = HEU_InputNode.CreateSetupInput(parentAsset.AssetInfo.nodeId, 0, newParameter._labelName, HEU_InputNode.InputNodeType.PARAMETER, parentAsset);
								if (newParameter._paramInputNode != null)
								{
									newParameter._paramInputNode.ParamName = newParameter._name;
									parentAsset.AddInputNode(newParameter._paramInputNode);
								}
							}

							break;
						}
						default:
						{
							Debug.Log("Unsupported parameter type: " + parmInfo.type);
							break;
						}
					}

					// Add to serializable map
					parameterMap.Add(newParameter.ParmID, newParameter);

					int listIndex = _parameterList.Count;
					newParameter._unityIndex = listIndex;
					_parameterList.Add(newParameter);

					// Now add to parent list

					if(currentFolderList != null && newParameter._parmInfo.type == HAPI_ParmType.HAPI_PARMTYPE_FOLDER)
					{
						// Folder is part of a folder list, in which case we add to the folder list as its child
						currentFolderList._childParameterIDs.Add(listIndex);
						//Debug.LogFormat("Adding child param {0} to folder list {1}", newParameter._name, currentFolderList._name);
					}
					else if (newParameter.ParentID == HEU_Defines.HEU_INVALID_NODE_ID)
					{
						// No parent: store the root level parm in list.
						_rootParameters.Add(listIndex);
					}
					else
					{
						// For mutliparams, the ParentID will be valid so we will add to its parent multiparm container

						// Look up parent and add to it
						//Debug.LogFormat("Child with Parent: name={0}, instance num={1}", HEU_SessionManager.GetString(parmInfo.nameSH, session), parmInfo.instanceNum);
						HEU_ParameterData parentParameter = null;
						if(parameterMap.TryGetValue(newParameter.ParentID, out parentParameter))
						{
							// Store the list index of the current parameter into its parent's child list
							//Debug.LogFormat("Found parent id: {0}", HEU_SessionManager.GetString(parentParameter._parmInfo.nameSH, session));

							bool bInserted = false;
							int numChildren = parentParameter._childParameterIDs.Count;
							if (parmInfo.isChildOfMultiParm && numChildren > 0)
							{
								// For multiparms, keep the list ordered based on instance number.
								// The ordered list allows us to draw Inspector UI quickly.
								for (int j = 0; j < numChildren; ++j)
								{
									HEU_ParameterData childParm = GetParameter(parentParameter._childParameterIDs[j]);
									if(childParm._parmInfo.instanceNum >= 0 && parmInfo.instanceNum < childParm._parmInfo.instanceNum)
									{
										parentParameter._childParameterIDs.Insert(j, listIndex);
										bInserted = true;
										break;
									}
								}
							}

							if (!bInserted)
							{
								parentParameter._childParameterIDs.Add(listIndex);
								//Debug.LogFormat("Added child {0} to parent {1} with instance num {2} at index {3}",
								//	HEU_SessionManager.GetString(newParameter._parmInfo.nameSH, session),
								//	HEU_SessionManager.GetString(parentParameter._parmInfo.nameSH, session),
								//	newParameter._parmInfo.instanceNum, parentParameter._childParameterIDs.Count - 1);
							}
						}
						else
						{
							Debug.LogErrorFormat("Unable to find parent parameter with id {0}. It should have already been added to list!\n"
								+ "Parameter with id {0} and name {1} will not be showing up on UI.", newParameter.ParmID, newParameter._name);
							continue;
						}
					}
				}
			}

			// Setup each ramp parameter for quicker drawing
			foreach(HEU_ParameterData ramp in rampParameters)
			{
				SetupRampParameter(ramp);
			}

			_recacheUI = true;

			_validParameters = true;
			return true;
		}

		public void SetupRampParameter(HEU_ParameterData rampParameter)
		{
			if (rampParameter._parmInfo.rampType == HAPI_RampType.HAPI_RAMPTYPE_COLOR)
			{
				// Get all children that are points, and use their info to set the gradient color keys

				// instanceCount is the # of points, and therefore # of color keys
				GradientColorKey[] colorKeys = new GradientColorKey[rampParameter._parmInfo.instanceCount];

				int pointStartOffset = rampParameter._parmInfo.instanceStartOffset;

				// Unity only supports global GradientMode for the Gradient. Not per point.
				// Also there is only Fixed or Blend avaiable.
				// Therefore we use Blend unless all points in Houdini are Constant, in which case we use Fixed.
				GradientMode gradientMode = GradientMode.Fixed;

				int numChildren = rampParameter._childParameterIDs.Count;
				for (int i = 0; i < numChildren; ++i)
				{
					HEU_ParameterData childParam = GetParameter(rampParameter._childParameterIDs[i]);
					Debug.Assert(childParam != null && childParam._parmInfo.isChildOfMultiParm, "Expected valid child for MultiParm: " + rampParameter._labelName);

					int pointIndex = childParam._parmInfo.instanceNum;
					if (pointIndex >= pointStartOffset && ((pointIndex - pointStartOffset) < colorKeys.Length))
					{
						if (childParam._parmInfo.type == HAPI_ParmType.HAPI_PARMTYPE_FLOAT)
						{
							// Point position
							Debug.Assert(childParam._floatValues != null && childParam._floatValues.Length == 1, "Only expecting a single float for ramp position.");
							colorKeys[pointIndex - pointStartOffset].time = childParam._floatValues[0];
						}
						else if (childParam._parmInfo.type == HAPI_ParmType.HAPI_PARMTYPE_COLOR)
						{
							// Point color
							colorKeys[pointIndex - pointStartOffset].color = childParam._color;
						}
						else if (childParam._parmInfo.type == HAPI_ParmType.HAPI_PARMTYPE_INT)
						{
							// Point interpolation
							if (childParam._intValues[0] != 0)
							{
								gradientMode = GradientMode.Blend;
							}
						}
					}
				}

				rampParameter._gradient = new Gradient();
				rampParameter._gradient.colorKeys = colorKeys;
				rampParameter._gradient.mode = gradientMode;
			}
			else if (rampParameter._parmInfo.rampType == HAPI_RampType.HAPI_RAMPTYPE_FLOAT)
			{
				int numPts = rampParameter._parmInfo.instanceCount;
				int pointStartOffset = rampParameter._parmInfo.instanceStartOffset;

				List<int> interpolationValues = new List<int>();

				// First create the animation curve and set point positions and values
				rampParameter._animCurve = new AnimationCurve();
				for (int pt = 0; pt < numPts; ++pt)
				{
					HEU_ParameterData posParamData = GetParameter(rampParameter._childParameterIDs[pt * 3 + 0]);
					HEU_ParameterData valueParamData = GetParameter(rampParameter._childParameterIDs[pt * 3 + 1]);

					int pointIndex = posParamData._parmInfo.instanceNum;
					if (pointIndex >= pointStartOffset && ((pointIndex - pointStartOffset) < numPts))
					{
						float position = posParamData._floatValues[0];
						float value = valueParamData._floatValues[0];
						rampParameter._animCurve.AddKey(position, value);

						HEU_ParameterData interpParamData = GetParameter(rampParameter._childParameterIDs[pt * 3 + 2]);
						interpolationValues.Add(interpParamData._intValues[0]);
					}
				}

				// Setting tangent mode seems to work better after all points are added.
				HEU_HAPIUtility.SetAnimationCurveTangentModes(rampParameter._animCurve, interpolationValues);
			}
		}

		public HEU_ParameterData GetParameter(int listIndex)
		{
			if(listIndex >= 0 && listIndex < _parameterList.Count)
			{
				return _parameterList[listIndex];
			}
			return null;
		}

		public HEU_ParameterData GetParameter(string name)
		{
			foreach (HEU_ParameterData parameterData in _parameterList)
			{
				if(parameterData._name.Equals(name))
				{
					return parameterData;
				}
			}
			return null;
		}

		public HEU_ParameterData GetParameterWithParmID(HAPI_ParmId parmID)
		{
			foreach (HEU_ParameterData parameterData in _parameterList)
			{
				if (parameterData.ParmID == parmID)
				{
					return parameterData;
				}
			}
			return null;
		}

		public void RemoveParameter(int listIndex)
		{
			if (listIndex >= 0 && listIndex < _parameterList.Count)
			{
				_parameterList.RemoveAt(listIndex);
			}
		}

		public int GetChosenIndexFromChoiceList(HEU_ParameterData inChoiceParameter)
		{
			Debug.Assert(inChoiceParameter._parmInfo.choiceCount > 0, "Expecting a Choice List!");

			int numChoices = inChoiceParameter._choiceStringValues.Length;
			for (int i = 0; i < numChoices; ++i)
			{
				if(inChoiceParameter._choiceStringValues[i] == _paramStrings[inChoiceParameter._parmInfo.stringValuesIndex])
				{
					return i;
				}
			}

			return -1;
		}

		public string GetStringFromParameter(string paramName)
		{
			HEU_ParameterData paramData = GetParameter(paramName);
			if(paramData != null && (paramData.IsString() || paramData.IsPathFile()))
			{
				return paramData._stringValues[0];
			}
			return null;
		}

		public void SetStringToParameter(string paramName, string value)
		{
			HEU_ParameterData paramData = GetParameter(paramName);
			if (paramData != null && (paramData.IsString() || paramData.IsPathFile()))
			{
				paramData._stringValues[0] = value;
			}
		}
		
		/// <summary>
		/// Returns true if the parameter values have changed.
		/// Checks locally stored vs. values in the arrays from Houdini.
		/// </summary>
		/// <returns>True if parameter values have changed.</returns>
		public bool HaveParametersChanged()
		{
			if (!AreParametersValid())
			{
				return false;
			}

			foreach (HEU_ParameterData parameterData in _parameterList)
			{
				// Compare parameter data value against the value from arrays

				switch(parameterData._parmInfo.type)
				{
					case HAPI_ParmType.HAPI_PARMTYPE_INT:
					case HAPI_ParmType.HAPI_PARMTYPE_BUTTON:
					{
						if (!HEU_GeneralUtility.DoArrayElementsMatch(_paramInts, parameterData._parmInfo.intValuesIndex, parameterData._intValues, 0, parameterData.ParmSize))
						{
							return true;
						}
						break;
					}
					case HAPI_ParmType.HAPI_PARMTYPE_FLOAT:
					{
						if(!HEU_GeneralUtility.DoArrayElementsMatch(_paramFloats, parameterData._parmInfo.floatValuesIndex, parameterData._floatValues, 0, parameterData.ParmSize))
						{
							return true;
						}
						break;
					}
					case HAPI_ParmType.HAPI_PARMTYPE_STRING:
					case HAPI_ParmType.HAPI_PARMTYPE_PATH_FILE:
					case HAPI_ParmType.HAPI_PARMTYPE_PATH_FILE_GEO:
					case HAPI_ParmType.HAPI_PARMTYPE_PATH_FILE_IMAGE:
					{
						if(!HEU_GeneralUtility.DoArrayElementsMatch(_paramStrings, parameterData._parmInfo.stringValuesIndex, parameterData._stringValues, 0, parameterData.ParmSize))
						{
							return true;
						}
						break;
					}
					case HAPI_ParmType.HAPI_PARMTYPE_TOGGLE:
					{
						if(_paramInts[parameterData._parmInfo.intValuesIndex] != Convert.ToInt32(parameterData._toggle))
						{
							return true;
						}
						break;
					}
					case HAPI_ParmType.HAPI_PARMTYPE_COLOR:
					{
						if(_paramFloats[parameterData._parmInfo.floatValuesIndex] != parameterData._color[0]
							|| _paramFloats[parameterData._parmInfo.floatValuesIndex + 1] != parameterData._color[1]
							|| _paramFloats[parameterData._parmInfo.floatValuesIndex + 2] != parameterData._color[2]
							|| (parameterData.ParmSize == 4 && _paramFloats[parameterData._parmInfo.floatValuesIndex + 3] != parameterData._color[3]))
						{
							return true;
						}

						break;
					}
					case HAPI_ParmType.HAPI_PARMTYPE_NODE:
					{
						if(parameterData._paramInputNode != null && (parameterData._paramInputNode.RequiresUpload || parameterData._paramInputNode.HasInputNodeTransformChanged()))
						{
							return true;
						}

						break;
					}
					default:
					{
						// Unsupported type
						break;
					}
					// TODO: add support for rest of types
				}
			}
			return false;
		}

		/// <summary>
		/// Uploads parameter values to Houdini.
		/// Also updates local array values with the value from each HEU_ParameterData.
		/// Fails if invalid session, parameter size mismatch, or if any HAPI calls fail.
		/// </summary>
		/// <param name="bDoCheck">If true, will check if values changed before uploading. False skips check and uploads.</param>
		/// <returns>True if successfully uploaded all values.</returns>
		public bool UploadValuesToHoudini(HEU_SessionBase session, HEU_HoudiniAsset parentAsset, bool bDoCheck = true)
		{
			if(!AreParametersValid())
			{
				return false;
			}

			//Debug.LogFormat("UploadValuesToHAPI(bDoCheck = {0})", bDoCheck);

			// Check if parameters changed (unless bDoCheck is false).
			// Upload ints and floats are arrays.
			// Upload strings individually.

			bool bUpdateInts = false;
			bool bUpdateFloats = false;

			// Get the node info
			HAPI_NodeInfo nodeInfo = new HAPI_NodeInfo();
			bool bResult = session.GetNodeInfo(_nodeID, ref nodeInfo);
			if(!bResult)
			{
				return false;
			}

			// For each parameter, check changes and upload
			foreach (HEU_ParameterData parameterData in _parameterList)
			{
				switch (parameterData._parmInfo.type)
				{
					case HAPI_ParmType.HAPI_PARMTYPE_INT:
					case HAPI_ParmType.HAPI_PARMTYPE_BUTTON:
					{
						if(!bDoCheck || !HEU_GeneralUtility.DoArrayElementsMatch(_paramInts, parameterData._parmInfo.intValuesIndex, parameterData._intValues, 0, parameterData.ParmSize))
						{
							// For ints, update local array first
							//Debug.LogFormat("Int changed from {0} to {1}", _paramInts[parameterData._parmInfo.intValuesIndex], parameterData._intValues[0]);
							Array.Copy(parameterData._intValues, 0, _paramInts, parameterData._parmInfo.intValuesIndex, parameterData.ParmSize);

							bUpdateInts = true;
						}

						break;
					}
					case HAPI_ParmType.HAPI_PARMTYPE_FLOAT:
					{
						if (!bDoCheck || !HEU_GeneralUtility.DoArrayElementsMatch(_paramFloats, parameterData._parmInfo.floatValuesIndex, parameterData._floatValues, 0, parameterData.ParmSize))
						{
							// For floats, update local array first
							//Debug.LogFormat("Float changed to from {0} to {1}", _paramFloats[parameterData._parmInfo.floatValuesIndex], parameterData._floatValues[0]);
							Array.Copy(parameterData._floatValues, 0, _paramFloats, parameterData._parmInfo.floatValuesIndex, parameterData.ParmSize);
							bUpdateFloats = true;
						}

						break;
					}
					case HAPI_ParmType.HAPI_PARMTYPE_STRING:
					case HAPI_ParmType.HAPI_PARMTYPE_PATH_FILE:
					case HAPI_ParmType.HAPI_PARMTYPE_PATH_FILE_GEO:
					case HAPI_ParmType.HAPI_PARMTYPE_PATH_FILE_IMAGE:
					{
						if(!bDoCheck || !HEU_GeneralUtility.DoArrayElementsMatch(_paramStrings, parameterData._parmInfo.stringValuesIndex, parameterData._stringValues, 0, parameterData.ParmSize))
						{
							//Debug.LogFormat("Updating string at {0} with value {1}", parameterData._parmInfo.stringValuesIndex, parameterData._stringValue);

							// Update local value
							Array.Copy(parameterData._stringValues, 0, _paramStrings, parameterData._parmInfo.stringValuesIndex, parameterData.ParmSize);

							// Update Houdini each string at a time
							int numStrings = parameterData.ParmSize;
							for(int i = 0; i < numStrings; ++i)
							{
								if(!session.SetParamStringValue(_nodeID, _paramStrings[parameterData._parmInfo.stringValuesIndex + i], parameterData.ParmID, i))
								{
									return false;
								}
							}
						}
						break;
					}
					case HAPI_ParmType.HAPI_PARMTYPE_TOGGLE:
					{
						int toggleInt = Convert.ToInt32(parameterData._toggle);
						if (!bDoCheck || _paramInts[parameterData._parmInfo.intValuesIndex] != toggleInt)
						{
							_paramInts[parameterData._parmInfo.intValuesIndex] = toggleInt;

							bUpdateInts = true;
						}
						break;
					}
					case HAPI_ParmType.HAPI_PARMTYPE_COLOR:
					{
						if (!bDoCheck 
							|| _paramFloats[parameterData._parmInfo.floatValuesIndex] != parameterData._color[0]
							|| _paramFloats[parameterData._parmInfo.floatValuesIndex + 1] != parameterData._color[1]
							|| _paramFloats[parameterData._parmInfo.floatValuesIndex + 2] != parameterData._color[2]
							|| (parameterData.ParmSize == 4 && _paramFloats[parameterData._parmInfo.floatValuesIndex + 3] != parameterData._color[3]))
						{
							_paramFloats[parameterData._parmInfo.floatValuesIndex] = parameterData._color[0];
							_paramFloats[parameterData._parmInfo.floatValuesIndex + 1] = parameterData._color[1];
							_paramFloats[parameterData._parmInfo.floatValuesIndex + 2] = parameterData._color[2];

							if(parameterData.ParmSize == 4)
							{
								_paramFloats[parameterData._parmInfo.floatValuesIndex + 3] = parameterData._color[3];
							}

							bUpdateFloats = true;
						}

						break;
					}
					case HAPI_ParmType.HAPI_PARMTYPE_NODE:
					{
						if(!bDoCheck || (parameterData._paramInputNode.RequiresUpload))
						{
							parameterData._paramInputNode.UploadInput(session);
						}
						else if(bDoCheck && parameterData._paramInputNode.HasInputNodeTransformChanged())
						{
							parameterData._paramInputNode.UploadInputObjectTransforms(session);
						}
						
						break;
					}
					default:
					{
						// Unsupported type
						break;
					}
					// TODO: add support for rest of types
				}
			}

			// Sanity check for parameter sizes. Catching this early means we can inform player to rebuild the asset if something
			// goes out of sync.
			if((nodeInfo.parmIntValueCount != _paramInts.Length) || (nodeInfo.parmFloatValueCount != _paramFloats.Length) 
				|| (nodeInfo.parmStringValueCount != _paramStrings.Length))
			{
				// Parameter array size mismatch!
				Debug.LogError("Parameter size in Houdini does not match loaded asset's parameter size. Recommend rebuilding asset!");
				return false;
			}

			// Update the int values array
			if (bUpdateInts && !session.SetParamIntValues(_nodeID, ref _paramInts, 0, _paramInts.Length))
			{
				return false;
			}

			// Update the float values array
			if (bUpdateFloats && !session.SetParamFloatValues(_nodeID, ref _paramFloats, 0, _paramFloats.Length))
			{
				return false;
			}

			return true;
		}

		public void InsertInstanceToMultiParm(int unityParamIndex, int instanceIndex, int numInstancesToAdd)
		{
			_parameterModifiers.Add(HEU_ParameterModifier.GetNewModifier(HEU_ParameterModifier.ModifierAction.MULTIPARM_INSERT, unityParamIndex, instanceIndex, numInstancesToAdd));
		}

		public void RemoveInstancesFromMultiParm(int unityParamIndex, int instanceIndex, int numInstancesToRemove)
		{
			_parameterModifiers.Add(HEU_ParameterModifier.GetNewModifier(HEU_ParameterModifier.ModifierAction.MULTIPARM_REMOVE, unityParamIndex, instanceIndex, numInstancesToRemove));
		}

		public void ClearInstancesFromMultiParm(int unityParamIndex)
		{
			_parameterModifiers.Add(HEU_ParameterModifier.GetNewModifier(HEU_ParameterModifier.ModifierAction.MULTIPARM_CLEAR, unityParamIndex, 0, 0));
		}

		public bool HasModifiersPending()
		{
			return _parameterModifiers.Count > 0;
		}

		/// <summary>
		/// Goes through all pending parameter modifiers and actions on them.
		/// Deferred way to modify the parameter list after UI drawing.
		/// </summary>
		public void ProcessModifiers(HEU_SessionBase session)
		{
			if (!AreParametersValid())
			{
				return;
			}

			foreach(HEU_ParameterModifier paramModifier in _parameterModifiers)
			{
				//Debug.LogFormat("Processing modifier {0}", paramModifier._action);

				HEU_ParameterData parameter = GetParameter(paramModifier._parameterIndex);
				if(parameter == null)
				{
					// Possibly removed already? Don't believe need to flag a warning here.
					continue;
				}

				if (paramModifier._action == HEU_ParameterModifier.ModifierAction.MULTIPARM_CLEAR)
				{
					// Remove all instances one by one.
					for(int i = 0; i < parameter._parmInfo.instanceCount; ++i)
					{
						int lastIndex = parameter._parmInfo.instanceCount - i;
						//Debug.Log("CLEARING instance index " + lastIndex);
						if (!session.RemoveMultiParmInstance(_nodeID, parameter._parmInfo.id, lastIndex))
						{
							Debug.LogWarningFormat("Unable to clear instances from MultiParm {0}", parameter._labelName);
							break;
						}
					}

					RequiresRegeneration = true;
				}
				else if (paramModifier._action == HEU_ParameterModifier.ModifierAction.MULTIPARM_INSERT)
				{
					// Insert new parameter instances at the specified index
					// paramModifier._instanceIndex is the location to add at
					// paramModifier._modifierValue is the number of new parameter instances to add
					for (int i = 0; i < paramModifier._modifierValue; ++i)
					{
						int insertIndex = paramModifier._instanceIndex + i;
						//Debug.Log("INSERTING instance index " + insertIndex);
						if (!session.InsertMultiparmInstance(_nodeID, parameter._parmInfo.id, insertIndex))
						{
							Debug.LogWarningFormat("Unable to insert instance at {0} for MultiParm {1}", insertIndex, parameter._labelName);
							break;
						}
					}

					RequiresRegeneration = true;
				}
				else if (paramModifier._action == HEU_ParameterModifier.ModifierAction.MULTIPARM_REMOVE)
				{
					// Remove parameter instances at the specified index
					// paramModifier._modifierValue number of instances will be removed
					// paramModifier._instanceIndex is the starting index to remove from
					for (int i = 0; i < paramModifier._modifierValue; ++i)
					{
						int removeIndex = paramModifier._instanceIndex;
						//Debug.Log("REMOVING instance index " + removeIndex);
						if (!session.RemoveMultiParmInstance(_nodeID, parameter._parmInfo.id, removeIndex))
						{
							Debug.LogWarningFormat("Unable to remove instance at {0} for MultiParm {1}", removeIndex, parameter._labelName);
							break;
						}
					}

					RequiresRegeneration = true;
				}
				else if (paramModifier._action == HEU_ParameterModifier.ModifierAction.SET_FLOAT)
				{
					string paramName = parameter._name;
					session.SetParamFloatValue(_nodeID, paramName, paramModifier._instanceIndex, paramModifier._floatValue);

					RequiresRegeneration = true;
				}
				else if (paramModifier._action == HEU_ParameterModifier.ModifierAction.SET_INT)
				{
					string paramName = parameter._name;
					session.SetParamIntValue(_nodeID, paramName, paramModifier._instanceIndex, paramModifier._intValue);

					RequiresRegeneration = true;
				}
				else
				{
					Debug.LogWarningFormat("Unsupported parameter modifier: {0}", paramModifier._action);
				}
			}

			_parameterModifiers.Clear();
		}

#if REMOVE_THIS_AFTER_FINISHING_INPUT_NODE
		/// <summary>
		/// Disconnect the input object node for given parameter.
		/// </summary>
		/// <param name="session"></param>
		/// <param name="parameterData"></param>
		/// <param name="parentAsset"></param>
		private void DisconnectNodeParameter(HEU_SessionBase session, HEU_ParameterData parameterData, HEU_HoudiniAsset parentAsset)
		{
			if (parameterData._parameterInputNode._connectedInputNodeID != HEU_Defines.HEU_INVALID_NODE_ID)
			{
				session.SetParamNodeValue(_nodeID, parameterData._name, HEU_Defines.HEU_INVALID_NODE_ID);

				HEU_HoudiniAssetRoot houdiniAssetRoot = parameterData._parameterInputNode._connectedInputObject != null ? parameterData._parameterInputNode._connectedInputObject.GetComponent<HEU_HoudiniAssetRoot>() : null;
				if (houdiniAssetRoot != null)
				{
					// Connected to a HDA, so we'll just remove the reference.
					// Don't need to delete the node as the HDA should manage itself.

					// Remove downstream asset
					parentAsset.DisconnectFromUpstream(houdiniAssetRoot._houdiniAsset);
				}
				else if (session.IsNodeValid(parameterData._parameterInputNode._connectedInputNodeID, parameterData._parameterInputNode._connectedInputNodeUniqueID))
				{
					// The connected node only exists in Houdini session, so okay to delete it
					session.DeleteNode(parameterData._parameterInputNode._connectedInputNodeID);
				}
			}

			// Disconnect
			parameterData._parameterInputNode._connectedInputObject = null;
			parameterData._parameterInputNode._connectedInputNodeID = HEU_Defines.HEU_INVALID_NODE_ID;
			parameterData._parameterInputNode._connectedInputNodeUniqueID = HEU_Defines.HEU_INVALID_NODE_ID;
		}

		/// <summary>
		/// Connects the input object node for given parameter.
		/// </summary>
		/// <param name="parameterData">Parameter containing connected node reference.</param>
		private void ConnectNodeParameter(HEU_SessionBase session, HEU_ParameterData parameterData, HEU_HoudiniAsset parentAsset)
		{
			Debug.AssertFormat(parameterData._parmInfo.type == HAPI_ParmType.HAPI_PARMTYPE_NODE, "Expected parameter type {0} but instead got {1}", HAPI_ParmType.HAPI_PARMTYPE_NODE, parameterData._parmInfo.type);

			if(parameterData._parameterInputNode._unconnectedInputObject == null)
			{
				return;
			}

			// Handle connection based on the type of input object
			GameObject inputObject = parameterData._parameterInputNode._unconnectedInputObject;
			MeshFilter meshFilter = inputObject.GetComponent<MeshFilter>();

			string inputObjectName = inputObject.name;

			if (HEU_HoudiniAsset.IsHoudiniAssetRoot(inputObject))
			{
				// Connect to the asset ID
				HEU_HoudiniAsset houdiniAsset = inputObject.GetComponent<HEU_HoudiniAssetRoot>()._houdiniAsset;
				if (!houdiniAsset.IsAssetValidInHoudini(session) || houdiniAsset.DoesAssetRequireRecook())
				{
					Debug.LogFormat("Input HDA {0} requires recook for input node parameter {1}", houdiniAsset.AssetName, parameterData._labelName);
					houdiniAsset.RequestCook(false, false, false);
				}
				parameterData._parameterInputNode._connectedInputNodeID = houdiniAsset.AssetID;

				// Add downstream dependency
				parentAsset.ConnectToUpstream(houdiniAsset);
			}
			// TODO: handle curve
			else if(meshFilter != null && meshFilter.sharedMesh != null)
			{
				string validInputName = inputObjectName.Replace(' ', '_');

				// Create input node ID
				// Upload mesh data
				// Set object transform

				HAPI_NodeId newNodeID = HEU_Defines.HEU_INVALID_NODE_ID;
				session.CreateInputNode(out newNodeID, validInputName);
				if(newNodeID == HEU_Defines.HEU_INVALID_NODE_ID)
				{
					Debug.LogErrorFormat("Failed to create new input node in Houdini session. Connection will not be set for parameter {0}.", parameterData._labelName);
					return;
				}

				HAPI_NodeInfo newNodeInfo = new HAPI_NodeInfo();
				if(!session.GetNodeInfo(newNodeID, ref newNodeInfo))
				{
					session.DeleteNode(newNodeID);
					Debug.LogErrorFormat("Failed to retrieve new node info. Connection will not be set for parameter {0}.", parameterData._labelName);
					return;
				}

				// TODO: call HAPI_GetDisplayGeoInfo on created node? see http://www.sidefx.com/docs/hengine17.0/_h_a_p_i__asset_inputs.html

				Mesh sharedMesh = meshFilter.sharedMesh;
				if(!HEU_HAPIUtility.UploadMeshIntoHoudiniNode(session, newNodeID, 0, newNodeID, ref sharedMesh))
				{
					session.DeleteNode(newNodeID);
					Debug.LogErrorFormat("Failed to upload mesh data for input node parameter {0}.", parameterData._labelName);
					return;
				}

				// TODO: session.ConnectNodeInput(parentAsset.AssetID, )

				parameterData._parameterInputNode._connectedInputNodeID = newNodeID;
				parameterData._parameterInputNode._connectedInputParentNodeID = newNodeInfo.parentId;
			}
			else
			{
				// We don't support other types
				Debug.LogWarningFormat("Specified object {0} is not supported for input node parameter {1}.", inputObject.name, parameterData._labelName);
				return;
			}

			// Get node info for connected node
			HAPI_NodeInfo nodeInfo = new HAPI_NodeInfo();
			bool bResult = session.GetNodeInfo(parameterData._parameterInputNode._connectedInputNodeID, ref nodeInfo);
			if (!bResult)
			{
				// Unable to get node info which might mean the node doesn't exist. We can't do anything.
				Debug.LogError("Input node does not exist in session. Unable to connect!");
				parameterData._parameterInputNode._connectedInputNodeID = HEU_Defines.HEU_INVALID_NODE_ID;
				return;
			}

			parameterData._parameterInputNode._connectedInputObject = parameterData._parameterInputNode._unconnectedInputObject;
			parameterData._parameterInputNode._connectedInputNodeUniqueID = nodeInfo.uniqueHoudiniNodeId;

			// Set the asset transform using gameobject's
			UploadInputNodeTransform(session, parameterData._parameterInputNode);

			// Set param node value
			session.SetParamNodeValue(_nodeID, parameterData._name, parameterData._parameterInputNode._connectedInputNodeID);
		}

		private void UploadInputNodeTransform(HEU_SessionBase session, HEU_ParameterInputNode paramInputNode)
		{
			if (paramInputNode._connectedInputParentNodeID != HEU_Defines.HEU_INVALID_NODE_ID)
			{
				Matrix4x4 transMatrix = paramInputNode._connectedInputObject.transform.localToWorldMatrix;
				HAPI_TransformEuler transEuler = HEU_HAPIUtility.GetHAPITransformFromMatrix(ref transMatrix);
				session.SetObjectTransform(paramInputNode._connectedInputParentNodeID, ref transEuler);
				paramInputNode._connectedInputLastSyncedTransformMatrix = transMatrix;
			}
		}

		public static bool HasInputNodeTransformChanged(HEU_ParameterInputNode paramInputNode)
		{
			return (paramInputNode._connectedInputObject != null)
				&& (paramInputNode._connectedInputParentNodeID != HEU_Defines.HEU_INVALID_NODE_ID)
				&& (paramInputNode._connectedInputObject.transform.localToWorldMatrix != paramInputNode._connectedInputLastSyncedTransformMatrix);
		}
#endif

		/// <summary>
		/// Populate folder and input node parameter data from current parameter list.
		/// </summary>
		/// <param name="folderParams">Map to populate folder parameters</param>
		/// <param name="inputNodeParams">Map to populate input node parameters</param>
		public void GetParameterDataForUIRestore(Dictionary<string, HEU_ParameterData> folderParams, Dictionary<string, HEU_InputNode> inputNodeParams)
		{
			foreach (HEU_ParameterData parmData in _parameterList)
			{
				if (parmData._parmInfo.type == HAPI_ParmType.HAPI_PARMTYPE_NODE)
				{
					inputNodeParams[parmData._name] = parmData._paramInputNode;
				}
				else if(parmData._parmInfo.type == HAPI_ParmType.HAPI_PARMTYPE_FOLDER || parmData._parmInfo.type == HAPI_ParmType.HAPI_PARMTYPE_FOLDERLIST)
				{
					folderParams[parmData._name] = parmData;
				}
			}
		}

		/// <summary>
		/// Returns list of connected input node gameobjects.
		/// </summary>
		/// <param name="inputNodeObjects">List to populate</param>
		public void GetInputNodeConnectionObjects(List<GameObject> inputNodeObjects)
		{
			/* TODO INPUT NODE - add connected objects to list
			foreach (HEU_ParameterData parmData in _parameterList)
			{
				
				if (parmData._parmInfo.type == HAPI_ParmType.HAPI_PARMTYPE_NODE && parmData._parameterInputNode._connectedInputObject != null)
				{
					inputNodeObjects.Add(parmData._parameterInputNode._connectedInputObject);
				}
			}
			*/
		}

		public void DownloadPresetData(HEU_SessionBase session)
		{
			byte[] presetData = null;
			if (session.GetPreset(_nodeID, out presetData))
			{
				_presetData = presetData;
			}
		}

		public void UploadPresetData(HEU_SessionBase session)
		{
			if (_presetData != null && _presetData.Length > 0)
			{
				session.SetPreset(_nodeID, _presetData);
			}
		}

		public void UploadParameterInputs(HEU_SessionBase session, HEU_HoudiniAsset parentAsset, bool bForceUpdate)
		{
			foreach (HEU_ParameterData parmData in _parameterList)
			{
				if(parmData._parmInfo.type == HAPI_ParmType.HAPI_PARMTYPE_NODE && (bForceUpdate || parmData._paramInputNode.RequiresUpload))
				{
					if(bForceUpdate)
					{
						parmData._paramInputNode.ResetConnectionForForceUpdate(session);
					}

					parmData._paramInputNode.UploadInput(session);
				}
			}
		}

		public void UpdateTransformParameters(HEU_SessionBase session, ref HAPI_TransformEuler HAPITransform)
		{
			SyncParameterFromHoudini(session, "t");
			SyncParameterFromHoudini(session, "r");
			SyncParameterFromHoudini(session, "s");
		}

		public void SyncParameterFromHoudini(HEU_SessionBase session, string parameterName)
		{
			HEU_ParameterData parameterData = GetParameter(parameterName);
			if (parameterData != null)
			{
				if (session.GetParamFloatValues(_nodeID, parameterData._floatValues, parameterData._parmInfo.floatValuesIndex, parameterData.ParmSize))
				{
					Array.Copy(parameterData._floatValues, 0, _paramFloats, parameterData._parmInfo.floatValuesIndex, parameterData.ParmSize);
				}
			}
		}
	}

}   // HoudiniEngineUnity
						 
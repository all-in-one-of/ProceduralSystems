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

using UnityEngine;

namespace HoudiniEngineUnity
{
	/// <summary>
	/// Provides a programmer-centric API for querying and setting paramter values for an asset.
	/// </summary>
	public static class HEU_ParameterAccessor
	{
		public static bool GetToggle(HEU_HoudiniAsset asset, string paramName, out bool outValue)
		{
			outValue = false;
			HEU_ParameterData paramData = asset.Parameters.GetParameter(paramName);
			if (paramData != null && paramData.IsToggle())
			{
				outValue = paramData._toggle;
				return true;
			}
			else
			{
				Debug.LogWarningFormat("{0}: Query failed. Asset [{0}]'s Parameter [{1}] is not a valid toggle!", asset.AssetName, paramName);
				return false;
			}
		}

		public static bool SetToggle(HEU_HoudiniAsset asset, string paramName, bool setValue)
		{
			HEU_ParameterData paramData = asset.Parameters.GetParameter(paramName);
			if (paramData != null && paramData.IsToggle())
			{
				paramData._toggle = setValue;
				return true;
			}
			else
			{
				Debug.LogWarningFormat("{0}: Set failed. Asset [{0}]'s Parameter [{1}] is not a valid toggle!", asset.AssetName, paramName);
				return false;
			}
		}

		public static bool GetInt(HEU_HoudiniAsset asset, string paramName, out int outValue)
		{
			outValue = 0;
			HEU_ParameterData paramData = asset.Parameters.GetParameter(paramName);
			if(paramData != null && paramData.IsInt())
			{
				outValue = paramData._intValues[0];
				return true;
			}
			else
			{
				Debug.LogWarningFormat("{0}: Query failed. Asset [{0}]'s Parameter [{1}] is not a valid int!", asset.AssetName, paramName);
				return false;
			}
		}

		public static bool SetInt(HEU_HoudiniAsset asset, string paramName, int setValue)
		{
			HEU_ParameterData paramData = asset.Parameters.GetParameter(paramName);
			if (paramData != null && paramData.IsInt())
			{
				paramData._intValues[0] = setValue;
				return true;
			}
			else
			{
				Debug.LogWarningFormat("{0}: Set failed. Asset [{0}]'s Parameter [{1}] is not a valid int!", asset.AssetName, paramName);
				return false;
			}
		}

		public static bool GetFloat(HEU_HoudiniAsset asset, string paramName, out float outValue)
		{
			outValue = 0;
			HEU_ParameterData paramData = asset.Parameters.GetParameter(paramName);
			if (paramData != null && paramData.IsFloat())
			{
				outValue = paramData._floatValues[0];
				return true;
			}
			else
			{
				Debug.LogWarningFormat("{0}: Query failed. Asset [{0}]'s Parameter [{1}] is not a valid float!", asset.AssetName, paramName);
				return false;
			}
		}

		public static bool GetFloats(HEU_HoudiniAsset asset, string paramName, out float[] outValues)
		{
			HEU_ParameterData paramData = asset.Parameters.GetParameter(paramName);
			if (paramData != null && paramData.IsFloat())
			{
				outValues = paramData._floatValues;
				return true;
			}
			else
			{
				outValues = new float[0];
				Debug.LogWarningFormat("{0}: Query failed. Asset [{0}]'s Parameter [{1}] is not a valid float!", asset.AssetName, paramName);
				return false;
			}
		}

		public static bool SetFloat(HEU_HoudiniAsset asset, string paramName, float setValue)
		{
			HEU_ParameterData paramData = asset.Parameters.GetParameter(paramName);
			if (paramData != null && paramData.IsFloat())
			{
				paramData._floatValues[0] = setValue;
				return true;
			}
			else
			{
				Debug.LogWarningFormat("{0}: Set failed. Asset [{0}]'s Parameter [{1}] is not a valid float!", asset.AssetName, paramName);
				return false;
			}
		}

		public static bool SetFloats(HEU_HoudiniAsset asset, string paramName, float[] setValues)
		{
			HEU_ParameterData paramData = asset.Parameters.GetParameter(paramName);
			if (paramData != null && paramData.IsFloat())
			{
				paramData._floatValues = setValues;
				return true;
			}
			else
			{
				Debug.LogWarningFormat("{0}: Set failed. Asset [{0}]'s Parameter [{1}] is not a valid float!", asset.AssetName, paramName);
				return false;
			}
		}

		public static bool GetString(HEU_HoudiniAsset asset, string paramName, out string outValue)
		{
			outValue = null;
			HEU_ParameterData paramData = asset.Parameters.GetParameter(paramName);
			if (paramData != null && (paramData.IsString() || paramData.IsPathFile()))
			{
				outValue = paramData._stringValues[0];
				return true;
			}
			else
			{
				Debug.LogWarningFormat("{0}: Query failed. Asset [{0}]'s Parameter [{1}] is not a valid string!", asset.AssetName, paramName);
				return false;
			}
		}

		public static bool SetString(HEU_HoudiniAsset asset, string paramName, string setValue)
		{
			HEU_ParameterData paramData = asset.Parameters.GetParameter(paramName);
			if (paramData != null && (paramData.IsString() || paramData.IsPathFile()))
			{
				paramData._stringValues[0] = setValue;
				return true;
			}
			else
			{
				Debug.LogWarningFormat("{0}: Set failed. Asset [{0}]'s Parameter [{1}] is not a valid string!", asset.AssetName, paramName);
				return false;
			}
		}

		public static bool SetChoice(HEU_HoudiniAsset asset, string paramName, int setValue)
		{
			HEU_ParameterData paramData = asset.Parameters.GetParameter(paramName);
			if (paramData != null && paramData._parmInfo.choiceCount > 0 && setValue >= 0 && setValue < paramData._choiceIntValues.Length)
			{
				paramData._intValues[0] = paramData._choiceIntValues[setValue];
				return true;
			}
			else
			{
				Debug.LogWarningFormat("{0}: Set failed. Asset [{0}]'s Parameter [{1}] is not a valid choice!", asset.AssetName, paramName);
				return false;
			}
		}

		public static bool GetChoice(HEU_HoudiniAsset asset, string paramName, out int outValue)
		{
			outValue = 0;
			HEU_ParameterData paramData = asset.Parameters.GetParameter(paramName);
			if (paramData != null && paramData._parmInfo.choiceCount > 0)
			{
				outValue = paramData._intValues[0];
				return true;
			}
			else
			{
				Debug.LogWarningFormat("{0}: Query failed. Asset [{0}]'s Parameter [{1}] is not a valid choice!", asset.AssetName, paramName);
				return false;
			}
		}

		public static bool SetInputNode(HEU_HoudiniAsset asset, string paramName, GameObject obj, int index)
		{
			HEU_ParameterData paramData = asset.Parameters.GetParameter(paramName);
			if (paramData != null && paramData._paramInputNode != null)
			{
				if(index < paramData._paramInputNode.NumInputObjects())
				{
					paramData._paramInputNode.InsertInputObject(index, obj);
				}
				else
				{
					paramData._paramInputNode.AddInputObjectAtEnd(obj);
				}
				return true;
			}
			else
			{
				Debug.LogWarningFormat("{0}: Set failed. Asset [{0}]'s Parameter [{1}] is not a valid input parameter!", asset.AssetName, paramName);
				return false;
			}
		}

		public static bool GetInputNode(HEU_HoudiniAsset asset, string paramName, int index, out GameObject obj)
		{
			obj = null;
			HEU_ParameterData paramData = asset.Parameters.GetParameter(paramName);
			if (paramData != null && paramData._paramInputNode != null)
			{
				obj = paramData._paramInputNode.GetInputObject(index)._gameObject;
				return obj != null;
			}
			else
			{
				Debug.LogWarningFormat("{0}: Set failed. Asset [{0}]'s Parameter [{1}] is not a valid input parameter!", asset.AssetName, paramName);
				return false;
			}
		}

		public static bool GetColor(HEU_HoudiniAsset asset, string paramName, out Color getValue)
		{
			HEU_ParameterData paramData = asset.Parameters.GetParameter(paramName);
			if (paramData != null && paramData.IsColor())
			{
				getValue = paramData._color;
				return true;
			}
			else
			{
				getValue = Color.white;
				Debug.LogWarningFormat("{0}: Query failed. Asset [{0}]'s Parameter [{1}] is not a valid color!", asset.AssetName, paramName);
				return false;
			}
		}

		public static bool SetColor(HEU_HoudiniAsset asset, string paramName, Color setValue)
		{
			HEU_ParameterData paramData = asset.Parameters.GetParameter(paramName);
			if (paramData != null && paramData.IsColor())
			{
				paramData._color = setValue;
				return true;
			}
			else
			{
				Debug.LogWarningFormat("{0}: Set failed. Asset [{0}]'s Parameter [{1}] is not a valid color!", asset.AssetName, paramName);
				return false;
			}
		}
	}

}   // HoudiniEngineUnity
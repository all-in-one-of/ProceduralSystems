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
	/////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	// Typedefs (copy these from HEU_Common.cs)
	using HAPI_NodeId = System.Int32;
	using HAPI_ParmId = System.Int32;


	/// <summary>
	/// Contains utility functions for working with parameters
	/// </summary>
	public static class HEU_ParameterUtility
	{

		public static int GetParameterIndexFromName(HEU_SessionBase session, HAPI_ParmInfo[] parameters, string parameterName)
		{
			if(parameters != null && parameters.Length > 0)
			{
				int numParameters = parameters.Length;
				for(int i = 0; i < numParameters; ++i)
				{
					if(HEU_SessionManager.GetString(parameters[i].nameSH, session).Equals(parameterName))
					{
						return i;
					}
				}
			}
			return -1;
		}

		public static int GetParameterIndexFromNameOrTag(HEU_SessionBase session, HAPI_NodeId nodeID, HAPI_ParmInfo[] parameters, string parameterName)
		{
			int parameterIndex = GetParameterIndexFromName(session, parameters, parameterName);
			if (parameterIndex < 0)
			{
				// Try to find tag instead
				parameterIndex = HEU_Defines.HEU_INVALID_NODE_ID;
				session.GetParmWithTag(nodeID, parameterName, ref parameterIndex);
			}
			return parameterIndex;
		}

		public static float GetParameterFloatValue(HEU_SessionBase session, HAPI_NodeId nodeID, HAPI_ParmInfo[] parameters, string parameterName, float defaultValue)
		{
			int parameterIndex = GetParameterIndexFromNameOrTag(session, nodeID, parameters, parameterName);
			if(parameterIndex < 0 || parameterIndex >= parameters.Length)
			{
				return defaultValue;
			}

			int valueIndex = parameters[parameterIndex].floatValuesIndex;
			float[] value = new float[1];

			if(session.GetParamFloatValues(nodeID, value, valueIndex, 1))
			{
				return value[0];
			}

			return defaultValue;
		}

		public static Color GetParameterColor3Value(HEU_SessionBase session, HAPI_NodeId nodeID, HAPI_ParmInfo[] parameters, string parameterName, Color defaultValue)
		{
			int parameterIndex = GetParameterIndexFromNameOrTag(session, nodeID, parameters, parameterName);
			if (parameterIndex < 0 || parameterIndex >= parameters.Length)
			{
				return defaultValue;
			}

			if(parameters[parameterIndex].size < 3)
			{
				Debug.LogError("Parameter size not large enough to be a Color3");
				return defaultValue;
			}

			int valueIndex = parameters[parameterIndex].floatValuesIndex;
			float[] value = new float[3];

			if (session.GetParamFloatValues(nodeID, value, valueIndex, 3))
			{
				return new Color(value[0], value[1], value[2], 1f);
			}
			return defaultValue;
		}
	}

}   // HoudiniEngineUnity
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
using UnityEditor;


namespace HoudiniEngineUnity
{
	/// <summary>
	/// Custom Inspector UI for Houdini Asset.
	/// It uses HEU_HoudiniAssetRoot as the target object in order to access
	/// the underlying HEU_HoudiniAsset object whih contains actual data and logic.
	/// This allows to both show custom UI (via HEU_HoudiniAssetRoot) and 
	/// exclude Houdini-specific data at runtime (via HEU_HoudiniAsset which is EditorOnly).
	/// </summary>
	[CustomEditor(typeof(HEU_HoudiniAssetRoot))]
	public class HEU_HoudiniAssetUI : Editor
	{
		// The root gameobject for an HDA. Used to show this custom UI.
		private HEU_HoudiniAssetRoot _houdiniAssetRoot;

		// Actual HDA data and logic
		private HEU_HoudiniAsset _houdiniAsset;

		// Serialized asset object
		private SerializedObject _houdiniAssetSerializedObject;

		// Cache reference to the custom parameter editor
		private Editor _parameterEditor;

		// Cache reference to the custom curve editor
		private Editor _curveEditor;

		// Cache reference to the custom curve parameter editor
		private Editor _curveParameterEditor;

		// Cache reference to the custom Tools editor
		private Editor _toolsEditor;

		// Cache reference to the custom Handles editor
		private Editor _handlesEditor;


		private void OnEnable()
		{
			// Get the root gameobject, and the HDA bound to it
			_houdiniAssetRoot = target as HEU_HoudiniAssetRoot;
			TryAcquiringAsset();
		}

		private void TryAcquiringAsset()
		{
			if (_houdiniAsset == null && _houdiniAssetRoot != null)
			{
				_houdiniAsset = _houdiniAssetRoot._houdiniAsset;
			}

			if(_houdiniAsset != null && _houdiniAssetSerializedObject == null)
			{
				_houdiniAssetSerializedObject = new SerializedObject(_houdiniAsset);
			}
		}

		public void RefreshUI()
		{
			Repaint();
		}

		public override void OnInspectorGUI()
		{
			// Try acquiring asset reference in here again due to Undo.
			// Eg. After a delete, Undo requires us to re-acquire references.
			TryAcquiringAsset();

			if (_houdiniAsset == null)
			{
				DrawNoHDAInfo();
				return;
			}

			// Always hook into asset UI callback. This could have got reset on code refresh.
			_houdiniAsset._refreshUIDelegate = RefreshUI;

			serializedObject.Update();
			_houdiniAssetSerializedObject.Update();

			bool guiEnabled = GUI.enabled;

			GUIStyle backgroundStyle = new GUIStyle(GUI.skin.GetStyle("box"));
			RectOffset br = backgroundStyle.margin;
			br.top = 10;
			br.bottom = 6;
			br.left = 4;
			br.right = 4;
			backgroundStyle.margin = br;

			br = backgroundStyle.padding;
			br.top = 8;
			br.bottom = 8;
            br.left = 8;
            br.right = 8;
			backgroundStyle.padding = br;

			using (var hs = new EditorGUILayout.VerticalScope(backgroundStyle))
			{
				HEU_EditorUI.DrawSeparator();

                DrawHeaderSection();

				DrawLicenseInfo();

				bool bSkipDraw = DrawGenerateSection(_houdiniAssetRoot, serializedObject, _houdiniAsset, _houdiniAssetSerializedObject); ;
				if (!bSkipDraw)
				{
					SerializedProperty assetCookStatusProperty = HEU_EditorUtility.GetSerializedProperty(_houdiniAssetSerializedObject, "_cookStatus");
					if (assetCookStatusProperty != null)
					{
						// Track changes to Houdini Asset gameobject
						EditorGUI.BeginChangeCheck();

						DrawEventsSection(_houdiniAsset, _houdiniAssetSerializedObject);

						DrawAssetOptions(_houdiniAsset, _houdiniAssetSerializedObject);

						DrawCurvesSection(_houdiniAsset, _houdiniAssetSerializedObject);

						DrawInputNodesSection(_houdiniAsset, _houdiniAssetSerializedObject);

						// If this is a Curve asset, we don't need to draw parameters as its redundant
						if(_houdiniAsset.AssetType != HEU_HoudiniAsset.HEU_AssetType.TYPE_CURVE)
						{
							DrawParameters(_houdiniAsset.Parameters, ref _parameterEditor);
						}

						DrawInstanceInputs(_houdiniAsset, _houdiniAssetSerializedObject);

						// Check if any changes occurred, and if so, trigger a recook
						if (EditorGUI.EndChangeCheck())
						{
							_houdiniAssetSerializedObject.ApplyModifiedProperties();
							serializedObject.ApplyModifiedProperties();

							// Do recook if values have changed
							if (HEU_PluginSettings.CookingEnabled && _houdiniAsset.AutoCookOnParameterChange && _houdiniAsset.DoesAssetRequireRecook())
							{
								bool bCheckParametersChanged = true;
								bool bAsync = false;
								bool bForceCook = false;
								bool bUploadParameters = true;
								_houdiniAsset.RequestCook(bCheckParametersChanged, bAsync, bForceCook, bUploadParameters);
							}
						}
					}
				}
			}

			GUI.enabled = guiEnabled;
		}

		/// <summary>
		/// Callback when Scene is updated
		/// </summary>
		public void OnSceneGUI()
		{
			if ((Event.current.type == EventType.ValidateCommand && Event.current.commandName.Equals("UndoRedoPerformed")))
			{
				Event.current.Use();
			}

			if ((Event.current.type == EventType.ExecuteCommand && Event.current.commandName.Equals("UndoRedoPerformed")))
			{
				// TODO (read below)
				// Problem is that no matter how we query our asset, it already has the post Undo values. So we can't figure out what changed, if any.
				// Note that serializedObject has the old values. And we can't access _parameters from it. So can't get the old values.
				// Solution 1: don't do diff, update HAPI with all parameter vaues, recook
				// Solution 2: get parameter values from HAPI, diff, then recook if changed, but also need to check all asset values as well
				// Solution 3: Try using reflection? see Trello note

				// TODO: using temporary solution of not doing a diff, but just recooking!
				if(_houdiniAsset != null)   // _houidiniAsset.DoesAssetRequireRecook()
				{
					bool bCheckParametersChanged = false;
					bool bAsync = false;
					bool bForceCook = false;
					bool bUploadParameters = true;
					_houdiniAsset.RequestCook(bCheckParametersChanged, bAsync, bForceCook, bUploadParameters);
				}

				// Force a repaint here to update the UI when Undo is invoked. Handles case where the Inspector window is
				// no longer the focus. Without this the Inspector window still shows old value until user selects it.
				Repaint();
			}

			// Draw custom scene elements. Should be called for any event, not just repaint.
			DrawSceneElements(_houdiniAsset);
		}

		/// <summary>
		/// Draw Houdini Engine license info.
		/// </summary>
		private void DrawLicenseInfo()
		{
			HAPI_License license = HEU_SessionManager.GetCurrentLicense(false);
			if (license == HAPI_License.HAPI_LICENSE_HOUDINI_ENGINE_INDIE)
			{
				HEU_EditorUI.DrawSeparator();

				GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
				labelStyle.fontStyle = FontStyle.Bold;
				labelStyle.normal.textColor = HEU_EditorUI.IsEditorDarkSkin() ? Color.yellow : Color.red;
				EditorGUILayout.LabelField("Houdini Engine Indie - For Limited Commercial Use Only", labelStyle);

				HEU_EditorUI.DrawSeparator();
			}
		}

		private void DrawNoHDAInfo()
		{
			HEU_EditorUI.DrawSeparator();

			GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
			labelStyle.fontStyle = FontStyle.Bold;
			labelStyle.normal.textColor = HEU_EditorUI.IsEditorDarkSkin() ? Color.yellow : Color.red;
			EditorGUILayout.LabelField("Houdini Engine Asset - no HEU_HoudiniAsset found!", labelStyle);

			HEU_EditorUI.DrawSeparator();
		}

		/// <summary>
		/// Draw the Object Instance Inputs section for given asset.
		/// </summary>
		/// <param name="asset">The HDA asset</param>
		/// <param name="assetObject">Serialized HDA asset object</param>
		private void DrawInstanceInputs(HEU_HoudiniAsset asset, SerializedObject assetObject)
		{
			HEU_EditorUI.DrawSeparator();

			// Get list of object input fields
			List<HEU_ObjectInstanceInfo> objInstanceInfos = new List<HEU_ObjectInstanceInfo>();
			asset.PopulateObjectInstanceInfos(objInstanceInfos);

			int numObjInstances = objInstanceInfos.Count;

			// Display input section if at least have 1 input field
			if (numObjInstances > 0)
			{
				HEU_EditorUI.BeginSection();

				SerializedProperty showInstanceInputsProperty = assetObject.FindProperty("_showInstanceInputs");

				showInstanceInputsProperty.boolValue = HEU_EditorUI.DrawFoldOut(showInstanceInputsProperty.boolValue, "INSTANCE INPUTS");
				if (showInstanceInputsProperty.boolValue)
				{
					EditorGUI.BeginChangeCheck();

					// Draw each instanced input info
					for (int i = 0; i < numObjInstances; ++i)
					{
						EditorGUILayout.BeginVertical();

						string inputName = objInstanceInfos[i]._partTarget.PartName + "_" + i;

						SerializedObject objInstanceSerialized = new SerializedObject(objInstanceInfos[i]);

						SerializedProperty instancedInputsProperty = HEU_EditorUtility.GetSerializedProperty(objInstanceSerialized, "_instancedInputs");
						if (instancedInputsProperty != null)
						{
							int inputCount = instancedInputsProperty.arraySize;
							EditorGUILayout.PropertyField(instancedInputsProperty, new GUIContent(inputName), true);

							// When input size increases, Unity creates default values for HEU_InstancedInput which results in
							// zero value for scale offset. This fixes it up.
							int newInputCount = instancedInputsProperty.arraySize;
							if (inputCount < newInputCount)
							{
								for (int inputIndex = inputCount; inputIndex < newInputCount; ++inputIndex)
								{
									SerializedProperty scaleProperty = instancedInputsProperty.GetArrayElementAtIndex(inputIndex).FindPropertyRelative("_scaleOffset");
									scaleProperty.vector3Value = Vector3.one;
								}
							}
						}

						objInstanceSerialized.ApplyModifiedProperties();

						EditorGUILayout.EndVertical();
					}

					if(EditorGUI.EndChangeCheck())
					{
						bool bCheckParametersChanged = false;
						bool bAsync = true;
						bool bForceCook = false;
						bool bUploadParameters = true;
						asset.RequestCook(bCheckParametersChanged, bAsync, bForceCook, bUploadParameters);
					}
				}

				HEU_EditorUI.EndSection();
			}
		}

		/// <summary>
		/// Draw asset options for given asset.
		/// </summary>
		/// <param name="asset">The HDA asset</param>
		/// <param name="assetObject">Serialized HDA asset object</param>
		private void DrawAssetOptions(HEU_HoudiniAsset asset, SerializedObject assetObject)
		{
			HEU_EditorUI.BeginSection();
			{
				SerializedProperty showHDAOptionsProperty = assetObject.FindProperty("_showHDAOptions");

				showHDAOptionsProperty.boolValue = HEU_EditorUI.DrawFoldOut(showHDAOptionsProperty.boolValue, "ASSET OPTIONS");
				if (showHDAOptionsProperty.boolValue)
				{
					EditorGUI.indentLevel++;
					HEU_EditorUI.DrawPropertyField(assetObject, "_autoCookOnParameterChange", "Auto-Cook On Parameter Change");
					HEU_EditorUI.DrawPropertyField(assetObject, "_pushTransformToHoudini", "Push Transform To Houdini");
					HEU_EditorUI.DrawPropertyField(assetObject, "_transformChangeTriggersCooks", "Transform Change Triggers Cooks");
					HEU_EditorUI.DrawPropertyField(assetObject, "_cookingTriggersDownCooks", "Cooking Triggers Downstream Cooks");
					HEU_EditorUI.DrawPropertyField(assetObject, "_generateUVs", "Generate UVs");
					HEU_EditorUI.DrawPropertyField(assetObject, "_generateTangents", "Generate Tangents");
					HEU_EditorUI.DrawPropertyField(assetObject, "_ignoreNonDisplayNodes", "Ignore NonDisplay Nodes");

					if (asset.NumAttributeStores() > 0)
					{
						HEU_EditorUI.DrawPropertyField(assetObject, "_editableNodesToolsEnabled", "Enable Editable Node Tools");
					}

					if (asset.NumHandles() > 0)
					{
						HEU_EditorUI.DrawPropertyField(assetObject, "_handlesEnabled", "Enable Handles");
					}

					EditorGUI.indentLevel--;
				}
			}
			HEU_EditorUI.EndSection();

			HEU_EditorUI.DrawSeparator();
		}


		/// <summary>
		/// Draw the Generate section.
		/// </summary>
		private static bool DrawGenerateSection(HEU_HoudiniAssetRoot assetRoot, SerializedObject assetRootSerializedObject, HEU_HoudiniAsset asset, SerializedObject assetObject)
		{
			bool bSkipDrawing = false;

			float separatorDistance = 5f;

			float screenWidth = EditorGUIUtility.currentViewWidth;

			float buttonHeight = 30f;
			float widthPadding = 55f;
			float doubleButtonWidth = Mathf.Round(screenWidth - widthPadding + separatorDistance);
			float singleButtonWidth = Mathf.Round((screenWidth - widthPadding) * 0.5f);

			Texture2D reloadhdaIcon = Resources.Load("heu_reloadhdaIcon") as Texture2D;
			Texture2D recookhdaIcon = Resources.Load("heu_recookhdaIcon") as Texture2D;
			Texture2D bakegameobjectIcon = Resources.Load("heu_bakegameobjectIcon") as Texture2D;
			Texture2D bakeprefabIcon = Resources.Load("heu_bakeprefabIcon") as Texture2D;
			Texture2D bakeandreplaceIcon = Resources.Load("heu_bakeandreplaceIcon") as Texture2D;
			Texture2D removeheIcon = Resources.Load("heu_removeheIcon") as Texture2D;
			Texture2D duplicateAssetIcon = Resources.Load("heu_duplicateassetIcon") as Texture2D;

			GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
			buttonStyle.fontStyle = FontStyle.Bold;
			buttonStyle.fontSize = 11;
			buttonStyle.alignment = TextAnchor.MiddleLeft;
			buttonStyle.fixedHeight = buttonHeight;
			buttonStyle.padding.left = 6;
			buttonStyle.padding.right = 6;
			buttonStyle.margin.left = 0;
			buttonStyle.margin.right = 0;

			GUIStyle centredButtonStyle = new GUIStyle(buttonStyle);
			centredButtonStyle.alignment = TextAnchor.MiddleCenter;

			GUIStyle buttonSetStyle = new GUIStyle(GUI.skin.box);
			RectOffset br = buttonSetStyle.margin;
			br.left = 4;
			br.right = 4;
			buttonSetStyle.margin = br;

			GUIStyle boxStyle = new GUIStyle(GUI.skin.GetStyle("ColorPickerBackground"));
			br = boxStyle.margin;
			br.left = 4;
			br.right = 4;
			boxStyle.margin = br;
			boxStyle.padding = br;

			GUIContent reloadhdaContent = new GUIContent("  Reload Asset", reloadhdaIcon);
			GUIContent recookhdaContent = new GUIContent("  Recook Asset", recookhdaIcon);
			GUIContent bakegameobjectContent = new GUIContent("  Bake GameObject", bakegameobjectIcon);
			GUIContent bakeprefabContent = new GUIContent("  Bake Prefab", bakeprefabIcon);
			GUIContent bakeandreplaceContent = new GUIContent("  Bake Update", bakeandreplaceIcon);
			GUIContent removeheContent = new GUIContent("  Keep Only Output", removeheIcon);
			GUIContent duplicateContent = new GUIContent("  Duplicate Asset", duplicateAssetIcon);

			HEU_HoudiniAsset.AssetBuildAction pendingBuildAction = HEU_HoudiniAsset.AssetBuildAction.NONE;
			SerializedProperty pendingBuildProperty = HEU_EditorUtility.GetSerializedProperty(assetObject, "_requestBuildAction");
			if (pendingBuildProperty != null)
			{
				pendingBuildAction = (HEU_HoudiniAsset.AssetBuildAction)pendingBuildProperty.enumValueIndex;
			}

			// Track changes for the build and bake targets
			EditorGUI.BeginChangeCheck();

			HEU_EditorUI.BeginSection();
			{
				HEU_HoudiniAsset.AssetCookStatus cookStatus = HEU_HoudiniAsset.AssetCookStatus.NONE;

				SerializedProperty cookStatusProperty = HEU_EditorUtility.GetSerializedProperty(assetObject, "_cookStatus");
				if (cookStatusProperty != null)
				{
					cookStatus = (HEU_HoudiniAsset.AssetCookStatus)cookStatusProperty.enumValueIndex;

					if(cookStatus == HEU_HoudiniAsset.AssetCookStatus.COOKING || cookStatus == HEU_HoudiniAsset.AssetCookStatus.POSTCOOK)
					{
						recookhdaContent.text = "  Cooking Asset";
					}
					else if (cookStatus == HEU_HoudiniAsset.AssetCookStatus.LOADING || cookStatus == HEU_HoudiniAsset.AssetCookStatus.POSTLOAD)
					{
						reloadhdaContent.text = "  Loading Asset";
					}
				}

				SerializedProperty showGenerateProperty = assetObject.FindProperty("_showGenerateSection");

				showGenerateProperty.boolValue = HEU_EditorUI.DrawFoldOut(showGenerateProperty.boolValue, "GENERATE");
				if (showGenerateProperty.boolValue)
				{
					bool bHasPendingAction = (pendingBuildAction != HEU_HoudiniAsset.AssetBuildAction.NONE) || (cookStatus != HEU_HoudiniAsset.AssetCookStatus.NONE);

					HEU_EditorUI.DrawSeparator();

					EditorGUI.BeginDisabledGroup(bHasPendingAction);

					using (var hs = new EditorGUILayout.HorizontalScope(boxStyle))
					{
						if (GUILayout.Button(reloadhdaContent, buttonStyle, GUILayout.Width(singleButtonWidth)))
						{
							pendingBuildAction = HEU_HoudiniAsset.AssetBuildAction.RELOAD;
							bSkipDrawing = true;
						}

						GUILayout.Space(separatorDistance);

						if (!bSkipDrawing && GUILayout.Button(recookhdaContent, buttonStyle, GUILayout.Width(singleButtonWidth)))
						{
							pendingBuildAction = HEU_HoudiniAsset.AssetBuildAction.COOK;
							bSkipDrawing = true;
						}
					}

					using (var hs = new EditorGUILayout.HorizontalScope(boxStyle))
					{
						if (GUILayout.Button(removeheContent, buttonStyle, GUILayout.Width(singleButtonWidth)))
						{
							pendingBuildAction = HEU_HoudiniAsset.AssetBuildAction.STRIP_HEDATA;
							bSkipDrawing = true;
						}

						GUILayout.Space(separatorDistance);

						if (GUILayout.Button(duplicateContent, buttonStyle, GUILayout.Width(singleButtonWidth)))
						{
							pendingBuildAction = HEU_HoudiniAsset.AssetBuildAction.DUPLICATE;
							bSkipDrawing = true;
						}
					}

					EditorGUI.EndDisabledGroup();

					HEU_EditorUI.DrawSeparator();
				}
			}
			
			HEU_EditorUI.EndSection();
			
			HEU_EditorUI.DrawSeparator();

			HEU_EditorUI.BeginSection();
			{
				SerializedProperty showBakeProperty = assetObject.FindProperty("_showBakeSection");

				showBakeProperty.boolValue = HEU_EditorUI.DrawFoldOut(showBakeProperty.boolValue, "BAKE");
				if (showBakeProperty.boolValue)
				{
					if (!bSkipDrawing)
					{
						// Bake -> New Instance, New Prefab, Existing instance or prefab

						using (var vs = new EditorGUILayout.HorizontalScope(boxStyle))
						{
							if (GUILayout.Button(bakegameobjectContent, buttonStyle, GUILayout.Width(singleButtonWidth)))
							{
								asset.BakeToNewStandalone();
							}

							GUILayout.Space(separatorDistance);

							if (GUILayout.Button(bakeprefabContent, buttonStyle, GUILayout.Width(singleButtonWidth)))
							{
								asset.BakeToNewPrefab();
							}
						}

						HEU_EditorUI.DrawSeparator();

						using (var hs2 = new EditorGUILayout.VerticalScope(boxStyle))
						{
							if (GUILayout.Button(bakeandreplaceContent, centredButtonStyle, GUILayout.Width(doubleButtonWidth)))
							{
								if (assetRoot._bakeTargets == null || assetRoot._bakeTargets.Count == 0)
								{
									// No bake target means user probably forgot to set one. So complain!
									HEU_EditorUtility.DisplayDialog("No Bake Targets", "Bake Update requires atleast one valid GameObject.\n\nDrag a GameObject or Prefab onto the Drag and drop GameObjects / Prefabs field!", "OK");
								}
								else
								{
									int numTargets = assetRoot._bakeTargets.Count;
									for(int i = 0; i < numTargets; ++i)
									{
										GameObject bakeGO = assetRoot._bakeTargets[i];
										if (bakeGO != null)
										{
											if (HEU_EditorUtility.IsPrefabOriginal(bakeGO))
											{
												// Prefab original means its true prefab, and not an instance of it
												// TODO: allow user to cancel
												asset.BakeToExistingPrefab(bakeGO);
											}
											else
											{
												// This is for all standalone (including prefab instances)
												asset.BakeToExistingStandalone(bakeGO);
											}
										}
										else
										{
											Debug.LogWarning("Unable to bake to null target at index " + i);
										}
									}
								}
							}

							using (var hs = new EditorGUILayout.VerticalScope(buttonSetStyle))
							{
								SerializedProperty bakeTargetsProp = assetRootSerializedObject.FindProperty("_bakeTargets");
								if (bakeTargetsProp != null)
								{
									EditorGUILayout.PropertyField(bakeTargetsProp, new GUIContent("Drag & drop GameObjects / Prefabs:"), true, GUILayout.Width(doubleButtonWidth - 9f));
								}
							}
						}
					}
				}
			}
			HEU_EditorUI.EndSection();

			HEU_EditorUI.DrawSeparator();

			if(pendingBuildAction != HEU_HoudiniAsset.AssetBuildAction.NONE)
			{
				// Sanity check to make sure the asset is part of the AssetUpater
				HEU_AssetUpdater.AddAssetForUpdate(asset);

				// Apply pending build action based on user UI interaction above
				pendingBuildProperty.enumValueIndex = (int)pendingBuildAction;

				if (pendingBuildAction == HEU_HoudiniAsset.AssetBuildAction.COOK)
				{
					// Forcing recook without checking for changes allows users to do a semi-reset on the output, 
					// without needing to reload (loose changes) or change parameter then undo.
					SerializedProperty checkParameterChange = HEU_EditorUtility.GetSerializedProperty(assetObject, "_checkParameterChangeForCook");
					if (checkParameterChange != null)
					{
						checkParameterChange.boolValue = false;
					}
				}
			}
			
			if (EditorGUI.EndChangeCheck())
			{
				assetRootSerializedObject.ApplyModifiedProperties();
				assetObject.ApplyModifiedProperties();
			}

			return bSkipDrawing;
		}

        /// <summary>
        /// Draw the Houdini Engine header image
        /// </summary>
        void DrawHeaderSection()
        {
            GUI.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            Texture2D headerImage = Resources.Load("heu_hengine") as Texture2D;

            HEU_EditorUI.BeginSection();
            GUILayout.Label(headerImage, GUILayout.MinWidth(100));
            HEU_EditorUI.EndSection();

            GUI.backgroundColor = Color.white;

            HEU_EditorUI.DrawSeparator();
        }

		/// <summary>
		/// Draw Asset Events section.
		/// </summary>
		/// <param name="asset"></param>
		/// <param name="assetObject"></param>
		private void DrawEventsSection(HEU_HoudiniAsset asset, SerializedObject assetObject)
		{
			HEU_EditorUI.BeginSection();
			{
				SerializedProperty showEventsProperty = assetObject.FindProperty("_showEventsSection");

				showEventsProperty.boolValue = HEU_EditorUI.DrawFoldOut(showEventsProperty.boolValue, "EVENTS");
				if (showEventsProperty.boolValue)
				{
					HEU_EditorUI.DrawSeparator();

					SerializedProperty reloadEvent = assetObject.FindProperty("_reloadEvent");
					EditorGUILayout.PropertyField(reloadEvent, new GUIContent("Reload Events"));

					HEU_EditorUI.DrawSeparator();

					SerializedProperty recookEvent = assetObject.FindProperty("_cookedEvent");
					EditorGUILayout.PropertyField(recookEvent, new GUIContent("Cooked Events"));

					HEU_EditorUI.DrawSeparator();

					SerializedProperty bakedEvent = assetObject.FindProperty("_bakedEvent");
					EditorGUILayout.PropertyField(bakedEvent, new GUIContent("Baked Events"));
				}
			}

			HEU_EditorUI.EndSection();

			HEU_EditorUI.DrawSeparator();
		}

		private void DrawParameters(HEU_Parameters parameters, ref Editor parameterEditor)
		{
			if (parameters != null)
			{
				SerializedObject paramObject = new SerializedObject(parameters);
				Editor.CreateCachedEditor(paramObject.targetObject, null, ref parameterEditor);
				parameterEditor.OnInspectorGUI();
			}
		}

		private void DrawCurvesSection(HEU_HoudiniAsset asset, SerializedObject assetObject)
		{
			if (asset.GetEditableCurveCount() <= 0)
			{
				return;
			}

			HEU_EditorUI.BeginSection();
			{
				SerializedProperty showCurvesProperty = HEU_EditorUtility.GetSerializedProperty(assetObject, "_showCurvesSection");
				if (showCurvesProperty != null)
				{
					showCurvesProperty.boolValue = HEU_EditorUI.DrawFoldOut(showCurvesProperty.boolValue, "CURVES");
					if (showCurvesProperty.boolValue)
					{
						SerializedProperty curveEditorProperty = HEU_EditorUtility.GetSerializedProperty(assetObject, "_curveEditorEnabled");
						if (curveEditorProperty != null)
						{
							EditorGUILayout.PropertyField(curveEditorProperty);
						}

						SerializedProperty curveCollisionProperty = HEU_EditorUtility.GetSerializedProperty(assetObject, "_curveDrawCollision");
						if (curveCollisionProperty != null)
						{
							EditorGUILayout.PropertyField(curveCollisionProperty, new GUIContent("Draw Collision Type"));
							if (curveCollisionProperty.enumValueIndex == (int)HEU_Curve.CurveDrawCollision.COLLIDERS)
							{
								HEU_EditorUtility.EditorDrawSerializedProperty(assetObject, "_curveDrawColliders", label: "Colliders");
							}
							else if (curveCollisionProperty.enumValueIndex == (int)HEU_Curve.CurveDrawCollision.LAYERMASK)
							{
								HEU_EditorUtility.EditorDrawSerializedProperty(assetObject, "_curveDrawLayerMask", label: "Layer Mask");
							}
						}

						List<HEU_Curve> curves = asset.GetCurves();
						for (int i = 0; i < curves.Count; ++i)
						{
							if (curves[i].Parameters != null)
							{
								DrawParameters(curves[i].Parameters, ref _curveParameterEditor);
							}
						}
					}
				}
			}
			HEU_EditorUI.EndSection();

			HEU_EditorUI.DrawSeparator();
		}

		private void DrawInputNodesSection(HEU_HoudiniAsset asset, SerializedObject assetObject)
		{
			List<HEU_InputNode> inputNodes = asset.GetNonParameterInputNodes();
			if (inputNodes.Count > 0)
			{
				HEU_EditorUI.BeginSection();

				SerializedProperty showInputNodesProperty = HEU_EditorUtility.GetSerializedProperty(assetObject, "_showInputNodesSection");
				if (showInputNodesProperty != null)
				{
					showInputNodesProperty.boolValue = HEU_EditorUI.DrawFoldOut(showInputNodesProperty.boolValue, "INPUT NODES");
					if (showInputNodesProperty.boolValue)
					{
						foreach (HEU_InputNode inputNode in inputNodes)
						{
							HEU_InputNodeUI.EditorDrawInputNode(inputNode);

							if (inputNodes.Count > 1)
							{
								HEU_EditorUI.DrawSeparator();
							}
						}
					}

					HEU_EditorUI.DrawSeparator();
				}

				HEU_EditorUI.EndSection();

				HEU_EditorUI.DrawSeparator();
			}
		}

		private void DrawSceneElements(HEU_HoudiniAsset asset)
		{
			if(asset == null)
			{
				return;
			}

			// Curve Editor
			if (asset.CurveEditorEnabled)
			{
				if (asset.GetEditableCurveCount() > 0)
				{
					HEU_Curve[] curvesArray = asset.GetCurves().ToArray();
					Editor.CreateCachedEditor(curvesArray, null, ref _curveEditor);
					(_curveEditor as HEU_CurveUI).UpdateSceneCurves(asset);

					bool bRequiresCook = !System.Array.TrueForAll(curvesArray, c => c.EditState != HEU_Curve.CurveEditState.REQUIRES_GENERATION);
					if (bRequiresCook)
					{
						bool bCheckParametersChanged = false;
						bool bAsync = false;
						bool bForceCook = false;
						bool bUploadParameters = true;
						_houdiniAsset.RequestCook(bCheckParametersChanged, bAsync, bForceCook, bUploadParameters);
					}
				}
			}

			// Tools Editor
			if(asset.EditableNodesToolsEnabled)
			{
				List<HEU_AttributesStore> attributesStores = asset.GetAttributesStores();
				if(attributesStores.Count > 0)
				{
					HEU_AttributesStore[] attributesStoresArray = attributesStores.ToArray();
					Editor.CreateCachedEditor(attributesStoresArray, null, ref _toolsEditor);
					HEU_ToolsUI toolsUI = (_toolsEditor as HEU_ToolsUI);
					toolsUI.DrawToolsEditor(asset);

					if (asset.ToolsInfo._liveUpdate && !asset.ToolsInfo._isPainting)
					{
						bool bAttributesDirty = !System.Array.TrueForAll(attributesStoresArray, s => !s.AreAttributesDirty());
						if (bAttributesDirty)
						{
							//Debug.Log("Cook for attributes dirty!");
							bool bCheckParametersChanged = true;
							bool bAsync = false;
							bool bForceCook = false;
							bool bUploadParameters = true;
							_houdiniAsset.RequestCook(bCheckParametersChanged, bAsync, bForceCook, bUploadParameters);
						}
					}
				}
			}

			// Handles
			if(asset.HandlesEnabled)
			{
				List<HEU_Handle> handles = asset.GetHandles();
				if(handles.Count > 0)
				{
					HEU_Handle[] handlesArray = handles.ToArray();
					Editor.CreateCachedEditor(handlesArray, null, ref _handlesEditor);
					HEU_HandlesUI handlesUI = (_handlesEditor as HEU_HandlesUI);
					bool bHandlesChanged = handlesUI.DrawHandles(asset);

					if(bHandlesChanged)
					{
						bool bCheckParametersChanged = false;
						bool bAsync = false;
						bool bForceCook = false;
						bool bUploadParameters = true;
						_houdiniAsset.RequestCook(bCheckParametersChanged, bAsync, bForceCook, bUploadParameters);
					}
				}
			}
		}

		
	}

}   // HoudiniEngineUnity
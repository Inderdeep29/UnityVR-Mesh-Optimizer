﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class SceneOptimizerEditor : EditorWindow {
	private static SceneOptimizerEditor instance;

	private Vector2 scroll;
	private Vector3 playerPosition;
	private bool initializeFromTranform;
	private Transform playerTransform;
	private MeshEditorData root;
	private List<AnalyzerMeshData> meshData;
    private IEnumerator coroutineInProgress;
	private bool isAnalyzed;
	private bool isOptimized;
	private bool isAnalyzing;

	private Texture viewButtonImage;

	public static SceneOptimizerEditor GetInstance(){
		return instance;
	}

	[MenuItem("IDS/Optimize Scene for 3DOF")]
	static void OnOptimizeClick() {
		instance = (SceneOptimizerEditor) EditorWindow.GetWindow(typeof(SceneOptimizerEditor), true, "Scene Optimizer");
		instance.Initialize();
	}

	void GenerateMeshEditorData(MeshEditorData root) {
		if(root.gameObject != null && root.gameObject.GetComponent<MeshFilter>() != null)  {
			meshData.Add(new AnalyzerMeshData(root.gameObject.GetComponent<MeshFilter>(), Utilities.SampleSize._128, 20));
			root.analyzerMesh = meshData.Count - 1;
		}
		for(int i = 0; i< root.childrenData.Count; i++) {
			GenerateMeshEditorData(root.childrenData[i]);
		}
	}

	void Initialize() {
		viewButtonImage = Resources.Load("viewButtonImage") as Texture;
		playerPosition = Camera.main.transform.position;
		root = new MeshEditorData();
		root.GenerateChildData(UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects());
		meshData = new List<AnalyzerMeshData>();
		GenerateMeshEditorData(root);
		coroutineInProgress = null;
		isAnalyzing = false;
	}

	void EditorUpdate() {
		if(coroutineInProgress == null) return;
        bool finish = !coroutineInProgress.MoveNext();
		instance.Repaint();
        if (finish) {
			EditorApplication.update -= EditorUpdate;
			coroutineInProgress = null;
        }
	}

	void StartAnalyzing(MeshEditorData meshEditorData) {
		List<int> children = new List<int>();
		GetAllChildren(meshEditorData, ref children);
		StartAnalyzing(children);
	}

	void StartAnalyzing(List<int> data) {
		isAnalyzing = true;
		EditorApplication.update += EditorUpdate;
		coroutineInProgress = SceneOptimizer.AnalyzeMeshes(playerPosition, meshData.ToArray(), data, true);
	}

	public void StopAnalyzing() {
		isAnalyzing = false;
		EditorApplication.update -= EditorUpdate;
		coroutineInProgress = null;
		SceneOptimizer.Reset(meshData.ToArray());
	}

	void ResetAnalyzedData(MeshEditorData meshEditorData) {
		List<int> children = new List<int>();
		GetAllChildren(meshEditorData, ref children);
		ResetAnalyzedData(children);
	}

	void ResetAnalyzedData(List<int> data) {
		for(int i = 0; i<data.Count; i++) {
			meshData[data[i]].meshFilter.mesh = meshData[data[i]].OriginalMesh;
			Mesh.DestroyImmediate(meshData[data[i]].AnalyzedMesh);
			meshData[data[i]].AnalyzedMesh = null;
		}
	}

	void ResetOptimizedData(MeshEditorData meshEditorData) {
		List<int> children = new List<int>();
		GetAllChildren(meshEditorData, ref children);
		ResetOptimizedData(children);
	}

	void ResetOptimizedData(List<int> data) {
		for(int i = 0; i<data.Count; i++) {
			meshData[data[i]].meshFilter.mesh = meshData[data[i]].AnalyzedMesh;
			Mesh.DestroyImmediate(meshData[data[i]].OptimizedMesh);
			meshData[data[i]].OptimizedMesh = null;
		}
	}
	
	void StartOptimizing(MeshEditorData meshEditorData) {
		List<int> children = new List<int>();
		GetAllChildren(meshEditorData, ref children);
		StartOptimizing(children);
	}

	void StartOptimizing(List<int> data) {
		EditorApplication.update += EditorUpdate;
		coroutineInProgress = SceneOptimizer.OptimizeMeshes(meshData.ToArray(), data, true);
	}

	void StopOptimizing() {
		EditorApplication.update -= EditorUpdate;
		coroutineInProgress = null;
	}

	void OnDestroy() {
		StopAnalyzing();
	}

	Pair<Utilities.SampleSize, float> GetCommonChildrenData(MeshEditorData meshEditorData) {
		if(meshEditorData.childrenData.Count == 0) {
			return new Pair<Utilities.SampleSize, float>(meshData[meshEditorData.analyzerMesh].sampleRenderSize, meshData[meshEditorData.analyzerMesh].thresholdAngle);
		}
		Pair<Utilities.SampleSize, float> curr = new Pair<Utilities.SampleSize, float>();
		if(meshEditorData.analyzerMesh != -1) {
			curr.first = meshData[meshEditorData.analyzerMesh].sampleRenderSize;
			curr.second = meshData[meshEditorData.analyzerMesh].thresholdAngle;
		} else if(meshEditorData.childrenData.Count > 0) {
			curr = GetCommonChildrenData(meshEditorData.childrenData[0]);
		}
		for(int i = 0; i<meshEditorData.childrenData.Count; i++) {
			Pair<Utilities.SampleSize, float> child = GetCommonChildrenData(meshEditorData.childrenData[i]);
			if(child.first != curr.first) {
				curr.first = (Utilities.SampleSize)(-1);
			}
			if(child.second != curr.second) {
				curr.second = float.NaN;
			}
		}
		return curr;
	}

	bool IsChildrenAnalyzed(MeshEditorData meshEditorData) {
		if(meshEditorData.analyzerMesh != -1) {
			if(meshData[meshEditorData.analyzerMesh].AnalyzedMesh == null) {
				return false;
			}
		}
		for(int i = 0; i<meshEditorData.childrenData.Count; i++) {
			if(!IsChildrenAnalyzed(meshEditorData.childrenData[i])) {
				return false;
			}
		}
		return true;
	}

	bool IsChildrenOptimized(MeshEditorData meshEditorData) {
		if(meshEditorData.analyzerMesh != -1) {
			if(meshData[meshEditorData.analyzerMesh].OptimizedMesh == null) {
				return false;
			}
		}
		for(int i = 0; i<meshEditorData.childrenData.Count; i++) {
			if(!IsChildrenOptimized(meshEditorData.childrenData[i])) {
				return false;
			}
		}
		return true;
	}

	void UpdateAllChildrenSampleSize(MeshEditorData meshEditorData, Utilities.SampleSize sampleSize) {
		if(meshEditorData.analyzerMesh != -1) {
			meshData[meshEditorData.analyzerMesh].sampleRenderSize = sampleSize;
		}
		for(int i = 0; i<meshEditorData.childrenData.Count; i++) {
			UpdateAllChildrenSampleSize(meshEditorData.childrenData[i], sampleSize);
		}
	}

	void UpdateAllChildrenThreshold(MeshEditorData meshEditorData, float threshold) {
		if(meshEditorData.analyzerMesh != -1) {
			meshData[meshEditorData.analyzerMesh].thresholdAngle = threshold;
		}
		for(int i = 0; i<meshEditorData.childrenData.Count; i++) {
			UpdateAllChildrenThreshold(meshEditorData.childrenData[i], threshold);
		}
	}

	void GetAllChildren(MeshEditorData meshEditorData, ref List<int> analyzerMeshes) {
		if(meshEditorData.analyzerMesh != -1) {
			analyzerMeshes.Add(meshEditorData.analyzerMesh);
		}
		for(int i = 0; i<meshEditorData.childrenData.Count; i++) {
			GetAllChildren(meshEditorData.childrenData[i], ref analyzerMeshes);
		}
	}

	void MeshDataGUI(MeshEditorData meshEditorData, bool foldout = true) {
		if(meshEditorData.gameObject != null) {
			if(meshEditorData.analyzerMesh != -1) {
				if(meshData[meshEditorData.analyzerMesh].AnalyzedMesh == null) {
					isAnalyzed = false;
				}
				if(meshData[meshEditorData.analyzerMesh].OptimizedMesh == null) {
					isOptimized = false;
				}
			}
			if(foldout) {
				EditorGUILayout.BeginHorizontal();
				if(GUILayout.Button(viewButtonImage, EditorStyles.label, GUILayout.Width(15), GUILayout.Height(15))) {
					EditorGUIUtility.PingObject(meshEditorData.gameObject);
					Selection.activeGameObject = meshEditorData.gameObject;
				}
				if(meshEditorData.childrenData.Count > 0) {
					bool foldoutStatus = EditorGUILayout.Foldout(meshEditorData.foldout, meshEditorData.gameObject.name);
					if(foldoutStatus != meshEditorData.foldout) {
						meshEditorData.foldout = foldoutStatus;
						Pair<Utilities.SampleSize, float> data = GetCommonChildrenData(meshEditorData);
						meshEditorData.sampleRenderSize = data.first;
						meshEditorData.thresholdAngle = data.second;
					}
				} else {
					EditorGUILayout.LabelField (meshEditorData.gameObject.name);
				}
				//GUILayout.FlexibleSpace();
				//GUILayout.FlexibleSpace();
				if(!isAnalyzing) {
					if(!meshEditorData.foldout) {
						if(!IsChildrenAnalyzed(meshEditorData)) {
							Utilities.SampleSize sampleSizeStatus = 
								(Utilities.SampleSize) EditorGUILayout.EnumPopup(meshEditorData.sampleRenderSize, GUILayout.Width(100));
							if(sampleSizeStatus != meshEditorData.sampleRenderSize) {
								meshEditorData.sampleRenderSize = sampleSizeStatus;
								UpdateAllChildrenSampleSize(meshEditorData, sampleSizeStatus);
							}
							if(GUILayout.Button("Analyze Children", GUILayout.Width(150))) {
								StartAnalyzing(meshEditorData);
							}
						} else if(!IsChildrenOptimized(meshEditorData)) {
							float thresholdStatus = EditorGUILayout.FloatField(meshEditorData.thresholdAngle, GUILayout.Width(100));
							if(thresholdStatus != meshEditorData.thresholdAngle) {
								meshEditorData.thresholdAngle = thresholdStatus;
								UpdateAllChildrenThreshold(meshEditorData, thresholdStatus);
							}
							if(GUILayout.Button("Reanalyze Children", GUILayout.Width(150))) {
								ResetAnalyzedData(meshEditorData);
							}
							if(GUILayout.Button("Optimize Children", GUILayout.Width(150))) {
								StartOptimizing(meshEditorData);
							}
						} else {
							if(GUILayout.Button("Revert Children", GUILayout.Width(150))) {
								ResetOptimizedData(meshEditorData);
							}
						}
					} else if(meshEditorData.analyzerMesh != -1) {
						if(meshData[meshEditorData.analyzerMesh].AnalyzedMesh == null) {
							Utilities.SampleSize sampleSizeStatus = 
								(Utilities.SampleSize) EditorGUILayout.EnumPopup(meshData[meshEditorData.analyzerMesh].sampleRenderSize, GUILayout.Width(100));
							if(!meshEditorData.foldout) {
								if(sampleSizeStatus != meshData[meshEditorData.analyzerMesh].sampleRenderSize) {
									UpdateAllChildrenSampleSize(meshEditorData, sampleSizeStatus);
								}
							}
							meshData[meshEditorData.analyzerMesh].sampleRenderSize = sampleSizeStatus;
							if(GUILayout.Button("Analyze", GUILayout.Width(150))) {
								StartAnalyzing(new List<int>(){meshEditorData.analyzerMesh});
							}
						} else if(meshData[meshEditorData.analyzerMesh].OptimizedMesh == null) {
							float thresholdStatus = 
								EditorGUILayout.FloatField(meshData[meshEditorData.analyzerMesh].thresholdAngle, GUILayout.Width(100));
							if(!meshEditorData.foldout) {
								if(thresholdStatus != meshData[meshEditorData.analyzerMesh].thresholdAngle) {
									UpdateAllChildrenThreshold(meshEditorData, thresholdStatus);
								}
							}
							meshData[meshEditorData.analyzerMesh].thresholdAngle = thresholdStatus;
							if(GUILayout.Button("Reanalyze", GUILayout.Width(150))) {
								ResetAnalyzedData(new List<int>(){meshEditorData.analyzerMesh});
							}
							if(GUILayout.Button("Optimize", GUILayout.Width(150))) {
								StartOptimizing(new List<int>(){meshEditorData.analyzerMesh});
							}
						} else {
							if(GUILayout.Button("Revert", GUILayout.Width(150))) {
								ResetOptimizedData(new List<int>(){meshEditorData.analyzerMesh});
							}
						}
					}
				}
				EditorGUILayout.EndHorizontal();
			}
		}
        EditorGUI.indentLevel++;
		for(int i = 0; i<meshEditorData.childrenData.Count; i++) {
			MeshDataGUI(meshEditorData.childrenData[i], foldout && meshEditorData.foldout);
		}
		EditorGUI.indentLevel--;
	}

	void OnGUI() {
		scroll = EditorGUILayout.BeginScrollView(scroll);
		EditorGUILayout.LabelField ("Scene Optimizer", EditorStyles.boldLabel);
		if(initializeFromTranform) {
			playerTransform = (Transform)EditorGUILayout.ObjectField("Camera Position: ", playerTransform, typeof(Transform), true);
			if(GUILayout.Button("Apply")) {
				playerPosition = playerTransform.position;
				initializeFromTranform = false;
				playerTransform = null;
			}
		} else {
			GUILayout.Label ("Camera Position: ");
			EditorGUILayout.BeginHorizontal();
			playerPosition = EditorGUILayout.Vector3Field("", playerPosition);
			if(GUILayout.Button("Initialize From Transform")) {
				playerTransform = Camera.main.transform;
				initializeFromTranform = true;
			}
			EditorGUILayout.EndHorizontal();
		}
		GUILayout.Label ("Active meshes in scene: ", EditorStyles.boldLabel);
		isAnalyzed = true;
		isOptimized = true;
		MeshDataGUI(root);
		EditorGUILayout.EndScrollView();
		
		if(!isAnalyzed && !isAnalyzing) {
			if(GUILayout.Button("Analyze All")) {
				StartAnalyzing(root);
			}
		} else if(isAnalyzing) {
			EditorGUILayout.LabelField (SceneOptimizer.currentStatus);
			if(GUILayout.Button("Stop")) {
				StopAnalyzing();
			}
		} else if(!isOptimized) {
			if(GUILayout.Button("Reanalyze All")) {
				ResetAnalyzedData(root);
			}
			if(GUILayout.Button("Optimize All")) {
				StartOptimizing(root);
			}
		} else {
			if(GUILayout.Button("Revert All")) {
				ResetOptimizedData(root);
			}
			if(GUILayout.Button("Save As Combined Obj")) {
				Debug.LogError("To Be Implemented");
			}
			if(GUILayout.Button("Save As Separate Objs")) {
				foreach(AnalyzerMeshData data in meshData) {
					if(data.AnalyzedMesh != null) {
						ObjExporter.MeshToFile(data.AnalyzedMesh, 
							data.meshFilter.GetComponent<Renderer>().sharedMaterials, "Assets/IDS_SceneOptimizer/Objs/" + data.AnalyzedMesh.name + ".obj");
					}
				}
			}
			if(GUILayout.Button("Generate")) {
				//GenerateOptimizedMesh();
			}
		}
	}
}

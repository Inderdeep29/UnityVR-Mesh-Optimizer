using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class SceneOptimizerEditor : EditorWindow {
	private enum ProcessStatus {notAnalyzed, isAnalyzing, isAnalyzed, isGenerated}

	private static Vector2 scroll;
	private static Vector3 playerPosition;
	private static AnalyzerMeshData[] meshData;
    private static IEnumerator CoroutineInProgress;
	private static ProcessStatus processStatus;

	private bool initializeFromTranform;
	private Transform playerTransform;

	private static EditorWindow editorWindow;

	[MenuItem("IDS/Optimize Scene for 3DOF")]
	static void OnOptimizeClick() {
		Initialize();
		editorWindow = EditorWindow.GetWindow(typeof(SceneOptimizerEditor), true, "Scene Optimizer");
	}

	static void Initialize() {
		playerPosition = Camera.main.transform.position;
		MeshFilter[] activeMeshes = GameObject.FindObjectsOfType(typeof(MeshFilter)) as MeshFilter[];
		meshData = new AnalyzerMeshData[activeMeshes.Length];
		for(int i = 0; i<meshData.Length; i++) {
			meshData[i] = new AnalyzerMeshData(activeMeshes[i], Utilities.GetSampleResolution(Utilities.SampleSize._512), 40);
		}
		//SceneOptimizer.CoroutineInProgress = null;
		CoroutineInProgress = null;
		processStatus = ProcessStatus.notAnalyzed;
	}

	static void EditorUpdate() {
		if(CoroutineInProgress == null) return;
        bool finish = !CoroutineInProgress.MoveNext();
		editorWindow.Repaint();
        if (finish)
        {
			CoroutineInProgress = null;
			processStatus = ProcessStatus.isAnalyzed;
        }
	}

	void StartAnalyzing() {
		//Camera.onPostRender += SceneOptimizer.OnPostRender;
		EditorApplication.update += EditorUpdate;
		//SceneOptimizer.CoroutineInProgress = SceneOptimizer.AnalyzeMeshes(meshData);
		CoroutineInProgress = SceneOptimizer.AnalyzeMeshes(meshData, playerPosition);
	}

	void StopAnalyzing() {
		//Camera.onPostRender -= SceneOptimizer.OnPostRender;
		EditorApplication.update -= EditorUpdate;
		SceneOptimizer.StopAnalyzing(meshData);
		//SceneOptimizer.CoroutineInProgress = null;
		CoroutineInProgress = null;
	}

	void GenerateOptimizedMesh() {
		foreach(AnalyzerMeshData analyzerMeshData in meshData) {
			analyzerMeshData.MeshFilter().mesh = analyzerMeshData.GetOptimizedMesh();
		}
	}

	void OnDestroy() {
		StopAnalyzing();
	}

	void OnGUI() {
		scroll = EditorGUILayout.BeginScrollView(scroll);
		EditorGUILayout.LabelField ("Scene Optimizer", EditorStyles.boldLabel);
		if(initializeFromTranform) {
			playerTransform = (Transform)EditorGUILayout.ObjectField("Player Camera Position :", playerTransform, typeof(Transform), true);
			if(GUILayout.Button("Apply")) {
				playerPosition = playerTransform.position;
				initializeFromTranform = false;
				playerTransform = null;
			}
		} else {
			playerPosition = EditorGUILayout.Vector3Field("Player Camera Position :", playerPosition);
			if(GUILayout.Button("Initialize Position From Transform")) {
				playerTransform = Camera.main.transform;
				initializeFromTranform = true;
			}
		}
		GUILayout.Label (" Active meshes in scene :", EditorStyles.boldLabel);
		for(int i = 0; i<meshData.Length; i++) {
			EditorGUILayout.LabelField (meshData[i].GetTransform().name + " :");
		}
		EditorGUILayout.EndScrollView();

		switch(processStatus) {
			case ProcessStatus.notAnalyzed:
				if(GUILayout.Button("Analyze")) {
					processStatus = ProcessStatus.isAnalyzing;
					StartAnalyzing();
				}
				break;
			case ProcessStatus.isAnalyzing:
				EditorGUILayout.LabelField ("Progress :" + SceneOptimizer.currentStatus);
				if(GUILayout.Button("Stop")) {
					processStatus = ProcessStatus.notAnalyzed;
					StopAnalyzing();
				}
				break;
			case ProcessStatus.isAnalyzed:
				if(GUILayout.Button("Generate")) {
					processStatus = ProcessStatus.isGenerated;
					GenerateOptimizedMesh();
				}
				break;
		}
	}
}

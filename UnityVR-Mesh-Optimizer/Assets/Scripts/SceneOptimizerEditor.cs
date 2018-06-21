using System.Collections;
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
	private bool isAnalyzing;

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
			meshData.Add(new AnalyzerMeshData(root.gameObject.GetComponent<MeshFilter>(), Utilities.SampleSize._128, 40));
			root.analyzerMesh = meshData.Count - 1;
		}
		for(int i = 0; i< root.childrenData.Count; i++) {
			GenerateMeshEditorData(root.childrenData[i]);
		}
	}

	void Initialize() {
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
        if (finish)
        {
			coroutineInProgress = null;
        }
	}

	void StartAnalyzing(List<int> dataToAnalyze = null) {
		isAnalyzing = true;
		EditorApplication.update += EditorUpdate;
		coroutineInProgress = SceneOptimizer.AnalyzeMeshes(playerPosition, meshData.ToArray(), dataToAnalyze);
	}

	public void StopAnalyzing() {
		isAnalyzing = false;
		EditorApplication.update -= EditorUpdate;
		coroutineInProgress = null;
		SceneOptimizer.Reset(meshData.ToArray());
	}

	void GenerateOptimizedMesh() {
		foreach(AnalyzerMeshData analyzerMeshData in meshData) {
			analyzerMeshData.meshFilter.mesh = analyzerMeshData.OptimizedMesh;
		}
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

	void MeshDataGUI(MeshEditorData meshEditorData, bool foldout = true) {
		if(meshEditorData.gameObject != null) {
			if(meshEditorData.analyzerMesh != -1 && meshData[meshEditorData.analyzerMesh].OptimizedMesh == null) {
				isAnalyzed = false;
			}
			if(foldout) {
				EditorGUILayout.BeginHorizontal();
				if(meshEditorData.childrenData.Count > 0) {
					bool foldoutStatus = EditorGUILayout.Foldout(meshEditorData.foldout, meshEditorData.gameObject.name);
					if(foldoutStatus != meshEditorData.foldout) {
						meshEditorData.foldout = foldoutStatus;
						if(meshEditorData.analyzerMesh == -1) {
							Pair<Utilities.SampleSize, float> data = GetCommonChildrenData(meshEditorData);
							meshEditorData.sampleRenderSize = data.first;
							meshEditorData.thresholdAngle = data.second;
						}
					}
				} else {
					EditorGUILayout.LabelField (meshEditorData.gameObject.name);
				}
				if(!isAnalyzing && meshEditorData.analyzerMesh != -1) {
					if(meshData[meshEditorData.analyzerMesh].OptimizedMesh == null) {
						Utilities.SampleSize sampleSizeStatus = 
							(Utilities.SampleSize) EditorGUILayout.EnumPopup(meshData[meshEditorData.analyzerMesh].sampleRenderSize);
						float thresholdStatus = 
							EditorGUILayout.FloatField(meshData[meshEditorData.analyzerMesh].thresholdAngle);
						if(!meshEditorData.foldout) {
							if(sampleSizeStatus != meshData[meshEditorData.analyzerMesh].sampleRenderSize) {
								UpdateAllChildrenSampleSize(meshEditorData, sampleSizeStatus);
							}
							if(thresholdStatus != meshData[meshEditorData.analyzerMesh].thresholdAngle) {
								UpdateAllChildrenThreshold(meshEditorData, thresholdStatus);
							}
						}
						meshData[meshEditorData.analyzerMesh].sampleRenderSize = sampleSizeStatus;
						meshData[meshEditorData.analyzerMesh].thresholdAngle = thresholdStatus;
					}
					if(GUILayout.Button("View")) {
						EditorGUIUtility.PingObject(meshEditorData.gameObject);
						Selection.activeGameObject = meshEditorData.gameObject;
					}
					if(meshData[meshEditorData.analyzerMesh].OptimizedMesh == null) {
						if(GUILayout.Button("Analyze")) {
							StartAnalyzing(new List<int>(){meshEditorData.analyzerMesh});
						}
					} else {
						if(GUILayout.Button("Reanalyze")) {
							meshData[meshEditorData.analyzerMesh].OptimizedMesh = null;
						}
					}
				} else if(!meshEditorData.foldout) {
					Utilities.SampleSize sampleSizeStatus = 
						(Utilities.SampleSize) EditorGUILayout.EnumPopup(meshEditorData.sampleRenderSize);
					float thresholdStatus = EditorGUILayout.FloatField(meshEditorData.thresholdAngle);
					if(sampleSizeStatus != meshEditorData.sampleRenderSize) {
						meshEditorData.sampleRenderSize = sampleSizeStatus;
						UpdateAllChildrenSampleSize(meshEditorData, sampleSizeStatus);
					}
					if(thresholdStatus != meshEditorData.thresholdAngle) {
						meshEditorData.thresholdAngle = thresholdStatus;
						UpdateAllChildrenThreshold(meshEditorData, thresholdStatus);
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
		MeshDataGUI(root);
		EditorGUILayout.EndScrollView();
		
		if(!isAnalyzed && !isAnalyzing) {
			if(GUILayout.Button("Analyze All")) {
				StartAnalyzing();
			}
		} else if(isAnalyzing) {
			EditorGUILayout.LabelField (SceneOptimizer.currentStatus);
			if(GUILayout.Button("Stop")) {
				StopAnalyzing();
			}
		} else {
			if(GUILayout.Button("Save As Combined Obj")) {
				Debug.LogError("To Be Implemented");
			}
			if(GUILayout.Button("Save As Separate Objs")) {
				foreach(AnalyzerMeshData data in meshData) {
					if(data.OptimizedMesh != null) {
						ObjExporter.MeshToFile(data.OptimizedMesh, 
							data.meshFilter.GetComponent<Renderer>().sharedMaterials, "Assets/IDS_SceneOptimizer/Objs/" + data.OptimizedMesh.name + ".obj");
					}
				}
			}
			if(GUILayout.Button("Generate")) {
				GenerateOptimizedMesh();
			}
		}
	}
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SceneOptimizer {
	public static string currentStatus;

	private static Camera cam;
	private static MeshFilter visiblityTesterMesh;
	private static RenderTexture renderTexture;
	private static Texture2D renderTexture2D;

	static void Initialize(AnalyzerMeshData[] analyzerMeshData, Vector3 cameraPosition) {
		if(cam == null) {
			GameObject optimizerCamera = new GameObject("OptimizerCamera");
			cam = optimizerCamera.AddComponent<Camera>();
		}
		cam.transform.position = cameraPosition;
		cam.clearFlags = CameraClearFlags.Color;
		cam.backgroundColor = Color.black;
		cam.allowMSAA = false;
		cam.allowHDR = false;
		RenderTexture.DestroyImmediate(renderTexture);
		Texture2D.DestroyImmediate(renderTexture2D);
		Material unlitBlackMaterial = Utilities.GetUnlitMaterial(Color.black);
		for(int i = 0; i<analyzerMeshData.Length; i++) {
			analyzerMeshData[i].SetMaterial(unlitBlackMaterial);
		}
	}

	public static void Reset(AnalyzerMeshData[] analyzerMeshData) {
		for(int i = 0; i < analyzerMeshData.Length; i++) {
			analyzerMeshData[i].ResetMaterial();
		}
		if(cam != null) GameObject.DestroyImmediate(cam.gameObject);
		if(visiblityTesterMesh != null) GameObject.DestroyImmediate(visiblityTesterMesh.gameObject);
	}

	static void UpdateRenderTextureSize(int sampleResolution) {
		if(renderTexture == null || renderTexture.width != sampleResolution) {
			renderTexture = new RenderTexture(sampleResolution, sampleResolution, 24, RenderTextureFormat.ARGB32);
			renderTexture.Create();
			renderTexture2D = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
			cam.targetTexture = renderTexture;
		}
	}

	public static IEnumerator AnalyzeMeshes(Vector3 cameraPosition, AnalyzerMeshData[] analyzerMeshData, List<int> dataToAnalyze, bool updateMeshFilter) {
		Initialize(analyzerMeshData, cameraPosition);
		GameObject visiblityTester = new GameObject();
		visiblityTester.name = "VisiblityTester";
		visiblityTesterMesh = visiblityTester.AddComponent<MeshFilter>();
		visiblityTesterMesh.sharedMesh = new Mesh();
		visiblityTesterMesh.sharedMesh.vertices = new Vector3[3];
		visiblityTesterMesh.sharedMesh.triangles = new int[] {0, 1, 2};
		visiblityTester.AddComponent<MeshRenderer>().material = Utilities.GetUnlitMaterial(Color.white);

		float startTime = Time.time;
		for(int i = 0; i<dataToAnalyze.Count; i++) {
			if(analyzerMeshData[dataToAnalyze[i]].AnalyzedMesh != null) continue;
			IEnumerator inum = AnalyzeMesh(analyzerMeshData[dataToAnalyze[i]], true,  "Processing mesh: " + (i + 1) + "/" + dataToAnalyze.Count + "  ");
			while(inum.MoveNext()) {
				yield return null;
			}
		}
		SceneOptimizerEditor.GetInstance().StopAnalyzing();
		Debug.Log("Total Time: " + (Time.time - startTime) / 60 + " minutes");
	}

	static IEnumerator AnalyzeMesh(AnalyzerMeshData analyzerMeshData, bool updateMeshFilter, string logPrefix = null) {
		UpdateRenderTextureSize(analyzerMeshData.sampleResolution);
		Mesh analyzedMesh =  Mesh.Instantiate(analyzerMeshData.OriginalMesh);
		analyzedMesh.name = "IDS " + analyzedMesh.name;
		List<int> triangles = new List<int>(analyzedMesh.triangles);

		for(int i = 0; i < triangles.Count;) {
			currentStatus = logPrefix + "Triangle: " + (i / 3 + 1) + "/" + triangles.Count / 3f;
			Triangle visiblityTriangle = new Triangle(
				analyzerMeshData.transform.TransformPoint(analyzedMesh.vertices[triangles[i]]),
				analyzerMeshData.transform.TransformPoint(analyzedMesh.vertices[triangles[i + 1]]),
				analyzerMeshData.transform.TransformPoint(analyzedMesh.vertices[triangles[i + 2]])
			);
			visiblityTriangle.InflateTriangle(0.01f);
			visiblityTesterMesh.transform.position = visiblityTriangle.Center();
			visiblityTriangle.InverseTransformPoint(visiblityTesterMesh.transform);
			visiblityTesterMesh.sharedMesh.vertices = visiblityTriangle.GetLocalVertices();

			cam.transform.LookAt(visiblityTesterMesh.transform.position);
			AdjustCameraFOV(visiblityTesterMesh);
			yield return null;
			if(!WhiteVisibleOnScreen(visiblityTriangle, analyzerMeshData.sampleResolution)) {
				triangles.RemoveAt(i + 2);
				triangles.RemoveAt(i + 1);
				triangles.RemoveAt(i);
			} else {
				i += 3;
			}
		}

		analyzedMesh.triangles = triangles.ToArray();
		RemoveLoneVertices(analyzedMesh);
		analyzerMeshData.AnalyzedMesh = analyzedMesh;
		if(updateMeshFilter) {
			analyzerMeshData.meshFilter.mesh = analyzerMeshData.AnalyzedMesh;
		}
	}

	public static IEnumerator OptimizeMeshes(AnalyzerMeshData[] analyzerMeshData, List<int> dataToAnalyze, bool updateMeshFilter) {
		for(int i = 0; i<dataToAnalyze.Count; i++) {
			if(analyzerMeshData[dataToAnalyze[i]].AnalyzedMesh == null) continue;
			Mesh optimizedMesh = Mesh.Instantiate(analyzerMeshData[dataToAnalyze[i]].AnalyzedMesh);
			while(OptimizeMeshTriangles(optimizedMesh, analyzerMeshData[dataToAnalyze[i]].thresholdAngle)) {
				yield return null;
			}
			RemoveLoneVertices(optimizedMesh);
			analyzerMeshData[dataToAnalyze[i]].OptimizedMesh = optimizedMesh;
			if(updateMeshFilter) {
				analyzerMeshData[dataToAnalyze[i]].meshFilter.mesh = 
					analyzerMeshData[dataToAnalyze[i]].OptimizedMesh;
			}
			yield return null;
		}
	}

	static void AdjustCameraFOV(MeshFilter meshFilter) {
		while (cam.fieldOfView > 1 && IsInsideCamera(meshFilter)) {
			cam.fieldOfView -= 1;
		}
		while (cam.fieldOfView < 100 && !IsInsideCamera(meshFilter)) {
			cam.fieldOfView += 1;
		}
	}

	static bool IsInsideCamera(MeshFilter meshFilter) {
		foreach(Vector3 point in meshFilter.sharedMesh.vertices) {
			Vector2 screenPoint = cam.WorldToViewportPoint(meshFilter.transform.TransformPoint(point));
			if(screenPoint.x < 0 || screenPoint.x > 1 || screenPoint.y < 0 || screenPoint.y > 1) {
				return false;
			}
		}
		return true;
	}

	static bool WhiteVisibleOnScreen(Triangle triangle, int sampleResolution) {
		RenderTexture.active = renderTexture;
		renderTexture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
		RenderTexture.active = null;
		return triangle.ContainsColor(cam, ref renderTexture2D, Color.white);
	}

	static void RemoveLoneVertices(Mesh m) {
		int[] vertCount = new int[m.vertices.Length];
		for(int i = 0; i<m.triangles.Length; i++) {
			vertCount[m.triangles[i]]++;
		}
		int[] vertexMap = new int[m.vertices.Length];
		for(int i = 0, curr = 0; i< vertCount.Length; i++) {
			if(vertCount[i] > 0) {
				vertexMap[i] = curr;
				curr++;
			} else {
				vertexMap[i] = -1;
			}
		}
		UpdateMeshWithVertexMap(m, vertexMap);
	}

	static void UpdateMeshWithVertexMap(Mesh m, int[] vertexMap) {
		//Triangles
		int[] triangles = new int[m.triangles.Length];
		for(int i = 0; i<m.triangles.Length; i++) {
			triangles[i] = vertexMap[m.triangles[i]];
		}
		int vertexCount = 0;
		for(int i = 0; i< vertexMap.Length; i++) {
			if(vertexMap[i] != -1) {
				vertexCount++;
			}
		}
		//Vertices, Normals, Tangents and UV's
		Vector3[] vertices = new Vector3[vertexCount];
		Vector3[] normals = new Vector3[vertexCount];
		Vector4[] tangents = new Vector4[vertexCount];
		Vector2[] uv = new Vector2[vertexCount];
		for(int i = 0; i<vertexMap.Length; i++) {
			if(vertexMap[i] != -1) {
				vertices[vertexMap[i]] = m.vertices[i];
				normals[vertexMap[i]] = m.normals[i];
				tangents[vertexMap[i]] = m.tangents[i];
				if(vertexMap.Length == m.uv.Length) {
					uv[vertexMap[i]] = m.uv[i];
				}
			}
		}
		m.triangles = triangles;
		m.vertices = vertices;
		m.normals = normals;
		m.tangents = tangents;
		m.uv = uv;
		//m.tangents
		// m.uv2
		// m.uv3
		// m.uv4
	}

	static bool OptimizeMeshTriangles(Mesh m, float threshold) {
		bool isOptimized = false;
		List<int> triangles = new List<int>(m.triangles);
		for(int i = 0; i<m.vertices.Length; i++ ) {
			if(CanRemoveVertex(ref triangles, m.normals, i, threshold)) {
				List<int> verticesToFill = DeleteVertex(ref triangles, i);
				if(verticesToFill != null && verticesToFill.Count > 0) {
					isOptimized = true;
					FillArea(ref triangles, verticesToFill);
				}
			}
		}
		m.triangles = triangles.ToArray();
		return isOptimized;
	}

	static List<int> AdjoiningTriangles(ref List<int> triangles, int vertexNumber) {
		List<int> tris = new List<int>();
		for(int i = 0; i<triangles.Count; i++) {
			if(triangles[i] == vertexNumber) {
				int a = i - (i % 3);
				tris.Add(a);
			}
		}
		return tris;
	}

	static List<int> AdjacentVertices(ref List<int> triangles, List<int> adjoiningTriangles, int vertexNumber) {
		List<Pair<int, int>> adjacentVertexPairs = new List<Pair<int, int>>();
		for(int i = 0; i<adjoiningTriangles.Count; i++) {
			int[] v = new int[2];
			if(triangles[adjoiningTriangles[i]] == vertexNumber) {
				v[0] = triangles[adjoiningTriangles[i] + 1];
				v[1] = triangles[adjoiningTriangles[i] + 2];
			} else if(triangles[adjoiningTriangles[i] + 1] == vertexNumber) {
				v[0] = triangles[adjoiningTriangles[i] + 2];
				v[1] = triangles[adjoiningTriangles[i]];
			} else {
				v[0] = triangles[adjoiningTriangles[i]];
				v[1] = triangles[adjoiningTriangles[i] + 1];
			}
			adjacentVertexPairs.Add(new Pair<int, int>(v[0], v[1]));
		}
		if(adjacentVertexPairs.Count == 0) return null;
		List<int> adjacentVertices = new List<int>();
		adjacentVertices.Add(adjacentVertexPairs[0].first);
		adjacentVertices.Add(adjacentVertexPairs[0].second);
		adjacentVertexPairs.RemoveAt(0);
		while(adjacentVertexPairs.Count != 0) {
			bool nextFound = false;
			for(int i = 0; i<adjacentVertexPairs.Count; i++) {
				if(adjacentVertices[adjacentVertices.Count - 1] == adjacentVertexPairs[i].first) {
					adjacentVertices.Add(adjacentVertexPairs[i].second);
					adjacentVertexPairs.RemoveAt(i);
					nextFound = true;
					break;
				}
			}
			if(!nextFound) return null;
		}
		if(adjacentVertices[adjacentVertices.Count - 1] != adjacentVertices[0]) {
			return null;
		}
		adjacentVertices.RemoveAt(adjacentVertices.Count - 1);
		return adjacentVertices;
	}

	static bool CanRemoveVertex(ref List<int> triangles, Vector3[] normals, int vertexNumber, float thresholdAngle) {
		List<int> adjoiningTriangles = AdjoiningTriangles(ref triangles, vertexNumber);
		for(int i = 0; i <adjoiningTriangles.Count; i++) {
			if(Vector3.Angle(normals[triangles[adjoiningTriangles[i]]], normals[vertexNumber]) > thresholdAngle ||
					Vector3.Angle(normals[triangles[adjoiningTriangles[i] + 1]], normals[vertexNumber]) > thresholdAngle ||
					Vector3.Angle(normals[triangles[adjoiningTriangles[i] + 2]], normals[vertexNumber]) > thresholdAngle) {
				return false;
			}
		}
		return true;
	}

	static List<int> DeleteVertex(ref List<int> triangles, int vertexNumber) {
		List<int> adjoiningTriangles = AdjoiningTriangles(ref triangles, vertexNumber);
		List<int> adjacentVertices = AdjacentVertices(ref triangles, adjoiningTriangles, vertexNumber);
		if(adjacentVertices == null) return null;

		List<int> trianglesToRemove = new List<int>();
		for(int i = 0; i < adjoiningTriangles.Count; i++) {
			trianglesToRemove.Add(adjoiningTriangles[i]);
			trianglesToRemove.Add(adjoiningTriangles[i] + 1);
			trianglesToRemove.Add(adjoiningTriangles[i] + 2);
		}
		trianglesToRemove.Sort();
		for(int i = trianglesToRemove.Count - 1; i >= 0; i--) {
			triangles.RemoveAt(trianglesToRemove[i]);
		}
		return adjacentVertices;
	}

	static void FillArea(ref List<int> triangles, List<int> vertices) {
		if(vertices == null) return;
		for(int i = 1; i<vertices.Count - 1; i++) {
			triangles.Add(vertices[0]);
			triangles.Add(vertices[i]);
			triangles.Add(vertices[i + 1]);
		}
	}
}

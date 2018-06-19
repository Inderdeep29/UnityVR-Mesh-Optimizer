using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SceneOptimizer {
	public static string currentStatus;

	static Camera cam;
	static MeshFilter visiblityTesterMesh;
	static RenderTexture renderTexture;
	static Texture2D renderTexture2D;

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

	static void UpdateRenderTextureSize(int sampleResolution) {
		if(renderTexture == null || renderTexture.width != sampleResolution) {
			renderTexture = new RenderTexture(sampleResolution, sampleResolution, 24, RenderTextureFormat.ARGB32);
			renderTexture.Create();
			renderTexture2D = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
			cam.targetTexture = renderTexture;
		}
	}

	public static IEnumerator AnalyzeMeshes(AnalyzerMeshData[] analyzerMeshData, Vector3 cameraPosition) {
		Initialize(analyzerMeshData, cameraPosition);
		GameObject visiblityTester = new GameObject();
		visiblityTester.name = "VisiblityTester";
		visiblityTesterMesh = visiblityTester.AddComponent<MeshFilter>();
		visiblityTesterMesh.sharedMesh = new Mesh();
		visiblityTesterMesh.sharedMesh.vertices = new Vector3[3];
		visiblityTesterMesh.sharedMesh.triangles = new int[] {0, 1, 2};
		visiblityTester.AddComponent<MeshRenderer>().material = Utilities.GetUnlitMaterial(Color.white);

		float startTime = Time.time;
		for(int i = 0; i<analyzerMeshData.Length; i++) {
			IEnumerator inum = AnalyzeMesh(analyzerMeshData[i], "Processing mesh: " + (i + 1) + "/" + analyzerMeshData.Length + "  ");
			while(inum.MoveNext()) {
				yield return null;
			}
			//ObjExporter.MeshToFile(meshes[i], "Assets/Test/SceneOptimizerTest/Objs/" + i + ".obj");
		}
		StopAnalyzing(analyzerMeshData);
		Debug.Log("Total Time: " + (Time.time - startTime) / 60 + " minutes");
	}

	public static void StopAnalyzing(AnalyzerMeshData[] analyzerMeshData) {
		for(int i = 0; i < analyzerMeshData.Length; i++) {
			analyzerMeshData[i].ResetMaterial();
		}
		if(cam != null) GameObject.DestroyImmediate(cam.gameObject);
		if(visiblityTesterMesh != null) GameObject.DestroyImmediate(visiblityTesterMesh.gameObject);
	}

	static IEnumerator AnalyzeMesh(AnalyzerMeshData analyzerMeshData, string logPrefix = null) {
		UpdateRenderTextureSize(analyzerMeshData.SampleResolution());
		Mesh optimizedMesh =  Mesh.Instantiate(analyzerMeshData.GetMesh());
		optimizedMesh.name = "Optimized" + analyzerMeshData.GetMesh().name;
		List<int> triangles = new List<int>(analyzerMeshData.GetMesh().triangles);

		for(int i = 0; i < triangles.Count;) {
			currentStatus = logPrefix + "Triangle: " + (i / 3 + 1) + "/" + triangles.Count / 3f;
			Triangle visiblityTriangle = new Triangle(
				analyzerMeshData.GetTransform().gameObject.transform.TransformPoint(analyzerMeshData.GetMesh().vertices[triangles[i]]),
				analyzerMeshData.GetTransform().gameObject.transform.TransformPoint(analyzerMeshData.GetMesh().vertices[triangles[i + 1]]),
				analyzerMeshData.GetTransform().gameObject.transform.TransformPoint(analyzerMeshData.GetMesh().vertices[triangles[i + 2]])
			);
			visiblityTriangle.InflateTriangle(0.001f);
			visiblityTesterMesh.transform.position = visiblityTriangle.Center();
			visiblityTriangle.InverseTransformPoint(visiblityTesterMesh.transform);
			visiblityTesterMesh.sharedMesh.vertices = visiblityTriangle.GetLocalVertices();

			cam.transform.LookAt(visiblityTesterMesh.transform.position);

			yield return null;

			if(!WhiteVisibleOnScreen(visiblityTriangle, analyzerMeshData.SampleResolution())) {
				triangles.RemoveAt(i + 2);
				triangles.RemoveAt(i + 1);
				triangles.RemoveAt(i);
			} else {
				i += 3;
			}
		}

		optimizedMesh.triangles = triangles.ToArray();
		RemoveLoneVertices(optimizedMesh);
		while(OptimizeMeshTriangles(optimizedMesh, analyzerMeshData.ThresholdAngle())) {
			yield return null;
		}
		RemoveLoneVertices(optimizedMesh);
		analyzerMeshData.SetOptimizedMesh(optimizedMesh);
	}

	static bool WhiteVisibleOnScreen(Triangle triangle, int sampleResolution) {
		RenderTexture.active = renderTexture;
		renderTexture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
		RenderTexture.active = null;
		List<Pair<int, int>> screenPoints = triangle.GetScreenPoints(cam, sampleResolution);

		for(int i = 0; i< screenPoints.Count; i++) {
			if(renderTexture2D.GetPixel(screenPoints[i].first, screenPoints[i].second) == Color.white) {
				return true;
			}
		}
		return false;
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

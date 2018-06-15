using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SceneOptimizer : MonoBehaviour {
	enum SampleSize {_128, _256, _512, _1024, _2048};

	[SerializeField] Camera cam;
	[SerializeField] SampleSize sampleSize = SampleSize._256;
	[SerializeField] float threshold;

	MeshFilter visiblityTesterMesh;
	RenderTexture renderTexture;
	List<Material> materialsBackup = new List<Material>();

	List<Mesh> optimizedMeshes;

	void Start () {
		GameObject visiblityTester = new GameObject();
		visiblityTester.name = "VisiblityTester";
		visiblityTesterMesh = visiblityTester.AddComponent<MeshFilter>();
		visiblityTester.AddComponent<MeshRenderer>().material = GetUnlitMaterial(Color.white);

		int renderResolution = GetSampleResolution(sampleSize);
		renderTexture = new RenderTexture(renderResolution, renderResolution, 24, RenderTextureFormat.ARGB32);
		renderTexture.Create();
		cam.targetTexture = renderTexture;

		MeshFilter[] meshes = GetComponentsInChildren<MeshFilter>();
		for(int i = 0; i<meshes.Length; i++) {
			materialsBackup.Add(meshes[i].gameObject.GetComponent<MeshRenderer>().material);
			meshes[i].gameObject.GetComponent<MeshRenderer>().material = GetUnlitMaterial(Color.black);
		}
		StartCoroutine(OptimizeScene(meshes));
	}

	int GetSampleResolution(SampleSize size) {
		switch(size) {
			case SampleSize._128:
				return 128;
			case SampleSize._256:
				return 256;
			case SampleSize._512:
				return 512;
			case SampleSize._1024:
				return 1024;
			case SampleSize._2048:
				return 2048;
			default: 
				return 256;
		}
	}

	Material GetUnlitMaterial(Color col) {
		Material m = new Material(Shader.Find("Unlit/Color"));
		m.color = col;
		return m;
	}

	IEnumerator OptimizeScene(MeshFilter[] meshes) {
		optimizedMeshes = new List<Mesh>();
		for(int i = 0; i<meshes.Length; i++) {
			Debug.Log("Processing mesh: " + (i + 1) + "/" + meshes.Length);
			yield return OptimizeMesh(meshes[i]);

			//ObjExporter.MeshToFile(meshes[i], "Assets/Test/SceneOptimizerTest/Objs/" + i + ".obj");
		}

		for(int i = 0; i<meshes.Length; i++) {
			meshes[i].gameObject.GetComponent<MeshRenderer>().material = materialsBackup[i];
		}
		DestroyImmediate(visiblityTesterMesh.gameObject);
	}

	IEnumerator OptimizeMesh(MeshFilter m) {
		Mesh optimizedMesh =  Instantiate(m.mesh);
		optimizedMesh.name = "Optimized" + m.mesh.name;
		List<int> triangles = new List<int>(m.mesh.triangles);

		for(int i = 0; i < triangles.Count;) {
			Debug.Log("Triangle: " + (i / 3 + 1) + "/" + triangles.Count / 3f);
			Triangle visiblityTriangle = new Triangle(
				m.gameObject.transform.TransformPoint(m.mesh.vertices[triangles[i]]),
				m.gameObject.transform.TransformPoint(m.mesh.vertices[triangles[i + 1]]),
				m.gameObject.transform.TransformPoint(m.mesh.vertices[triangles[i + 2]])
			);
			visiblityTriangle.InflateTriangle(0.01f);
			visiblityTesterMesh.transform.position = visiblityTriangle.Center();
			visiblityTriangle.InverseTransformPoint(visiblityTesterMesh.transform);
			visiblityTesterMesh.mesh.vertices = visiblityTriangle.GetLocalVertices();
			visiblityTesterMesh.mesh.triangles = new int[] {0, 1, 2};

			cam.transform.LookAt(visiblityTesterMesh.transform.position);

			yield return new WaitForEndOfFrame();

			if(!WhiteVisibleOnScreen(visiblityTriangle)) {
				triangles.RemoveAt(i + 2);
				triangles.RemoveAt(i + 1);
				triangles.RemoveAt(i);
			} else {
				i += 3;
			}
		}
		optimizedMesh.triangles = triangles.ToArray();
		while(!OptimizeMeshTriangles(optimizedMesh, threshold));
		RemoveLoneVertices(optimizedMesh);
		m.mesh = optimizedMesh;
		optimizedMeshes.Add(optimizedMesh);
	}

	bool OptimizeMeshTriangles(Mesh m, float threshold) {
		bool isOptimized = false;
		List<int> triangles = new List<int>(m.triangles);
		for(int i = 0; i<m.vertices.Length; i++ ) {
			if(CanRemoveVertex(triangles, m.normals, i, threshold)) {
				DeleteVertex(ref triangles, i);
				isOptimized = true;
			}
		}
		m.triangles = triangles.ToArray();
		return isOptimized;
	}

	List<Pair<int, Triplet<int, int, int>>> AdjoiningTriangles(List<int> triangles, int vertexNumber) {
		List<Pair<int, Triplet<int, int, int>>> tris = new List<Pair<int, Triplet<int, int, int>>>();
		for(int i = 0; i<triangles.Count; i++) {
			if(triangles[i] == vertexNumber) {
				int a = i - (i % 3);
				tris.Add(new Pair<int, Triplet<int, int, int>>(a, new Triplet<int, int, int>(triangles[a], triangles[a + 1], triangles[a + 2])));
			}
		}
		return tris;
	}

	List<int> AdjacentVertices(List<Pair<int, Triplet<int, int, int>>> adjoiningTriangles, int vertexNumber) {
		List<Pair<int, int>> adjacentVertices = new List<Pair<int, int>>();
		for(int i = 0; i<adjoiningTriangles.Count; i++) {
			int[] v = new int[2];
			if(adjoiningTriangles[i].second.first == vertexNumber) {
				v[0] = adjoiningTriangles[i].second.second;
				v[1] = adjoiningTriangles[i].second.third;
			} else if(adjoiningTriangles[i].second.second == vertexNumber) {
				v[0] = adjoiningTriangles[i].second.third;
				v[1] = adjoiningTriangles[i].second.first;
			} else {
				v[0] = adjoiningTriangles[i].second.first;
				v[1] = adjoiningTriangles[i].second.second;
			}
			// if(adjoiningTriangles[i].first != vertexNumber) v.Add(adjoiningTriangles[i].first);
			// if(adjoiningTriangles[i].second != vertexNumber) v.Add(adjoiningTriangles[i].second);
			// if(adjoiningTriangles[i].third != vertexNumber) v.Add(adjoiningTriangles[i].third);
			// v.Sort();
			adjacentVertices.Add(new Pair<int, int>(v[0], v[1]));
		}
		if(adjacentVertices.Count == 0) return null;
		List<int> result = new List<int>();
		result.Add(adjacentVertices[0].first);
		result.Add(adjacentVertices[0].second);
		adjacentVertices.RemoveAt(0);
		while(adjacentVertices.Count != 0) {
			bool nextFound = false;
			for(int i = 0; i<adjacentVertices.Count; i++) {
				if(result[result.Count - 1] == adjacentVertices[i].first) {
					result.Add(adjacentVertices[i].second);
					adjacentVertices.RemoveAt(i);
					nextFound = true;
					break;
				}
			}
			if(!nextFound) return null;
		}
		if(result[result.Count - 1] != result[0]) {
			return null;
		}
		result.RemoveAt(result.Count - 1);
		return result;

		// adjacentVertices.Sort(delegate(Pair<int, int> a, Pair<int, int> b) {
		// 	if(a.first < b.first) return 0;
		// 	return -1;
		// });
		//return adjacentVertices;
	}

	bool CanRemoveVertex(List<int> triangles, Vector3[] normals, int vertexNumber, float thresholdAngle) {
		List<Pair<int, Triplet<int, int, int>>> adjoiningTriangles = AdjoiningTriangles(triangles, vertexNumber);
		for(int i = 0; i <adjoiningTriangles.Count; i++) {
			if(Vector3.Angle(normals[adjoiningTriangles[i].second.first], normals[vertexNumber]) > thresholdAngle ||
					Vector3.Angle(normals[adjoiningTriangles[i].second.second], normals[vertexNumber]) > thresholdAngle ||
					Vector3.Angle(normals[adjoiningTriangles[i].second.third], normals[vertexNumber]) > thresholdAngle) {
				return false;
			}
		}
		return true;
	}

	void DeleteVertex(ref List<int> triangles, int vertexNumber) {
		List<Pair<int, Triplet<int, int, int>>> adjoiningTriangles = AdjoiningTriangles(triangles, vertexNumber);
		List<int> adjacentVertices = AdjacentVertices(adjoiningTriangles, vertexNumber);
		if(adjacentVertices == null) return;
		List<int> trianglesToRemove = new List<int>();
		for(int i = 0; i < adjoiningTriangles.Count; i++) {
			trianglesToRemove.Add(adjoiningTriangles[i].first);
			trianglesToRemove.Add(adjoiningTriangles[i].first + 1);
			trianglesToRemove.Add(adjoiningTriangles[i].first + 2);
		}
		trianglesToRemove.Sort();
		for(int i = trianglesToRemove.Count - 1; i >= 0; i--) {
			triangles.RemoveAt(trianglesToRemove[i]);
		}
		// List<Pair<int, int>> adjacentVertices = AdjacentVertices(adjoiningTriangles, vertexNumber);
		// for(int i = 1; i<adjacentVertices.Count; i++) {
		// 	triangles.Add(adjacentVertices[0].first);
		// 	triangles.Add(adjacentVertices[i].first);
		// 	triangles.Add(adjacentVertices[i].second);
		// }

		for(int i = 1; i<adjacentVertices.Count - 1; i++) {
			triangles.Add(adjacentVertices[0]);
			triangles.Add(adjacentVertices[i]);
			triangles.Add(adjacentVertices[i + 1]);
		}
	}

	void RemoveLoneVertices(Mesh m) {
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

	void UpdateMeshWithVertexMap(Mesh m, int[] vertexMap) {
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
				uv[vertexMap[i]] = m.uv[i];
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

	bool WhiteVisibleOnScreen(Triangle triangle) {
		Texture2D tex2d = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
		RenderTexture.active = renderTexture;
		tex2d.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
		tex2d.Apply();

		List<Vector2Int> screenPoints = triangle.GetScreenPoints(cam);
		for(int i = 0; i< screenPoints.Count; i++) {
			if(tex2d.GetPixel(screenPoints[i].x, screenPoints[i].y) == Color.white) {
				return true;
			}
		}

		DestroyImmediate(tex2d);
		return false;
	}

	class Pair<A, B> {
		public A first;
		public B second;

		public Pair(A a, B b) {
			first = a;
			second = b;
		}
	}

	class Triplet<A, B, C> {
		public A first;
		public B second;
		public C third;

		public Triplet(A a, B b, C c) {
			first = a;
			second = b;
			third = c;
		}
	}

	class Triangle {
		List<Vector3> worldVertices;
		List<Vector3> localVertices;
		
		public Triangle(Vector3 a, Vector3 b, Vector3 c) {
			worldVertices = new List<Vector3>();
			worldVertices.Add(a);
			worldVertices.Add(b);
			worldVertices.Add(c);
		}
		
		public Vector3 Center() {
			if(worldVertices == null) return new Vector3();
			Vector3 center = new Vector3();
			for(int i = 0; i<worldVertices.Count; i++) {
				center += worldVertices[i];
			}
			return center / worldVertices.Count;
		}

		public void InflateTriangle(float value) {
			if(worldVertices == null) return;
			Vector3 side1 = worldVertices[1] - worldVertices[0];
			Vector3 side2 = worldVertices[2] - worldVertices[0];

			Vector3 dir = Vector3.Cross(side1, side2);
			dir.Normalize();
			
			for(int i = 0; i<worldVertices.Count; i++) {
				worldVertices[i] += dir * value;
			}
		}

		public void InverseTransformPoint(Transform t) {
			localVertices = new List<Vector3>();
			for(int i = 0; i<worldVertices.Count; i++) {
				localVertices.Add(t.InverseTransformPoint(worldVertices[i]));
			}
		}

		public Vector3[] GetWorldVertices() {
			return worldVertices.ToArray();
		}

		public Vector3[] GetLocalVertices() {
			return localVertices.ToArray();
		}

		public List<Vector2Int> GetScreenPoints(Camera cam) {
			List<Vector3> screenVertices = new List<Vector3>();
			for(int i = 0; i < worldVertices.Count; i++) {
				screenVertices.Add(cam.WorldToScreenPoint(worldVertices[i]));
			}
			List<Vector2Int> points = new List<Vector2Int>();

			// int minX = 0;
			// int maxX = 0;
			// int minY = 0;
			// int midY = 0;
			// int maxY = 0;

			// for(int i = 0; i < screenVertices.Count; i++) {
			// 	if(screenVertices[i].x < screenVertices[minX].x) {
			// 		minX = i;
			// 	} 
			// 	if(screenVertices[i].x > screenVertices[maxX].x) {
			// 		maxX = i;
			// 	}
			// 	if(screenVertices[i].y < screenVertices[minY].y) {
			// 		minY = i;
			// 	} 
			// 	if(screenVertices[i].y > screenVertices[maxY].y) {
			// 		maxY = i;
			// 	}
			// }
			// midY = 3 - (minY + maxY);

			// for(int i = (int)screenVertices[minY].y; i < screenVertices[midY].y; i++) {
			// 	//int a = (screenVertices[maxY].x - screenVertices[minY].x) * (i - screenVertices[minY].y) / (screenVertices[maxY].y - screenVertices[minY].y);
			// 	int a = Vector2.Lerp()
			// 	int b = ;
			// 	for(int j = screenVertices[minX]; j < 1; j++) {
			// 		points.Add(new Vector2Int(j, i));
			// 	}
			// }

			int minX = (int)screenVertices[0].x - 1;
			int maxX = (int)screenVertices[0].x - 1;
			int minY = (int)screenVertices[0].y - 1;
			int maxY = (int)screenVertices[0].y - 1;
			for(int i = 0; i < screenVertices.Count; i++) {
				minX = (int)Mathf.Min(minX, screenVertices[i].x);
				maxX = (int)Mathf.Ceil(Mathf.Max(maxX, screenVertices[i].x));
				minY = (int)Mathf.Min(minY, screenVertices[i].y);
				maxY = (int)Mathf.Ceil(Mathf.Max(maxY, screenVertices[i].y));
			}

			for(int i = minX; i<=maxX; i++) {
				for(int j = minY; j<=maxY; j++) {
					points.Add(new Vector2Int(i, j));
				}
			}

			return points;
		}
	}
}

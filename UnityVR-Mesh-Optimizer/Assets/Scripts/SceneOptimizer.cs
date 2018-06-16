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
			//System.GC.Collect();
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
			visiblityTriangle.InflateTriangle(0.001f);
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
			if(CanRemoveVertex(ref triangles, m.normals, i, threshold)) {
				List<int> vertexToFill = DeleteVertex(ref triangles, i);
				FillArea(ref triangles, vertexToFill);
				isOptimized = true;
			}
		}
		m.triangles = triangles.ToArray();
		return isOptimized;
	}

	List<int> AdjoiningTriangles(ref List<int> triangles, int vertexNumber) {
		List<int> tris = new List<int>();
		for(int i = 0; i<triangles.Count; i++) {
			if(triangles[i] == vertexNumber) {
				int a = i - (i % 3);
				tris.Add(a);
			}
		}
		return tris;
	}

	List<int> AdjacentVertices(ref List<int> triangles, List<int> adjoiningTriangles, int vertexNumber) {
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

	bool CanRemoveVertex(ref List<int> triangles, Vector3[] normals, int vertexNumber, float thresholdAngle) {
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

	List<int> DeleteVertex(ref List<int> triangles, int vertexNumber) {
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

	void FillArea(ref List<int> triangles, List<int> vertices) {
		if(vertices == null) return;
		for(int i = 1; i<vertices.Count - 1; i++) {
			triangles.Add(vertices[0]);
			triangles.Add(vertices[i]);
			triangles.Add(vertices[i + 1]);
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

		List<Pair<int, int>> screenPoints = triangle.GetScreenPoints(cam, GetSampleResolution(sampleSize));
		for(int i = 0; i< screenPoints.Count; i++) {
			if(tex2d.GetPixel(screenPoints[i].first, screenPoints[i].second) == Color.white) {
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

		public List<Pair<int, int>> GetScreenPoints(Camera cam, float renderResolution) {
			List<Vector2> screenVertices = new List<Vector2>();
			for(int i = 0; i < worldVertices.Count; i++) {
				screenVertices.Add(cam.WorldToScreenPoint(worldVertices[i]));
			}
			List<Pair<int, int>> points = new List<Pair<int, int>>();

			screenVertices.Sort(delegate(Vector2 a, Vector2 b){
				if(a.y < b.y) return -1;
				return 1;
			});

			float totalHeight = screenVertices[2].y - screenVertices[0].y;
			for(int y = (int)screenVertices[0].y; y < screenVertices[1].y; y++) {
				float segmentHeight = screenVertices[1].y - screenVertices[0].y + 1;
				float alpha = (y - screenVertices[0].y) / totalHeight;
				float beta = (y - screenVertices[0].y) / segmentHeight;

				Vector2 a = screenVertices[0] + (screenVertices[2] - screenVertices[0]) * alpha;
				Vector2 b = screenVertices[0] + (screenVertices[1] - screenVertices[0]) * beta;

				if(a.x > b.x) {
					a += b;
					b = a - b;
					a = a - b;
				}
				for(int x = (int)a.x; x <= Mathf.Ceil(b.x); x++) {
					points.Add(new Pair<int, int>(x, y));
				}
			}
			for(int y = (int)screenVertices[1].y; y <= Mathf.Ceil(screenVertices[2].y); y++) {
				float segmentHeight = screenVertices[2].y - screenVertices[1].y + 1;
				float alpha = (y - screenVertices[0].y) / totalHeight;
				float beta = (y - screenVertices[1].y) / segmentHeight;

				Vector2 a = screenVertices[0] + (screenVertices[2] - screenVertices[0]) * alpha;
				Vector2 b = screenVertices[1] + (screenVertices[2] - screenVertices[1]) * beta;

				if(a.x > b.x) {
					a += b;
					b = a - b;
					a = a - b;
				}
				for(int x = (int)a.x; x <= Mathf.Ceil(b.x); x++) {
					points.Add(new Pair<int, int>(x, y));
				}
			}
			return points;
		}
	}
}

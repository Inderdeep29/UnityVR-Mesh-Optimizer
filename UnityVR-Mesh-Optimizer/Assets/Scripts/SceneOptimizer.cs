using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SceneOptimizer : MonoBehaviour {
	enum SampleSize {_128, _256, _512, _1024, _2048};

	[SerializeField] Camera cam;
	[SerializeField] SampleSize sampleSize = SampleSize._256;

	MeshFilter visiblityTesterMesh;
	RenderTexture renderTexture;
	List<Material> materialsBackup = new List<Material>();

	List<Mesh> optimizedMeshes;

	void OnEnable () {
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
		RemoveLoneVertices(optimizedMesh);
		optimizedMeshes.Add(optimizedMesh);
		m.mesh = optimizedMesh;
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
		List<int> triangles = new List<int>();
		for(int i = 0; i<m.triangles.Length; i++) {
			triangles.Add(vertexMap[m.triangles[i]]);
		}
		//Vertices, Normals and UV's
		List<Vector3> vertices = new List<Vector3>();
		List<Vector3> normals = new List<Vector3>();
		List<Vector2> uv = new List<Vector2>();
		for(int i = 0; i<vertexMap.Length; i++) {
			if(vertexMap[i] != -1) {
				vertices.Add(m.vertices[i]);
				normals.Add(m.normals[i]);
				uv.Add(m.uv[i]);
			}
		}
		m.triangles = triangles.ToArray();
		m.vertices = vertices.ToArray();
		m.normals = normals.ToArray();
		// m.tangents
		m.uv = uv.ToArray();
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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

class Triangle {
	private List<Vector3> worldVertices;
	private List<Vector3> localVertices;
	
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

	public bool ContainsColor(Camera cam, ref Texture2D tex, Color color) {
		List<Vector2> screenVertices = new List<Vector2>();
		for(int i = 0; i < worldVertices.Count; i++) {
			screenVertices.Add(cam.WorldToScreenPoint(worldVertices[i]));
		}
		screenVertices.Sort(delegate(Vector2 a, Vector2 b){
			if(a.y < b.y) return -1;
			return 1;
		});
		float totalHeight = screenVertices[2].y - screenVertices[0].y;
		for(int y = (int)screenVertices[0].y; y <= Mathf.Ceil(screenVertices[2].y); y++) {
			bool isSecondHalf = y >= screenVertices[1].y;
			float segmentHeight = isSecondHalf ? screenVertices[2].y - screenVertices[1].y : 
				screenVertices[1].y - screenVertices[0].y;

			float alpha = (y - screenVertices[0].y) / totalHeight;
			float beta = (y - (isSecondHalf ? (screenVertices[1].y) : (screenVertices[0].y)))/ segmentHeight;

			Vector2 a = screenVertices[0] + (screenVertices[2] - screenVertices[0]) * alpha;
			Vector2 b = isSecondHalf ? screenVertices[1] + (screenVertices[2] - screenVertices[1]) * beta :
				screenVertices[0] + (screenVertices[1] - screenVertices[0]) * beta;

			if(a.x > b.x) {
				a += b;
				b = a - b;
				a = a - b;
			}
			int startX = (int)a.x;
			int endX = (int)Mathf.Ceil(b.x);
			for(int x = startX; x <= endX; x++) {
				if(x >= 0 && x <= tex.width && y >= 0 && y <= tex.height) {
					if(tex.GetPixel(x, y) == color) {
						return true;
					}
				}
			}
		}
		return false;
	}
}
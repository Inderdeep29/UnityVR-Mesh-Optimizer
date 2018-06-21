using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshEditorData {
	public bool foldout;
	public int analyzerMesh;
	private Transform current;
	private Utilities.SampleSize sampleSize;
	private float threshold;
	private List<MeshEditorData> children;

	public Transform transform {
		get { return current; }
	}

	public GameObject gameObject {
		get {
			if(current == null) return null;
			return current.gameObject;
		}
	}

	public Utilities.SampleSize sampleRenderSize {
		get { return sampleSize; }
		set { sampleSize = value; }
	}

	public float thresholdAngle {
		get { return threshold; }
		set { threshold = value; }
	}

	public List<MeshEditorData> childrenData {
		get { return children; }
	}

	public MeshEditorData(Transform curr = null, bool foldout = true, int analyzerMesh = -1) {
		this.current = curr;
		this.foldout = foldout;
		this.analyzerMesh = analyzerMesh;
		children = new List<MeshEditorData>();
	}

	public void GenerateChildData(GameObject[] children) {
		Transform[] childrenTransforms = new Transform[children.Length];
		for(int i = 0; i < childrenTransforms.Length; i++) {
			childrenTransforms[i] = children[i].transform;
		}
		GenerateChildData(childrenTransforms);
	}

	public void GenerateChildData(Transform[] childrenTransforms) {
		foreach(Transform child in childrenTransforms) {
			if(!child.gameObject.activeInHierarchy) continue;
			MeshEditorData childMeshData = new MeshEditorData(child.transform);
			Transform[] childChildren = new Transform[child.childCount];
			for(int i = 0; i < childChildren.Length; i++) {
				childChildren[i] = child.GetChild(i);
			}
			childMeshData.GenerateChildData(childChildren);
			if((childMeshData.current != null && childMeshData.current.GetComponent<MeshFilter>() != null) || childMeshData.children.Count > 0) {
				this.children.Add(childMeshData);
			}
		}
	}
}
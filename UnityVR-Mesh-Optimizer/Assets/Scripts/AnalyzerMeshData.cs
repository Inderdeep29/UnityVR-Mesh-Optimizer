using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnalyzerMeshData {
	private MeshFilter sourceMeshFilter;
	private Material originalMaterial;
	private Utilities.SampleSize sampleSize;
	private float threshold;
	private Mesh originalMesh;
	private Mesh analyzedMesh;
	private Mesh optimizedMesh;

	public Transform transform {
		get { return sourceMeshFilter.transform; }
	}

	public GameObject gameObject {
		get { return sourceMeshFilter.gameObject; }
	}

	public MeshFilter meshFilter {
		get { return sourceMeshFilter; }
	}

	public Mesh mesh {
		get { return sourceMeshFilter.sharedMesh; }
	}

	public Utilities.SampleSize sampleRenderSize {
		get { return sampleSize; }
		set { sampleSize = value; }
	}

	public int sampleResolution {
		get { return Utilities.GetSampleResolution(sampleSize); }
	}

	public float thresholdAngle {
		get { return threshold; }
		set { threshold = value; }
	}

	public Mesh OriginalMesh {
		get { return originalMesh; }
	}

	public Mesh AnalyzedMesh {
		get { return analyzedMesh; }
		set { analyzedMesh = value; }
	}

	public Mesh OptimizedMesh {
		get { return optimizedMesh; }
		set { optimizedMesh = value; }
	}

	public AnalyzerMeshData(MeshFilter sourceMeshFilter, Utilities.SampleSize sampleSize, float threshold) {
		this.sourceMeshFilter = sourceMeshFilter;
		this.originalMesh = Mesh.Instantiate(sourceMeshFilter.sharedMesh);
		if(sourceMeshFilter.gameObject.GetComponent<MeshRenderer>() != null) {
			this.originalMaterial = sourceMeshFilter.gameObject.GetComponent<MeshRenderer>().sharedMaterial;
		}
		this.sampleRenderSize = sampleSize;
		this.threshold = threshold;
	}

	public void SetMaterial(Material material) {
		if(sourceMeshFilter.gameObject.GetComponent<MeshRenderer>() != null) {
			sourceMeshFilter.gameObject.GetComponent<MeshRenderer>().material = material;
		}
	}

	public void ResetMaterial() {
		if(sourceMeshFilter.gameObject.GetComponent<MeshRenderer>() != null) {
			sourceMeshFilter.gameObject.GetComponent<MeshRenderer>().material = originalMaterial;
		}
	}
}

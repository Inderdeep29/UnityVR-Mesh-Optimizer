using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnalyzerMeshData {
	MeshFilter sourceMeshFilter;
	Mesh optimizedMesh;
	Material originalMaterial;
	int sampleResolution;
	float threshold;

	public AnalyzerMeshData(MeshFilter sourceMeshFilter, int sampleResolution, float threshold) {
		this.sourceMeshFilter = sourceMeshFilter;
		this.originalMaterial = sourceMeshFilter.gameObject.GetComponent<MeshRenderer>().sharedMaterial;
		this.sampleResolution = sampleResolution;
		this.threshold = threshold;
	}

	public void SetMaterial(Material material) {
		sourceMeshFilter.gameObject.GetComponent<MeshRenderer>().material = material;
	}

	public void ResetMaterial() {
		sourceMeshFilter.gameObject.GetComponent<MeshRenderer>().material = originalMaterial;
	}

	public float ThresholdAngle() {
		return threshold;
	}

	public int SampleResolution() {
		return sampleResolution;
	}

	public MeshFilter MeshFilter() {
		return sourceMeshFilter;
	}

	public Mesh GetMesh() {
		return sourceMeshFilter.sharedMesh;
	}

	public Transform GetTransform() {
		return sourceMeshFilter.transform;
	}

	public void SetOptimizedMesh(Mesh optimizedMesh) {
		this.optimizedMesh = optimizedMesh;
	}

	public Mesh GetOptimizedMesh() {
		return optimizedMesh;
	}
}

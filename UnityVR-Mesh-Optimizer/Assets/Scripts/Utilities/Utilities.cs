using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Utilities {
	public enum SampleSize {_32, _64, _128, _256, _512, _1024, _2048};

	public static int GetSampleResolution(SampleSize size) {
		switch(size) {
			case SampleSize._32:
				return 32;
			case SampleSize._64:
				return 64;
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

	public static Material GetUnlitMaterial(Color col) {
		Material m = new Material(Shader.Find("Unlit/Color"));
		m.color = col;
		return m;
	}
}

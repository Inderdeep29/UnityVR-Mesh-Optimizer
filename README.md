# UnityVR-Mesh-Optimizer
Reduce geometry of meshes, optimised for 3DOF VR experience.

To Use:
1. Open Project in Unity and open your desired scene with geometry to optimize.
2. In menubar, goto "IDS->Optimize Scene for 3DOF".
3. In the Editor window that opens, Enter player position or transform.
4. Click Analyze button and wait. This step can take time.
5. Click Generate.

Process: 
1. First, it does back culling and remove all triangles which will not be visible to user.
2. Then, it uses a threshhold angle value to simplify geometry while maintaining the same silhouette.

# -- Work in Progress--
3. Output the obj files generated, with option to combine all meshes into one as well as with sharing the same material so as to reduce batching.
4. Allow for limited 6DOF


In 100k triangles test environment, it was able to simplify it to 12k triangles without any noticable difference in appearance.

# Next commit will focus on Editor improvements + possible possible improvements
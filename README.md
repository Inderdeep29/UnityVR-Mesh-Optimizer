# UnityVR-Mesh-Optimizer
Reduce geometry of meshes, optimised for 3DOF VR experience.

# Currently, it will run in playmode only since I havn't yet implemented Editor interface/ability to export generated obj's. Next commit should enable this functionality.

Process: 
1. First, it does back culling and remove all triangles which will not be visible to user.
2. Then, it uses a threshhold angle value to simplify geometry while maintaining the same silhouette.

# -- Work in Progress--
3. Output the obj files generated, with option to combine all meshes into one as well as with sharing the same material so as to reduce batching.


In 100k triangles test environment, it was able to simplify it to 12k triangles without any noticable difference in appearance.

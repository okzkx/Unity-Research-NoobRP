using UnityEngine;

public class MeshBall : MonoBehaviour {

	static int baseColorId = Shader.PropertyToID("_BaseColor");

	[SerializeField]
	Mesh mesh = default;

	[SerializeField]
	Material material = default;

	Matrix4x4[] matrices = new Matrix4x4[1023];
	Vector4[] baseColors = new Vector4[1023];

	MaterialPropertyBlock block;

	void Awake() {
		for (int i = 0; i < matrices.Length; i++) {
			matrices[i] = Matrix4x4.TRS(
				Random.insideUnitSphere * 10f, Quaternion.identity, Vector3.one
			);
			baseColors[i] =
				new Vector4(Random.value, Random.value, Random.value, 1f);
		}
	}

	void Update() {
		if (block == null) {
			block = new MaterialPropertyBlock();
			block.SetVectorArray(baseColorId, baseColors);
		}
		Graphics.DrawMeshInstanced(mesh, 0, material, matrices, 1023, block);
	}
}
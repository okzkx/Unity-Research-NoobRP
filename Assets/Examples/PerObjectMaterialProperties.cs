using UnityEngine;

[DisallowMultipleComponent]
public class PerObjectMaterialProperties : MonoBehaviour {

	static int baseColorId = Shader.PropertyToID("_BaseColor");

	[SerializeField]
	Color baseColor = Color.white;

	static MaterialPropertyBlock block;
	void Awake() {
		OnValidate();
	}

	void OnValidate() {
		if (block == null) {
			block = new MaterialPropertyBlock();
		}
		block.SetColor(baseColorId, baseColor);
		GetComponent<Renderer>().SetPropertyBlock(block);
	}
}
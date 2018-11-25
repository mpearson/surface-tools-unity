using System.Collections;
using UnityEngine;

namespace Doublemice.Geometry.Primitives {
  public class IcoSphere : BetterMonoBehaviour {
    public MeshFilter leafMeshFilter;
    public MeshRenderer leafMeshRenderer;
    public Mesh leafMesh;
    public Material leafMaterial;

    public void Awake() {
      // this.water = new LiquidVolume(0.0f);
      // this.stats = new PlantStats(0.0f);
      // this.flowers = new List<Flower>();
    }

    public override void Start() {
      this.leafMeshFilter = this.gameObject.AddComponent<MeshFilter>();
      this.leafMeshRenderer = this.gameObject.AddComponent<MeshRenderer>();
      this.leafMeshRenderer.material = this.leafMaterial;
    }
  }

}

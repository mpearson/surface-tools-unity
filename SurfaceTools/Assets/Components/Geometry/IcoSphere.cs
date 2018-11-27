using System.Collections;
using UnityEngine;

namespace Doublemice.Geometry.Primitives {
  [ExecuteInEditMode]
  [RequireComponent(typeof(MeshRenderer))]
  [RequireComponent(typeof(MeshFilter))]
  public class IcoSphere : MonoBehaviour {
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh mesh;
    private IcoSphereGeometry geometry;
    private float renderedRadius = -1f;
    private float renderedSubdivisions = -1f;

    [Range(0, 5f)]
    public float radius = 0.1f;
    [Range(0, 5)]
    public int subdivisions = 0;

    public void Awake() {
      this.meshRenderer = this.GetComponent<MeshRenderer>();
      this.meshFilter = this.GetComponent<MeshFilter>();
    }

    public void OnEnable() {
      this.mesh = new Mesh();
      this.meshFilter.mesh = this.mesh;
      this.geometry = new IcoSphereGeometry();
      this.Regenerate();
    }

    public void Update() {
      if (this.radius != this.renderedRadius || this.subdivisions != this.renderedSubdivisions) {
        this.Regenerate();
      }
    }

    private void Regenerate() {
      this.geometry.Generate(this.subdivisions, this.radius);
      this.mesh.vertices = this.geometry.vertices;
      this.mesh.triangles = this.geometry.triangles;
      this.mesh.normals = this.geometry.normals;
      this.renderedRadius = this.radius;
    }
  }
}

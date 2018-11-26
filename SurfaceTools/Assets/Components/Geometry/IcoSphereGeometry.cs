using System.Collections;
using UnityEngine;

namespace Doublemice.Geometry.Primitives {
  public class IcoSphereGeometry {
    public Vector3[] vertices;
    public int[] triangles;

    public IcoSphereGeometry() { }

    public IcoSphereGeometry(int subdivisions, float edgeLength) {
      this.Generate(subdivisions, edgeLength);
    }

    /// <summary>
    /// Generate an icosahedron with edge length <paramref name="len" />
    /// </summary>
    public void Generate(int subdivisions, float len) {
      // thanks to Andreas Kahler for this method:
      // http://blog.andreaskahler.com/2009/06/creating-icosphere-mesh-in-code.html

      float t = (1f + Mathf.Sqrt(5f)) * 0.5f * len;

      this.vertices = new Vector3[] {
        // z = 0 plane
        new Vector3(-len,  t,  0),
        new Vector3( len,  t,  0),
        new Vector3(-len, -t,  0),
        new Vector3( len, -t,  0),
        // x = 0 plane
        new Vector3( 0, -len,  t),
        new Vector3( 0,  len,  t),
        new Vector3( 0, -len, -t),
        new Vector3( 0,  len, -t),
        // y = 0 plane
        new Vector3( t,  0, -len),
        new Vector3( t,  0,  len),
        new Vector3(-t,  0, -len),
        new Vector3(-t,  0,  len),
      };

      this.triangles = new int[] {
        // 5 faces around point 0
        0,  11, 5,
        0,  5,  1,
        0,  1,  7,
        0,  7,  10,
        0,  10, 11,
        // 5 adjacent faces
        1,  5,  9,
        5,  11, 4,
        11, 10, 2,
        10, 7,  6,
        7,  1,  8,
        // 5 faces around point 3
        3,  9,  4,
        3,  4,  2,
        3,  2,  6,
        3,  6,  8,
        3,  8,  9,
        // 5 adjacent faces
        4,  9,  5,
        2,  4,  11,
        6,  2,  10,
        8,  6,  7,
        9,  8,  1,
      };
    }
  }
}

using System.Collections;
using UnityEngine;

namespace Doublemice.Geometry.Primitives {


  public class IcoSphereGeometry {
    private static readonly float RADIUS_TO_EDGE_LENGTH = Mathf.Sin(Mathf.PI * 2f / 5f);

    public Vector3[] vertices;
    public Vector3[] normals;
    public int[] triangles;

    public IcoSphereGeometry() { }

    public IcoSphereGeometry(int subdivisions, float radius) {
      this.Generate(subdivisions, radius);
    }

    /// <summary>
    /// Generate an icosahedron with approximate radius <paramref name="radius" />
    /// </summary>
    public void Generate(int subdivisions, float radius) {
      // thanks to Andreas Kahler for this method:
      // http://blog.andreaskahler.com/2009/06/creating-icosphere-mesh-in-code.html

      float len = radius * RADIUS_TO_EDGE_LENGTH;

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

      this.normals = new Vector3[this.vertices.Length];

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

      this.NormalizeVertRadii(radius);
    }

    public void NormalizeVertRadii(float radius) {
      for (int i = 0; i < this.vertices.Length; i++) {
        this.normals[i] = this.vertices[i].normalized;
        this.vertices[i] = this.normals[i] * radius;
      }
    }


    // def subdivide(self):
    //     # allocate memory for new vertices
    //     old_vert_count = self.vertices.shape[0]
    //     new_vert_count = (old_vert_count * 4) - 6
    //     new_vertices = np.empty((new_vert_count, 3), dtype=np.float32)
    //     new_vertices[:old_vert_count] = self.vertices
    //     next_vert_index = old_vert_count

    //     # allocate memory for new faces
    //     old_face_count = self.faces.shape[0]
    //     new_face_count = old_face_count * 4
    //     new_faces = np.empty((new_face_count, 3), dtype=np.intp)
    //     next_face_index = 0

    //     edge_dict = {}

    //     for i in range(old_face_count):
    //         #         B
    //         #         *
    //         #        / \
    //         #       /   \
    //         #   AB *-----* BC
    //         #     / \   / \
    //         #    /   \ /   \
    //         #   *-----*-----*
    //         #  A     CA      C

    //             A, B, C = self.faces[i]

    //             AB, next_vert_index = self._add_midpoint_vertex(A, B, new_vertices, edge_dict, next_vert_index);
    //             BC, next_vert_index = self._add_midpoint_vertex(B, C, new_vertices, edge_dict, next_vert_index);
    //             CA, next_vert_index = self._add_midpoint_vertex(C, A, new_vertices, edge_dict, next_vert_index);

    //             new_faces[next_face_index:next_face_index + 4] = (
    //                 (A,  AB, CA),
    //                 (B,  BC, AB),
    //                 (C,  CA, BC),
    //                 (AB, BC, CA),
    //             )
    //             next_face_index += 4

    //     self.vertices = new_vertices
    //     self.faces = new_faces

    // @staticmethod
    // def _add_midpoint_vertex(A, B, new_vertices, edge_dict, next_vert_index):
    //     edge_key = (A, B) if A <= B else (B, A)
    //     midpoint_index = edge_dict.get(edge_key, None)
    //     if midpoint_index is None:
    //         midpoint_index = next_vert_index
    //         edge_dict[edge_key] = midpoint_index
    //         # add new vertex
    //         new_vertices[midpoint_index] = (new_vertices[A] + new_vertices[B]) * 0.5
    //         next_vert_index += 1

    //     return midpoint_index, next_vert_index


    // def normalize_vert_radii(self):
    //     self.normals = self.vertices / np.linalg.norm(self.vertices, axis=1, keepdims=True)
    //     self.vertices = self.normals * self.radius


  }
}

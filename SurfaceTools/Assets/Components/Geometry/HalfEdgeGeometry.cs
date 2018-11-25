using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Doublemice.Geometry {

public class HalfEdge {
  public int prev = -1;
  public int next = -1;
  public int pair = -1;
  public int vert = -1;
  public int face = -1;
  public bool deleted = false;
  public float lengthSq = -1;
}

public class HalfEdgeGeometry {
  public class DegenerateTriangleError : Exception {
    public DegenerateTriangleError(string msg) : base(msg) { }
  }

  public Vector3[] vertices;
  public int[] triangles;

  public HalfEdge[] edges;
  public int[] vertKeyEdges;
  public Stack<int> freeEdges;
  public Stack<int> freeFaces;
  public Stack<int> freeVerts;

  public float volume;
  public Vector3 centroid;

  // public HalfEdgeGeometry() {

  // }

  public HalfEdgeGeometry(Mesh mesh) {
    this.vertices = mesh.vertices;
    this.triangles = mesh.triangles;

    this.vertKeyEdges = new int[this.vertices.Length];
    this.edges = new HalfEdge[this.triangles.Length * 3];
    this.freeEdges = new Stack<int>();
    this.freeFaces = new Stack<int>();
    this.freeVerts = new Stack<int>();

    // loop through faces and build a half-edge for each edge of each face,
    // containing one vertex, one face and some other stuff
    for (int faceOffset = 0; faceOffset < triangles.Length; faceOffset++) {
      if (this.isDegenerateFace(faceOffset))
        throw new DegenerateTriangleError(String.Format("Degenerate triangle at index {0}", faceOffset));

      // if we wanted to support arbitrary polygons, we just have to change "3" to the real number of sides
      // (note that "edgeCount" above would also have to be corrected, and other stuff would probably break too)
      for (int edgeOffset = 0; edgeOffset < 3; edgeOffset++) {
        int edgeIndex = faceOffset + edgeOffset;
        HalfEdge edge = this.createHalfEdge(faceOffset, edgeOffset);
        this.edges[edgeIndex] = edge;
        this.vertKeyEdges[edge.vert] = edge.next; // key edges point away from their vertex
      }
    }

    this.findEdgePairs();
  }

  // determine if any two vertices of the triangle are equal,
  // which means the face has zero area (degenerate scum!)
  public bool isDegenerateFace(int faceOffset) {
    int vertA = this.triangles[faceOffset];
    int vertB = this.triangles[faceOffset + 1];
    int vertC = this.triangles[faceOffset + 2];
    return vertA == vertB || vertA == vertC || vertB == vertC;
  }

  private HalfEdge createHalfEdge(int faceOffset, int edgeOffset) {

    //         B    Half-edge shown has vert = A, face = ABC,
    //        / \     and might be the key edge of C
    //       /   \
    //      /     \
    //     /       \
    //    /   --->  \
    //   C --------- A

    int edgeIndex = faceOffset + edgeOffset;
    int vertIndex = this.triangles[edgeIndex];
    int prevEdgeIndex = faceOffset + ((edgeOffset - 1) % 3);
    int nextEdgeIndex = faceOffset + ((edgeOffset + 1) % 3);

    HalfEdge edge = new HalfEdge();
    edge.vert = vertIndex;
    edge.face = faceOffset;
    edge.prev = prevEdgeIndex;
    edge.next = nextEdgeIndex;

    return edge;
  }

  // find the edge pairs, O(N^2) sadly
  private void findEdgePairs() {
    for (int i = 0; i < edges.Length; i++) {
      HalfEdge edge = this.edges[i];
      if (edge.pair != -1)
          continue;

      int A = edge.vert; // destination vertex of this edge
      int B = this.edges[edge.prev].vert; // source vertex

      for (int j = i + 1; j < edges.Length; j++) {
        HalfEdge pairCandidate = this.edges[j];
        if (pairCandidate.pair == -1 && pairCandidate.vert == B && this.edges[pairCandidate.prev].vert == A) {
          edge.pair = j;
          pairCandidate.pair = i;
          break;
        }
      }
      if (edge.pair == -1)
        throw new Exception("Could not find opposite vertex! What kind of mesh is this exactly??");
    }
  }

  // Checks that all edge pairs match (pair of A is b, and pair of B is A)
  // Shouldn't be necessary once all the mesh modifier functions work correctly.
  public bool checkEdgePairs() {
    for (int i = 0; i < this.edges.Length; i++) {
      var edge = this.edges[i];
      if (edge.deleted)
        continue;
      if (this.edges[edge.pair].pair != i)
        return false;
    }

    return true;
  }

  // nifty little algorithm that sums positive and negative volumes
  // of tetrahedrons from the origin to each face.
  // also calculates the center of mass of the mesh
  public void calculateVolume() {
    this.volume = 0;
    this.centroid = Vector3.zero;

    for (int i = 0; i < triangles.Length; ) {
      if (this.isDegenerateFace(i))
        continue;

      Vector3 A = this.vertices[this.triangles[i++]];
      Vector3 B = this.vertices[this.triangles[i++]];
      Vector3 C = this.vertices[this.triangles[i++]];

      float simplexVolume = Vector3.Dot(A, Vector3.Cross(B, C));
      this.volume += simplexVolume;
      this.centroid += (A + B + C) * (simplexVolume * 0.25f);
    }
  }

  public void removeEdge(int i) {
    Debug.LogFormat("Removing edge {0}", i);

    this.edges[i].deleted = true;
    this.freeEdges.Push(i);
  }

  public void removeFace(int i) {
    Debug.LogFormat("Removing face {0}", i);

    this.triangles[i] = 0;
    this.triangles[i + 1] = 0;
    this.triangles[i + 2] = 0;
    this.freeFaces.Push(i);
  }

  public void removeVert(int i) {
    Debug.LogFormat("Removing vert {0}", i);

    this.vertices[i] = Vector3.zero;
    this.vertKeyEdges[i] = -1;
    this.freeVerts.Push(i);
  }

  // public void mergeEdge(int AX) {

  //   log('Merging edge '+AX)

  //   var i, count, edge, lastEdge, face,
  //       vertices = geometry.vertices,
  //       faces = geometry.faces,
  //       edgeVert = geometry.edges.vert,
  //       keyEdge = geometry.edges.key,
  //       edgePair = geometry.edges.pair,
  //       edgeNext = geometry.edges.next,
  //       edgeFace = geometry.edges.face;

  //   //  -*-------L------L2---   we want to delete the faces AXL and ARX,
  //   //  / \     /`\     / \     vertex X, edges LX and RX,
  //   //     \   /```\   /   \    and reconnect L2, L3, R2, R3, etc to A
  //   //      \ /`````\ /     \
  //   //  -----A-------X-------*
  //   //      / \`````/ \     /
  //   //     /   \```/   \   /
  //   //  \ /     \`/     \ /
  //   //  -*-------R-------R2---

  //   var XA = edgePair[AX],
  //       XL = edgeNext[AX], LA = edgeNext[XL],
  //       AR = edgeNext[XA], RX = edgeNext[AR],
  //       XR = edgePair[RX], LX = edgePair[XL],
  //       RR2 = edgeNext[XR], R2X = edgeNext[RR2],
  //       XL2 = edgeNext[LX], L2L = edgeNext[XL2];

  //   var A = edgeVert[XA],
  //       R = edgeVert[XR],
  //       L = edgeVert[XL],
  //       L2 = edgeVert[XL2],
  //       R2 = edgeVert[RR2],
  //       X = edgeVert[AX];

  //   if(XA === LA || AX === AR)
  //       // return;
  //       throw "ugh"; // shit

  //   if(X === R || X === L)
  //       // return;
  //       throw "wat"; // shit

  //   // make sure vertices A, R and L don't end up without key edges
  //   keyEdge[A] = AR;
  //   keyEdge[L] = LA;
  //   keyEdge[R] = RR2;

  //   // replace faces that are about to be deleted
  //   edgeFace[LA] = edgeFace[LX];
  //   edgeFace[AR] = edgeFace[XR];

  //   // loop through the "spokes" of vertex X
  //   var n = 2, edge = LX;
  //   while(edge !== RX) {
  //       // replace vertex X with A in the face
  //       face = faces[edgeFace[edge]];

  //       if(face.a === X)
  //           face.a = A;
  //       else if(face.b === X)
  //           face.b = A;
  //       else if(face.c === X)
  //           face.c = A;
  //       else
  //           throw 'uh-oh: '+face.a+', '+face.b+', '+face.c;

  //       // update the vertex of each inward edge
  //       edgeVert[edge] = A;

  //       edge = edgePair[edgeNext[edge]];

  //       if(n++ > 20)
  //           throw 'whoops, infinite loop on aisle 3!';
  //   }

  //   // repair the next-edge relationships

  //   //  ---------------L--     degenerate case where X
  //   //  \          _-'/|\      has only 3 neighbors
  //   //   \      _-'  / | \
  //   //    \  _-'    /  |
  //   //  ---A-------X   |
  //   //    / `-._    \  |
  //   //   /      `-._ \ | /
  //   //  /           `-\|/
  //   //  ---------------R---

  //   // if(R === L2) { // also R2 === L
  //   if(RR2 === L2L) {
  //       edgeNext[LA] = AR;
  //       edgeNext[L2L] = LA;

  //   } else {
  //       edgeNext[LA] = XL2;
  //       edgeNext[R2X] = AR;
  //       edgeNext[L2L] = LA;
  //   }
  //   edgeNext[AR] = RR2;

  //   // delete stuff
  //   this.removeFace(geometry, edgeFace[AX]);
  //   this.removeFace(geometry, edgeFace[XA]);

  //   this.removeEdge(geometry, AX);
  //   this.removeEdge(geometry, XA);
  //   this.removeEdge(geometry, LX);
  //   this.removeEdge(geometry, XL);
  //   this.removeEdge(geometry, RX);
  //   this.removeEdge(geometry, XR);


  //   this.removeVert(geometry, X);


  //   // check if anything didn't get deleted good
  //   var removedEdges = [AX, XA, LX, XL, RX, XR];
  //   var deleted = geometry.edges.deleted;


  //   return;

  //   //diagnostics
  //   HalfEdge.checkEdgePairs(geometry);

  //   for(i=0, count=keyEdge.length; i<count; i++) {
  //       if(i === X || keyEdge[i] === null)
  //           continue;
  //       if(edgeVert[edgePair[keyEdge[i]]] !== i)
  //           throw("uh-oh");
  //   }


  //   for(i=0, count=edgeVert.length; i<count; i++) {
  //       if(deleted[i])
  //           continue;

  //       if(deleted[edgeNext[i]])
  //           throw("UGHHHH");
  //       if(deleted[edgePair[i]])
  //           throw("UGHHHH");
  //       if(edgeVert[i] == X)
  //           throw("UGHHHH");
  //   }

  //   for(i=0, count=keyEdge.length; i<count; i++) {
  //       if(geometry.freeVerts.indexOf(i) > -1)
  //           continue;
  //       if(deleted[keyEdge[i]])
  //           throw("UGHHHH");
  //   }


  //   for(i=0, count=edgeVert.length; i<count; i++) {
  //       if(deleted[i])
  //           continue;
  //       if(edgeVert[i] == X)
  //           throw("UGHHHH");
  //   }
  // }

  public int splitEdge(int AB) {
    // var freeFaces = geometry.freeFaces,
    //     freeVerts = geometry.freeVerts,
    //     freeEdges = geometry.freeEdges,
    //     edgeVert = geometry.edges.vert,
    //     edgePair = geometry.edges.pair,
    //     edgeNext = geometry.edges.next,
    //     edgeFace = geometry.edges.face,
    //     deleted = geometry.edges.deleted,
    //     keyEdge = geometry.edges.key,
    //     face,
    //     faces = geometry.faces;

    if(this.freeFaces.Count < 2 || this.freeVerts.Count == 0 || this.freeEdges.Count < 6)
        return -1; // this edge lives to see another day....for now

    //          B
    //        .'|'.
    //      .'  |  '.
    //    .'    |    '.
    //  L       |       R
    //    `.    |    .'
    //      `.  |  .'
    //        `.|.'
    //          A

    int BA = this.edges[AB].pair;
    int AR = this.edges[BA].next;
    int RB = this.edges[AR].next;
    int BL = this.edges[AB].next;
    int LA = this.edges[BL].next;

    Debug.LogFormat("Splitting edge {0} [{1}->{2}]", AB, this.edges[BA].vert, this.edges[AB].vert);

    int A = this.edges[BA].vert;
    int B = this.edges[AB].vert;
    int R = this.edges[AR].vert;
    int L = this.edges[BL].vert;
    int X = this.freeVerts.Pop();

    int AXL = this.edges[AB].face; // reusing ABL
    int XBL = this.edges[BA].face; // reusing BAR

    int ARX = freeFaces.Pop();
    int XRB = freeFaces.Pop();

    Debug.LogFormat("Reinserting vertex {0}", X);

    this.removeEdge(AB);
    this.removeEdge(BA);

    int AX = freeEdges.Pop();
    int XA = freeEdges.Pop();
    int XB = freeEdges.Pop();
    int BX = freeEdges.Pop();
    int XL = freeEdges.Pop();
    int LX = freeEdges.Pop();
    int XR = freeEdges.Pop();
    int RX = freeEdges.Pop();

    Debug.LogFormat("Reinserting edges: {0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}", AX, XA, XB, BX, XL, LX, XR, RX);

    this.edges[AX].deleted = false;
    this.edges[XA].deleted = false;
    this.edges[XB].deleted = false;
    this.edges[BX].deleted = false;
    this.edges[XL].deleted = false;
    this.edges[LX].deleted = false;
    this.edges[XR].deleted = false;
    this.edges[RX].deleted = false;

    this.triangles[AXL]     = A;
    this.triangles[AXL + 1] = X;
    this.triangles[AXL + 2] = L;

    this.triangles[XBL]     = X;
    this.triangles[XBL + 1] = B;
    this.triangles[XBL + 2] = L;

    this.triangles[ARX]     = A;
    this.triangles[ARX + 1] = R;
    this.triangles[ARX + 2] = X;

    this.triangles[XRB]     = X;
    this.triangles[XRB + 1] = R;
    this.triangles[XRB + 2] = B;

    //          B
    //        .'|'.
    //      .'  |  '.
    //    .'    |    '.
    //  L- - - -X- - - -R
    //    `.    |    .'
    //      `.  |  .'
    //        `.|.'
    //          A

    this.edges[BL].next = LX;
    this.edges[BL].face = XBL;

    this.edges[LA].next = AX;
    this.edges[LA].face = AXL;

    this.edges[AR].next = RX;
    this.edges[AR].face = ARX;

    this.edges[RB].next = BX;
    this.edges[RB].face = XRB;

    // HalfEdge edge = this.edges[AX];
    this.edges[AX].next = XL;
    this.edges[AX].vert = X;
    this.edges[AX].face = AXL;
    this.edges[AX].pair = XA;

    this.edges[XA].next = AR;
    this.edges[XA].vert = A;
    this.edges[XA].face = ARX;
    this.edges[XA].pair = AX;

    this.edges[XB].next = BL;
    this.edges[XB].vert = B;
    this.edges[XB].face = XBL;
    this.edges[XB].pair = BX;

    this.edges[BX].next = XR;
    this.edges[BX].vert = X;
    this.edges[BX].face = XRB;
    this.edges[BX].pair = XB;

    this.edges[XL].next = LA;
    this.edges[XL].vert = L;
    this.edges[XL].face = AXL;
    this.edges[XL].pair = LX;

    this.edges[LX].next = XB;
    this.edges[LX].vert = X;
    this.edges[LX].face = XBL;
    this.edges[LX].pair = XL;

    this.edges[XR].next = RB;
    this.edges[XR].vert = R;
    this.edges[XR].face = XRB;
    this.edges[XR].pair = RX;

    this.edges[RX].next = XA;
    this.edges[RX].vert = X;
    this.edges[RX].face = ARX;
    this.edges[RX].pair = XR;

    // reset key edges
    this.vertKeyEdges[X] = XA;
    this.vertKeyEdges[A] = AR;
    this.vertKeyEdges[B] = BL;
    this.vertKeyEdges[L] = LX;
    this.vertKeyEdges[R] = RX;


    return X;


    //diagnostics
    // var i, count, deleted = geometry.edges.deleted;


    // for(i=0, count=keyEdge.length; i<count; i++) {
    //     if(keyEdge[i] === null)
    //         continue;
    //     if(edgeVert[edgePair[keyEdge[i]]] !== i)
    //         throw("uh-oh");
    // }


    // for(i=0, count=edgeVert.length; i<count; i++) {
    //     if(deleted[i])
    //         continue;

    //     if(deleted[edgeNext[i]])
    //         throw("UGHHHH");
    //     if(deleted[edgePair[i]])
    //         throw("UGHHHH");


    //     if(edgeNext[edgeNext[edgeNext[i]]] !== i)
    //         throw('aint no triangle up in heah!');
    // }

    // for(i=0, count=keyEdge.length; i<count; i++) {
    //     if(keyEdge[i] === null && freeVerts.indexOf(i) == -1)
    //         continue;
    //     if(deleted[keyEdge[i]])
    //         throw("UGHHHH");
    // }

    // return X;
  }
}

}

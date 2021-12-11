using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Doublemice.Geometry {

  public class HalfEdge {
    public HalfEdge prev = null;
    public HalfEdge next = null;
    public HalfEdge pair = null;
    public int vert = -1;
    public int face = -1;
    public bool deleted = false;
    public float lengthSq = -1;

    // public HalfEdge() { }

    public void Set(int face, int vert, HalfEdge next, HalfEdge prev, HalfEdge pair = null) {
      this.face = face;
      this.vert = vert;
      this.next = next;
      this.prev = prev;
      this.pair = pair;
    }
  }

  public class HalfEdgeGeometry {
    public class DegenerateTriangleError : Exception {
      public DegenerateTriangleError(string msg) : base(msg) { }
    }

    public Vector3[] vertices;
    public int[] triangles;

    public HalfEdge[] edges;
    public HalfEdge[] vertKeyEdges;
    public Stack<HalfEdge> freeEdges;
    public Stack<int> freeFaces;
    public Stack<int> freeVerts;

    public float volume;
    public Vector3 centroid;

    public HalfEdgeGeometry() {

    }

    public HalfEdgeGeometry(Mesh mesh) {
      this.vertices = mesh.vertices;
      this.triangles = mesh.triangles;

      this.vertKeyEdges = new HalfEdge[this.vertices.Length];
      this.edges = new HalfEdge[this.triangles.Length * 3];
      for (int i = 0; i < this.edges.Length; i++)
        this.edges[i] = new HalfEdge();

      this.freeEdges = new Stack<HalfEdge>();
      this.freeFaces = new Stack<int>();
      this.freeVerts = new Stack<int>();

      // loop through faces and build a half-edge for each edge of each face,
      // containing one vertex, one face, and references to the preceding and following edges
      for (int faceOffset = 0; faceOffset < triangles.Length; faceOffset += 3) {
        if (this.isDegenerateFace(faceOffset))
          throw new DegenerateTriangleError(String.Format("Degenerate triangle at index {0}", faceOffset));

        //        B    Half-edge A shown has vert = A, face = ABC,
        //       / \     and might be the key edge of C
        //      /   \
        //     /     \
        //    /  ---> \
        //   C ------- A

        // if we wanted to support arbitrary polygons, we just have to change "3" to the real number of sides
        // (note that "edgeCount" above would also have to be corrected, and other stuff would probably break too)
        for (int edgeOffset = 0; edgeOffset < 3; edgeOffset++) {
          HalfEdge edge = this.edges[faceOffset];
          HalfEdge next = this.edges[faceOffset + ((edgeOffset + 1) % 3)];
          HalfEdge prev = this.edges[faceOffset + ((edgeOffset - 1) % 3)];
          edge.Set(faceOffset, this.triangles[faceOffset + edgeOffset], next, prev);
          this.vertKeyEdges[edge.vert] = prev; // key edges point away from their vertex
        }
      }

      this.FindEdgePairs();
    }

    // determine if any two vertices of the triangle are equal,
    // which means the face has zero area (degenerate scum!)
    public bool isDegenerateFace(int faceOffset) {
      int vertA = this.triangles[faceOffset + 0];
      int vertB = this.triangles[faceOffset + 1];
      int vertC = this.triangles[faceOffset + 2];
      return vertA == vertB || vertA == vertC || vertB == vertC;
    }

    // find the edge pairs, O(N^2) sadly
    private void FindEdgePairs() {
      for (int i = 0; i < edges.Length; i++) {
        HalfEdge edge = this.edges[i];
        if (edge.pair != null)
            continue;

        int A = edge.vert; // destination vertex of this edge
        int B = edge.prev.vert; // source vertex

        for (int j = i + 1; j < edges.Length; j++) {
          HalfEdge pairCandidate = this.edges[j];
          if (pairCandidate.pair == null && pairCandidate.vert == B && pairCandidate.prev.vert == A) {
            edge.pair = pairCandidate;
            pairCandidate.pair = edge;
            break;
          }
        }
        if (edge.pair == null)
          throw new Exception("Could not find opposite vertex! What kind of mesh is this exactly??");
      }
    }

    // Checks that all edge pairs match (pair of A is b, and pair of B is A)
    // Shouldn't be necessary once all the mesh modifier functions work correctly.
    protected bool CheckEdgePairs() {
      for (int i = 0; i < this.edges.Length; i++) {
        var edge = this.edges[i];
        if (edge.deleted)
          continue;
        if (edge.pair.pair != edge)
          return false;
      }

      return true;
    }

    // nifty little algorithm that sums positive and negative volumes
    // of tetrahedrons from the origin to each face.
    // also calculates the center of mass of the mesh
    public void CalculateVolume() {
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

    protected void RemoveEdge(HalfEdge edge) {
      Debug.Log("Removing edge");

      edge.deleted = true;
      this.freeEdges.Push(edge);
    }

    protected HalfEdge CreateEdge() {
      HalfEdge edge = this.freeEdges.Pop();
      edge.deleted = false;
      return edge;
    }

    protected void RemoveFace(int i) {
      Debug.LogFormat("Removing face {0}", i);

      this.triangles[i] = 0;
      this.triangles[i + 1] = 0;
      this.triangles[i + 2] = 0;
      this.freeFaces.Push(i);
    }

    protected int CreateFace(int a, int b, int c) {
      int faceOffset = this.freeFaces.Pop();
      this.triangles[faceOffset + 0] = a;
      this.triangles[faceOffset + 1] = b;
      this.triangles[faceOffset + 2] = c;
      return faceOffset;
    }

    protected void RemoveVert(int i) {
      Debug.LogFormat("Removing vert {0}", i);

      // this.vertices[i] = Vector3.zero;
      this.vertKeyEdges[i] = null;
      this.freeVerts.Push(i);
    }

    protected int CreateVert(HalfEdge keyEdge) {
      int face = this.freeFaces.Pop();
      this.vertKeyEdges[face] = keyEdge;
      return face;
    }

    public void MergeEdge(HalfEdge AX) {

      //  -*-------L------L2---   we want to delete the faces AXL and ARX,
      //  / \     /`\     / \     vertex X, edges LX and RX,
      //     \   /```\   /   \    and reconnect L2, L3, R2, R3, etc to A
      //      \ /`````\ /     \
      //  -----A-------X-------*
      //      / \`````/ \     /
      //     /   \```/   \   /
      //  \ /     \`/     \ /
      //  -*-------R-------R2---

      HalfEdge XA = AX.pair;
      HalfEdge XL = AX.next;
      HalfEdge LA = XL.next;
      HalfEdge AR = XA.next;
      HalfEdge RX = AR.next;
      HalfEdge XR = RX.pair;
      HalfEdge LX = XL.pair;
      HalfEdge RR2 = XR.next;
      HalfEdge R2X = RR2.next;
      HalfEdge XL2 = LX.next;
      HalfEdge L2L = XL2.next;

      Debug.LogFormat("Merging edge between verts [{1}->{2}]", XA.vert, AX.vert);

      int A = XA.vert;
      int R = XR.vert;
      int L = XL.vert;
      int L2 = XL2.vert;
      int R2 = RR2.vert;
      int X = AX.vert;

      // make sure vertices A, R and L don't end up without key edges
      this.vertKeyEdges[A] = AR;
      this.vertKeyEdges[L] = LA;
      this.vertKeyEdges[R] = RR2;

      // replace faces that are about to be deleted
      LA.face = LX.face;
      AR.face = XR.face;

      // loop through the "spokes" of vertex X
      int n = 2;
      HalfEdge edge = LX;
      while(edge != RX) {
        // replace vertex X with A in the face
        for (int i = 0; i < 3; i++)
          if (this.triangles[edge.face + i] == X)
            this.triangles[edge.face + i] = A;

        // update the vertex of each inward edge
        edge.vert = A;
        edge = edge.next.pair;

        if(n++ > 20)
            throw new System.Exception("whoops, infinite loop on aisle 3!");
      }

      // repair the next-edge relationships

      //  ---------------L--     degenerate case where X
      //  \          _-'/|\      has only 3 neighbors
      //   \      _-'  / | \
      //    \  _-'    /  |
      //  ---A-------X   |
      //    / `-._    \  |
      //   /      `-._ \ | /
      //  /           `-\|/
      //  ---------------R---

      // if(R === L2) { // also R2 === L
      if(RR2 == L2L) {
        LA.next = AR;
        L2L.next = LA;
      } else {
        LA.next = XL2;
        R2X.next = AR;
        L2L.next = LA;
      }
      AR.next = RR2;

      // delete stuff
      this.RemoveFace(AX.face);
      this.RemoveFace(XA.face);

      this.RemoveEdge(AX);
      this.RemoveEdge(XA);
      this.RemoveEdge(LX);
      this.RemoveEdge(XL);
      this.RemoveEdge(RX);
      this.RemoveEdge(XR);


      this.RemoveVert(X);


      // check if anything didn't get deleted good
      // var removedEdges = [AX, XA, LX, XL, RX, XR];
      // var deleted = geometry.edges.deleted;


      // return;

      // //diagnostics
      // HalfEdge.checkEdgePairs(geometry);

      // for(i=0, count=keyEdge.length; i<count; i++) {
      //     if(i === X || keyEdge[i] === null)
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
      //     if(edgeVert[i] == X)
      //         throw("UGHHHH");
      // }

      // for(i=0, count=keyEdge.length; i<count; i++) {
      //     if(geometry.freeVerts.indexOf(i) > -1)
      //         continue;
      //     if(deleted[keyEdge[i]])
      //         throw("UGHHHH");
      // }


      // for(i=0, count=edgeVert.length; i<count; i++) {
      //     if(deleted[i])
      //         continue;
      //     if(edgeVert[i] == X)
      //         throw("UGHHHH");
      // }
    }

    public int SplitEdge(HalfEdge AB) {
      if(this.freeFaces.Count < 2 || this.freeVerts.Count == 0 || this.freeEdges.Count < 6)
        return -1; // this edge lives to see another day....for now

      //     before            after
      //        B                B
      //      .'|'.            .'|'.
      //    .'  |  '.        .'  |  '.
      //  L     |     R    L- - -X- - -R
      //    `.  |  .'        `.  |  .'
      //      `.|.'            `.|.'
      //        A                A

      HalfEdge BA = AB.pair;
      HalfEdge AR = BA.next;
      HalfEdge RB = AR.next;
      HalfEdge BL = AB.next;
      HalfEdge LA = BL.next;

      Debug.LogFormat("Splitting edge between verts [{1}->{2}]", BA.vert, AB.vert);

      this.RemoveEdge(AB);
      this.RemoveEdge(BA);

      HalfEdge AX = this.CreateEdge();
      HalfEdge XA = this.CreateEdge();
      HalfEdge XB = this.CreateEdge();
      HalfEdge BX = this.CreateEdge();
      HalfEdge XL = this.CreateEdge();
      HalfEdge LX = this.CreateEdge();
      HalfEdge XR = this.CreateEdge();
      HalfEdge RX = this.CreateEdge();

      int A = BA.vert;
      int B = AB.vert;
      int R = AR.vert;
      int L = BL.vert;
      int X = this.freeVerts.Pop();

      // update key edges
      this.vertKeyEdges[A] = AR;
      this.vertKeyEdges[B] = BL;
      this.vertKeyEdges[R] = RX;
      this.vertKeyEdges[L] = LX;
      this.vertKeyEdges[X] = XA;

      this.RemoveFace(AB.face);
      this.RemoveFace(BA.face);

      int AXL = this.CreateFace(A, X, L);
      int XBL = this.CreateFace(X, B, L);
      int ARX = this.CreateFace(A, R, X);
      int XRB = this.CreateFace(X, R, B);

      // update links on existing edges
      BL.Set(XBL, L, LX, XB, BL.pair);
      LA.Set(AXL, A, AX, XL, LA.pair);
      AR.Set(ARX, R, RX, XA, AR.pair);
      RB.Set(XRB, B, BX, XR, RB.pair);

      // integrate new edges
      AX.Set(AXL, X, XL, LA, XA);
      XA.Set(ARX, A, AR, RX, AX);
      XB.Set(XBL, B, BL, LX, BX);
      BX.Set(XRB, X, XR, RB, XB);
      XL.Set(AXL, L, LA, AX, LX);
      LX.Set(XBL, X, XB, BL, XL);
      XR.Set(XRB, R, RB, BX, RX);
      RX.Set(ARX, X, XA, AR, XR);

      return X;
    }
  }
}

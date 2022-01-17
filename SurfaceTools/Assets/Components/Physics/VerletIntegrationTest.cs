using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Doublemice.Physics {

  public class VerletIntegrationMass {
    public Transform transform;
    public Vector3 Velocity;
    // public Vector3 LastAcceleration;
    public Vector3 SpringForce;
    public Vector3 Acceleration;
    public Vector3 LastPosition;
    public Vector3 Position {
      get { return transform.localPosition; }
      set { transform.localPosition = value; }
    }
    public float Mass;
    public bool IsFixed;

    public VerletIntegrationMass(Transform transform, float mass, bool isFixed = false) {
      this.transform = transform;
      Velocity = Vector3.zero;
      // LastAcceleration = Vector3.zero;
      SpringForce = Vector3.zero;
      Acceleration = Vector3.zero;
      LastPosition = transform.localPosition;
      Mass = mass;
      IsFixed = isFixed;
      // transform = GameObject.Instantiate<Transform>(prefab);
      // transform.SetParent(chainRoot);
      // transform.localPosition = new Vector3(0f, -InitialLinkSpacing * i, 0f);
      // _chainLinks.Add(newLink);
    }
  }

  public class VerletDistanceConstraint {
    public VerletIntegrationMass A;
    public VerletIntegrationMass B;
    public float Length;

    public VerletDistanceConstraint(VerletIntegrationMass a, VerletIntegrationMass b, float distance) {
      A = a;
      B = b;
      Length = distance;
    }

    public void Relax(float relaxFactor) {
      Vector3 AB = B.Position - A.Position;
      float currentLength = AB.magnitude;
      if (Length >= currentLength)
        return;

      float combinedError = (Length - currentLength) * relaxFactor / currentLength;

      // Move each mass based on its fraction of the total mass.
      // If one of the masses is fixed, the other one must move the entire distance.
      // If both are fixed, we can't really do anything...
      if (!A.IsFixed && !B.IsFixed) {
        combinedError /= A.Mass + B.Mass;
        A.Position -= AB * (combinedError * B.Mass);
        B.Position += AB * (combinedError * A.Mass);
      } else if (!A.IsFixed) {
        A.Position -= AB * combinedError;
      } else if (!B.IsFixed) {
        B.Position += AB * combinedError;
      }
    }
  }

  public class VerletIntegrationTest : MonoBehaviour {

      public Transform ChainLinkPrefab;
      [Range(2, 100)]
      public int ChainLinkCount = 2;
      [Range(0.01f, 10f)]
      public float LinkSpacing = 0.2f;
      // public float MinSpringLength = 0.2f;
      public float ChainLinkMass = 1f;
      public float SpringConstant = 100f;
      public float ConstantDamping = 0.01f;
      public float QuadraticDrag = 0.1f;
      [Range(0f, 1f)]
      public float LinearDrag = 0.001f;
      // public bool UseVerlet = true;
      // public bool ConstrainLength = false;
      [Range(1, 20)]
      public int ConstraintIterations = 1;
      [Range(0f, 1f)]
      public float ConstraintRelaxFactor = 0.5f;

      public Vector3 Gravity = new Vector3(0f, -9.807f, 0f);

      private List<VerletIntegrationMass> _chainLinks;
      private List<VerletDistanceConstraint> _constraints;

      public void Awake() {

      }

      public void Start() {
        _chainLinks = new List<VerletIntegrationMass>(ChainLinkCount);
        _constraints = new List<VerletDistanceConstraint>(ChainLinkCount - 1);

        for (int i = 0; i < ChainLinkCount; i++) {
          Transform newLink = GameObject.Instantiate<Transform>(ChainLinkPrefab);
          newLink.SetParent(transform);
          newLink.localPosition = new Vector3(0f, -LinkSpacing * i, 0f);
          _chainLinks.Add(new VerletIntegrationMass(newLink, ChainLinkMass));
        }

        for (int i = 1; i < ChainLinkCount; i++) {
          _constraints.Add(new VerletDistanceConstraint(_chainLinks[i - 1], _chainLinks[i], LinkSpacing));
        }

        _chainLinks[0].IsFixed = true;
      }


      // private void IntegratePositionsEuler() {
      //   for (int i = 1; i < ChainLinkCount; i++) {
      //     VerletIntegrationMass link = _chainLinks[i];
      //     link.Velocity += link.Acceleration * Time.deltaTime;

      //     float velocityMagnitude = link.Velocity.magnitude;
      //     float dampingMagnitude = QuadraticDrag * Time.deltaTime * link.Velocity.magnitude * link.Velocity.magnitude / link.Mass;
      //     link.Velocity *= Mathf.Clamp((velocityMagnitude - dampingMagnitude) / velocityMagnitude, 0f, 1f - ConstantDamping);

      //     link.LastPosition = link.Position;
      //     link.Position += link.Velocity * Time.deltaTime;
      //   }
      // }

      private void IntegratePositionsVerlet() {
        for (int i = 1; i < ChainLinkCount; i++) {
          VerletIntegrationMass link = _chainLinks[i];

          Vector3 newAcceleration = ComputeAccelerationVerlet(link);

          Vector3 newPosition = (2f * link.Position) - link.LastPosition + (link.Acceleration * Time.deltaTime * Time.deltaTime);
          // Vector3 newVelocity = link.Velocity + (link.Acceleration + newAcceleration) * (0.5f * Time.deltaTime);

          // Vector3 newPosition = link.Position + (link.Velocity + (link.Acceleration * (0.5f * Time.deltaTime))) * Time.deltaTime;
          // Vector3 newVelocity = link.Velocity + (link.Acceleration + newAcceleration) * (0.5f * Time.deltaTime);

          newPosition.y = Mathf.Max(-transform.position.y, newPosition.y);

          // Vector3 dragForce = velocity * velocityMagnitude * -QuadraticDrag;

          // link.Position = Vector3.Lerp(link.Position, newPosition, 1f - LinearDrag);
          link.LastPosition = link.Position;
          link.Position = Vector3.Lerp(link.Position, newPosition, 1f - LinearDrag);
          link.Acceleration = newAcceleration;
        }
      }

      private void IntegratePositionsVelocityVerlet() {
        for (int i = 1; i < ChainLinkCount; i++) {
          VerletIntegrationMass link = _chainLinks[i];

          Vector3 newAcceleration = Gravity;
          // Vector3 newAcceleration = ComputeAccelerationVerlet(link);
          Vector3 newPosition = link.Position + (link.Velocity + (link.Acceleration * (0.5f * Time.deltaTime))) * Time.deltaTime;
          Vector3 newVelocity = link.Velocity + (link.Acceleration + newAcceleration) * (0.5f * Time.deltaTime);

          // Vector3 dragForce = velocity * velocityMagnitude * -QuadraticDrag;
          link.LastPosition = link.Position;
          // link.Position = Vector3.Lerp(link.Position, newPosition, 1f - LinearDrag);
          link.Position = newPosition;
          link.Velocity = newVelocity * (1f - LinearDrag);
          link.Acceleration = newAcceleration;
        }
      }

      private Vector3 ComputeAccelerationVerlet(VerletIntegrationMass link) {
        // Vector3 velocity =  (link.Position - link.LastPosition);
        // float velocityMagnitude = velocity.magnitude;

        // Vector3 dragForce = velocity * velocityMagnitude * -QuadraticDrag;
        // Vector3 acceleration = link.Acceleration = Gravity + ((link.SpringForce + dragForce)/ link.Mass);
        Vector3 acceleration = link.Acceleration = Gravity + (link.SpringForce/ link.Mass);

        return acceleration;
      }

      private void ComputeSpringForces() {
        for (int i = 1; i < ChainLinkCount; i++) {
          Vector3 linkDelta = _chainLinks[i].Position - _chainLinks[i - 1].Position;
          // if (linkDelta.sqrMagnitude > MinSpringLength) {
            Vector3 springForce = linkDelta * -SpringConstant;

            _chainLinks[i].SpringForce = springForce;
            _chainLinks[i - 1].SpringForce -= springForce;
          }
        // }
      }

      private void RelaxConstraints() {
        for (int i = 0; i < ConstraintIterations; i++) {
          foreach (VerletDistanceConstraint constraint in _constraints) {
            constraint.Relax(ConstraintRelaxFactor);
          }
        }
      }

      // private void ComputeAccelerations() {
      //   // Vector3 linkDelta = _chainLinks[0].Position; // the 0th node is actually the parent object

      //   // note we skip the first link as this is considered fixed

      //   for (int i = 1; i < ChainLinkCount; i++) {
      //     // _chainLinks[i].LastAcceleration = _chainLinks[i].Acceleration;
      //     _chainLinks[i].Acceleration = Gravity;
      //   }

      //   for (int i = 1; i < ChainLinkCount; i++) {
      //     Vector3 linkDelta = _chainLinks[i].Position - _chainLinks[i - 1].Position;
      //     if (linkDelta.sqrMagnitude > MinSpringLength) {

      //       Vector3 springForce = linkDelta * -SpringConstant;

      //       _chainLinks[i].Acceleration += springForce / _chainLinks[i].Mass;
      //       _chainLinks[i - 1].Acceleration -= springForce / _chainLinks[i - 1].Mass;


      //       // Vector3 linkAccel = _chainLinks[i].Acceleration;
      //       // if (linkAccel.magnitude > 10000f) {
      //       //   Debug.LogFormat("wtf: {0}", linkAccel);
      //       // }
      //     }

      //     // _chainLinks[i].localPosition = new Vector3(0f, -ChainLinkSpacing * i, 0f);
      //   }

      //   // _chainLinks[0].Acceleration = Vector3.zero;
      // }

      public void Update() {
        // remove extra links
        while (_chainLinks.Count > ChainLinkCount) {
          GameObject.Destroy(_chainLinks[_chainLinks.Count - 1].transform.gameObject);
          _chainLinks.RemoveAt(_chainLinks.Count - 1);
          _constraints.RemoveAt(_constraints.Count - 1);
          // _linkLengths.RemoveAt(_linkLengths.Count - 1);
        }

        while (_chainLinks.Count < ChainLinkCount) {
          VerletIntegrationMass lastLink = _chainLinks[_chainLinks.Count - 1];
          Vector3 delta = lastLink.Position - _chainLinks[_chainLinks.Count - 2].Position;
          Transform newLinkTransform = GameObject.Instantiate<Transform>(ChainLinkPrefab);
          newLinkTransform.SetParent(transform);
          newLinkTransform.localPosition = lastLink.Position + delta;
          VerletIntegrationMass newLink = new VerletIntegrationMass(newLinkTransform, ChainLinkMass);
          _chainLinks.Add(newLink);
          _constraints.Add(new VerletDistanceConstraint(lastLink, newLink, LinkSpacing));
        }


        // update masses and constraints if they've been changed in the UI
        for (int i = 1; i < ChainLinkCount; i++) {
          _chainLinks[i].Mass = ChainLinkMass;
          _constraints[i - 1].Length = LinkSpacing;
        }


        // if (UseVerlet) {
        // ComputeSpringForces();
        IntegratePositionsVerlet();
        RelaxConstraints();
        // } else {
        //   ComputeAccelerations();
        //   IntegratePositionsEuler();
        // }
      }

    public void OnDrawGizmos() {
      if (_constraints == null)
        return;

      Gizmos.color = Color.green;
      foreach (VerletDistanceConstraint constraint in _constraints) {
        Gizmos.DrawLine(constraint.A.transform.position, constraint.B.transform.position);
      }
      Gizmos.color = Color.white;
    }
  }
}

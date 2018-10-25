using UnityEngine;
using UnityEngine.EventSystems;

namespace Doublemice.UI {
  public class CameraController : BetterMonoBehaviour {

    public static Camera Camera { get; protected set; }

    public float panSmoothing = 8f;
    public float orbitSmoothing = 8f;
    public float zoomSmoothing = 6f;

    public float panSpeed = 8f;
    public float zoomSpeed = 0.5f;
    public float orbitSpeedX = 4f;
    public float orbitSpeedY = 3f;

    public float maxZoom = 3f;
    public float minZoom = 6f;
    public float minTheta = 35f;
    public float maxTheta = 75f;

    public float maxX = 5f;
    public float minX = -5f;
    public float maxZ = 5f;
    public float minZ = -5f;

    public float baseFOV = 35f;
    public float FOVFactor = 0.45f;

    public const int PAN_MOUSE_BUTTON = 1;
    public const int ORBIT_MOUSE_BUTTON = 2;

    // if we're right clicking to rotate the camera, that mouseup shouldn't
    // cancel any tools or whatever else right click is supposed to do.
    // however, if we happen to move the mouse just a little, it still counts.
    public float rightClickThreshhold = 20f;
    public static bool ignoreRightClick = false;

    Vector3 dragStartPoint = Vector3.zero;
    Vector3 rightClickStart;
    Camera cam;
    Vector3 focusTarget;
    Vector3 zoomTarget;
    Vector3 currentZoom;
    Vector3 eulerAnglesTarget;
    Quaternion rotationTarget;

    Plane groundPlane;

    bool mouseOverUI = false;
    bool panningStarted = false;
    bool orbitStarted = false;

    // TODO: there appears to be a unity bug when clicking BTN 1, then BTN 2,
    // then releasing BTN 2, BTN 1 outside the window. The state of BTN 1 as
    // reported by Input.GetMouseButton() never changes. idiots!

    public override void Start() {
      this.cam = this.GetComponentInChildren<Camera>();
      CameraController.Camera = this.cam;
      this.focusTarget = this.transform.position;
      this.zoomTarget = this.cam.transform.localPosition;
      this.currentZoom = this.cam.transform.localPosition;
      this.eulerAnglesTarget = this.transform.eulerAngles;
      this.rotationTarget = Quaternion.Euler(this.eulerAnglesTarget);
      this.cam.fieldOfView = this.baseFOV + this.FOVFactor * (this.maxTheta - this.eulerAnglesTarget.x);

      this.groundPlane = new Plane(Vector3.up, Vector3.zero);

      base.Start();
    }

    // right click to orbit
    // middle click to pan
    // mousewheel to zoom
    public void Update() {
      this.mouseOverUI = EventSystem.current.IsPointerOverGameObject(-1);

      // TODO: track start/stop drag events, so you can't start dragging over a menu and then
      // it gets all stupid when you mouse out onto the map
      this.UpdateFocusTarget();
      this.UpdateZoomTarget();
      this.UpdateRotationTarget();
      this.CheckRightClickThreshhold();

      // update camera position
      this.transform.position = Vector3.Lerp(
        this.transform.position,
        this.focusTarget,
        this.panSmoothing * Time.deltaTime
      );

      // update camera rotation
      Quaternion newRotation = Quaternion.Slerp(
        this.transform.rotation,
        this.rotationTarget,
        this.orbitSmoothing * Time.deltaTime
      );

      // change field of view based on polar angle,
      // and calculate the necessary dolly zoom to keep things the same size
      float newFOV = this.baseFOV + this.FOVFactor * (this.maxTheta - newRotation.eulerAngles.x);
      // changing the projection matrix is probably expensive, better not do it constantly
      if (Mathf.Abs(newFOV - this.cam.fieldOfView) > 0.01)
        this.cam.fieldOfView = newFOV;

      float fovCorrection = 0.5f / Mathf.Tan(this.cam.fieldOfView * Mathf.Deg2Rad * 0.5f);
      this.transform.rotation = newRotation;

      // update camera zoom distance
      this.currentZoom = Vector3.Lerp(
          this.currentZoom,
          this.zoomTarget,
          this.zoomSmoothing * Time.deltaTime
        );
      this.cam.transform.localPosition = this.currentZoom * fovCorrection;
    }

    protected void UpdateFocusTarget() {
      if (Input.GetMouseButton(CameraController.PAN_MOUSE_BUTTON)) {
        bool mouseBtnDown = Input.GetMouseButtonDown(CameraController.PAN_MOUSE_BUTTON);
        if (mouseBtnDown && !this.mouseOverUI)
          this.panningStarted = true;

        if (this.panningStarted) {
          Ray clickRay = this.cam.ScreenPointToRay(Input.mousePosition);
          float dist;
          if (this.groundPlane.Raycast(clickRay, out dist)) {
            Vector3 gridPoint = clickRay.GetPoint(dist);

            if (mouseBtnDown) {
              this.dragStartPoint = gridPoint;
              // change cursor?
            }
            this.focusTarget = this.transform.position + this.dragStartPoint - gridPoint;
            this.focusTarget.x = Mathf.Clamp(this.focusTarget.x, this.minX, this.maxX);
            this.focusTarget.z = Mathf.Clamp(this.focusTarget.z, this.minZ, this.maxZ);
          }
        }
      } else {
        this.panningStarted = false;
        // change cursor back?
      }

      Vector3 cameraForward = new Vector3(this.transform.forward.x, 0, this.transform.forward.z);
      Vector3 delta = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical")) * this.panSpeed * Time.deltaTime;
      delta = Quaternion.LookRotation(cameraForward) * delta;

      float clampedTargetX = Mathf.Clamp(this.focusTarget.x + delta.x, this.minX, this.maxX);
      float clampedTargetZ = Mathf.Clamp(this.focusTarget.z + delta.z, this.minZ, this.maxZ);

      // we can't just let the keyboard keep shifting dragStartPoint
      // forever if we've scrolled to the edge, or it will get all stupid
      this.dragStartPoint.x += clampedTargetX - this.focusTarget.x;
      this.dragStartPoint.z += clampedTargetZ - this.focusTarget.z;

      this.focusTarget.x = clampedTargetX;
      this.focusTarget.z = clampedTargetZ;
    }

    protected void UpdateZoomTarget() {
      if (!this.mouseOverUI) {
        float mouseWheel = Input.GetAxis("Mouse ScrollWheel");
        if (mouseWheel > 0) {
          // zoom in
          this.zoomTarget.z = Mathf.Clamp(this.zoomTarget.z + this.zoomSpeed, -this.minZoom, -this.maxZoom);
        } else if (mouseWheel < 0) {
          // zoom out
          this.zoomTarget.z = Mathf.Clamp(this.zoomTarget.z - this.zoomSpeed, -this.minZoom, -this.maxZoom);
        }
      }
    }

    protected void UpdateRotationTarget() {
      if (Input.GetMouseButton(CameraController.ORBIT_MOUSE_BUTTON)) {
        bool mouseBtnDown = Input.GetMouseButtonDown(CameraController.ORBIT_MOUSE_BUTTON);
        if (mouseBtnDown && !this.mouseOverUI)
          this.orbitStarted = true;

        if (this.orbitStarted) {
          this.eulerAnglesTarget.x -= Input.GetAxis("Mouse Y") * this.orbitSpeedY;
          this.eulerAnglesTarget.y += Input.GetAxis("Mouse X") * this.orbitSpeedX;

          this.eulerAnglesTarget.x = Mathf.Clamp(this.eulerAnglesTarget.x, this.minTheta, this.maxTheta);
          this.eulerAnglesTarget.y -= (Mathf.Floor(this.eulerAnglesTarget.y / 360f) * 360f); // normalize to 0-360 range

          this.rotationTarget = Quaternion.Euler(this.eulerAnglesTarget);
        }
      } else {
        this.orbitStarted = false;
      }
    }
    /// <summary>
    /// track how far the mouse has been dragged,
    /// so we know when to stop recognizing mouseUp as a click
    /// </summary>
    protected void CheckRightClickThreshhold() {
      if (Input.GetMouseButton(1)) {
        if (Input.GetMouseButtonDown(1))
          this.rightClickStart = Input.mousePosition;

        float mouseDist = Mathf.Abs(Input.mousePosition.x - this.rightClickStart.x) +
          Mathf.Abs(Input.mousePosition.y - this.rightClickStart.y);

        if (!CameraController.ignoreRightClick && mouseDist > this.rightClickThreshhold)
          CameraController.ignoreRightClick = true;

      } else if (!Input.GetMouseButtonUp(1)) {
        // we want to leave this true for the frame the mouse is released, otherwise it would be pointless
        CameraController.ignoreRightClick = false;
      }
    }
  }
}

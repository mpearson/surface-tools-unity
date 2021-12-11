using UnityEngine;
using UnityEngine.EventSystems;


#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Doublemice.UI
{
    public class CameraController : MonoBehaviour {
        public float PanSmoothing = 8f;
        public float OrbitSmoothing = 8f;
        public float ZoomSmoothing = 6f;

        public float FollowStrength = 1f;   // determines responsiveness of target following

        [Range(1f, 90f)]
        public float MaxFollowDeltaAngle = 60f;  // prevents the camera from trying to move too fast

        [Range(1f, 90f)]
        public float FollowAngleDeadZone = 20f;  // angle at which following is ~ 0.5x maximum speed

        public float PanSpeed = 8f;
        public float ZoomSpeed = 1.5f;
        public float OrbitSpeedX = 0.4f;
        public float OrbitSpeedY = 0.3f;

        public float MaxZoom = 5f;
        public float MinZoom = 45f;
        public float MinTheta = -80f;
        public float MaxTheta = 80f;

        public float MinX = -200f;
        public float MaxX = 200f;
        public float MinZ = -200f;
        public float MaxZ = 200f;

        public float FieldOfView = 40f;

        /// <summary>
        /// If we're right clicking to rotate the camera, that mouseup shouldn't
        /// cancel any tools or whatever else right click is supposed to do.
        /// however, if we happen to move the mouse just a little, it still counts.
        /// </summary>
        public float RightClickThreshhold = 20f;

        public static bool IgnoreRightClick = false;

        public string FocusOnCentroidWithTag = "";
        public Transform FocusObject;
        public bool LockPositionToObject = true;
        public bool FollowObjectYaw = true;
        public bool LookTowardsObject = true;

        private Vector3 _focusPositionTarget;
        private Vector3 _dragStartPoint;
        private Vector2 _rightClickStart;
        private Camera _cam;
        private Vector3 _zoomTarget;
        private Vector3 _currentZoom;
        private Vector3 _currentEulerAngles;
        private Vector3 _eulerAnglesTargetDelta;

        private Plane _groundPlane;

        // Prevents panning/rotating when interacting with the UI.
        private bool _mouseEnabled = true;
        private bool _panningStarted = false;
        private bool _orbitStarted = false;

        // Used for mouse tracking.
        #if !ENABLE_INPUT_SYSTEM
        private Vector3 _lastMousePosition = Vector3.zero;
        private Vector2 _mouseDelta = Vector2.zero;
        #endif

        // TODO: there appears to be a unity bug when clicking BTN 1, then BTN 2,
        // then releasing BTN 2, BTN 1 outside the window. The state of BTN 1 as
        // reported by Input.GetMouseButton() never changes. idiots!

        public void Start() {
            _cam = GetComponentInChildren<Camera>();
            _rightClickStart = Vector2.zero;
            _zoomTarget = _cam.transform.localPosition;
            _currentZoom = _cam.transform.localPosition;
            _currentEulerAngles = transform.rotation.eulerAngles;
            _eulerAnglesTargetDelta = Vector3.zero;
            _cam.fieldOfView = FieldOfView;

            _groundPlane = new Plane(Vector3.up, Vector3.zero);
        }

        public void Update()
        {
            // Calculate mouse deltas manually since the idiotic built-in API scales it by the user
            // sensitivity settings, which is neither deterministic nor accessible programmatically.
            #if !ENABLE_INPUT_SYSTEM
            _mouseDelta = Input.mousePosition - _lastMousePosition;
            _lastMousePosition = Input.mousePosition;
            #endif
        }

        public void LateUpdate()
        {
            _mouseEnabled = !EventSystem.current.IsPointerOverGameObject(-1);
            // _mouseEnabled = IsMouseInsideViewport() && !EventSystem.current.IsPointerOverGameObject(-1);

            if (!LockPositionToObject)
            {
                UpdatePositionTarget();
            }
            UpdateZoomTarget();
            // UpdateRotationTarget();
            if (LookTowardsObject)
            {
                LookTowardsTarget();
            }
            CheckRightClickThreshhold();

            TranslateTowardsTarget();
            RotateTowardsTarget();
            ZoomTowardsTarget();
        }

        // private int i = 0;

        // private bool IsMouseInsideViewport()
        // {
        //     // Vector3 mousePos = Input.mousePosition;
        //     #if ENABLE_INPUT_SYSTEM
        //     Vector2 mousePos = Mouse.current.position.ReadValue();
        //     #else
        //     Vector2 mousePos = Input.mousePosition;
        //     #endif
        //     Vector3 viewportPosition = _cam.ScreenToViewportPoint(mousePos);
        //     // if (i++ >= 2) {
        //     //     Debug.LogFormat("{0}, {1}, {2}", mousePos.x, mousePos.y, (
        //     //         viewportPosition.x >= 0f &&
        //     //             viewportPosition.x <= 1f &&
        //     //             viewportPosition.y >= 0f &&
        //     //             viewportPosition.y <= 1f
        //     //         ) ? "1" : "0"
        //     //     );
        //     //     i = 0;
        //     // }
        //     return (
        //         viewportPosition.x >= 0f &&
        //         viewportPosition.x <= 1f &&
        //         viewportPosition.y >= 0f &&
        //         viewportPosition.y <= 1f
        //     );
        // }

        private void RotateTowardsTarget()
        {
            // update camera rotation
            Vector3 eulerAnglesDelta = _eulerAnglesTargetDelta * (OrbitSmoothing * Time.deltaTime);
            _eulerAnglesTargetDelta -= eulerAnglesDelta;
            // Vector3 currentEulerAngles = transform.rotation.eulerAngles;

            if (_currentEulerAngles.x > 180f)
            {
                // We define the lower limit as a negative angle, but the eulerAngles property
                // returns a 0-360 range so we need to wrap it around in order to clamp the value.
                _currentEulerAngles.x -= 360f;
            }
            else if (_currentEulerAngles.x > 90f)
            {
                // It shouldn't be possible to get into this upside-down angle range,
                // but if somehow it happens just reset to 0...
                _currentEulerAngles.x = 0f;
            }

            _currentEulerAngles += eulerAnglesDelta;

            // Constrain the vertical angle.
            _currentEulerAngles.x = Mathf.Clamp(_currentEulerAngles.x, MinTheta, MaxTheta);

            // Prevent horizontal angle from going too far off the deep end.
            _currentEulerAngles.y = _currentEulerAngles.y % 360f;


            // Now add the target's yaw since we want to automatically follow as it turns.
            Vector3 absoluteEulerAngles = _currentEulerAngles;
            if (FocusObject != null && FollowObjectYaw)
            {
                absoluteEulerAngles.y += FocusObject.rotation.eulerAngles.y;
            }

            transform.rotation = Quaternion.Euler(absoluteEulerAngles);

        }

        /// <summary>
        /// Update position of camera focus point.
        /// </summary>
        private void TranslateTowardsTarget()
        {
            if (FocusObject != null && LockPositionToObject)
            {
                transform.position = FocusObject.position;
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, _focusPositionTarget, PanSmoothing * Time.deltaTime);
            }
        }


        /// <summary>
        /// Update camera zoom distance.
        /// </summary>
        private void ZoomTowardsTarget() {
            _currentZoom = Vector3.Lerp(
                _currentZoom,
                _zoomTarget,
                ZoomSmoothing * Time.deltaTime
            );
            _cam.transform.localPosition = _currentZoom;
        }

        private void UpdatePositionTarget() {
            #if ENABLE_INPUT_SYSTEM
            if (Mouse.current.rightButton.isPressed)
            #else
            if (Input.GetMouseButton(1))
            #endif
            {
                #if ENABLE_INPUT_SYSTEM
                if (Mouse.current.rightButton.wasPressedThisFrame && _mouseEnabled)
                #else
                if (Input.GetMouseButtonDown(1) && _mouseEnabled)
                #endif
                {
                    _panningStarted = true;
                }

                if (_panningStarted) {
                    #if ENABLE_INPUT_SYSTEM
                    Ray clickRay = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
                    #else
                    Ray clickRay = Camera.main.ScreenPointToRay(Input.mousePosition);
                    #endif
                    float dist;
                    if (_groundPlane.Raycast(clickRay, out dist)) {
                        Vector3 gridPoint = clickRay.GetPoint(dist);
                        #if ENABLE_INPUT_SYSTEM
                        if (Mouse.current.rightButton.wasPressedThisFrame)
                        #else
                        if (Input.GetMouseButtonDown(1))
                        #endif
                        {
                            _dragStartPoint = gridPoint;
                            // change cursor?
                        }
                        _focusPositionTarget = transform.position + _dragStartPoint - gridPoint;
                        _focusPositionTarget.x = Mathf.Clamp(_focusPositionTarget.x, this.MinX, this.MaxX);
                        _focusPositionTarget.z = Mathf.Clamp(_focusPositionTarget.z, this.MinZ, this.MaxZ);
                    }
                }
            } else {
                _panningStarted = false;
            }

            // Vector3 cameraForward = new Vector3(this.transform.forward.x, 0, this.transform.forward.z);
            // Vector3 delta = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical")) * PanSpeed * Time.deltaTime;
            // delta = Quaternion.LookRotation(cameraForward) * delta;

            // float clampedTargetX = Mathf.Clamp(this.focusTarget.x + delta.x, this.minX, this.maxX);
            // float clampedTargetZ = Mathf.Clamp(this.focusTarget.z + delta.z, this.minZ, this.maxZ);

            // we can't just let the keyboard keep shifting dragStartPoint
            // forever if we've scrolled to the edge, or it will get all stupid
            // _dragStartPoint.x += clampedTargetX - _focusTargetPosition.x;
            // _dragStartPoint.z += clampedTargetZ - _focusTargetPosition.z;

            // _focusTargetPosition.x = clampedTargetX;
            // _focusTargetPosition.z = clampedTargetZ;
        }

        private void UpdateZoomTarget() {
            if (_mouseEnabled) {
                #if ENABLE_INPUT_SYSTEM
                float mouseWheel = Mouse.current.scroll.y.ReadValue();
                #else
                float mouseWheel = -Input.mouseScrollDelta.y;
                #endif
                if (mouseWheel < 0) {
                    // zoom in
                    _zoomTarget.z = Mathf.Clamp(_zoomTarget.z + ZoomSpeed, -MinZoom, -MaxZoom);
                }
                else if (mouseWheel > 0) {
                    // zoom out
                    _zoomTarget.z = Mathf.Clamp(_zoomTarget.z - ZoomSpeed, -MinZoom, -MaxZoom);
                }
            }
        }

        public void OnMouseDown(InputAction.CallbackContext context) {
            // Debug.LogFormat("onmousedown: {0}", context.control.IsPressed());
            // Debug.LogFormat("onmousedown: {0}", context.action..IsPressed());
            if (context.control.IsPressed()) {
                Vector2 mousePos = ((Mouse)context.control.parent).position.ReadValue();
                if (_mouseEnabled && !_orbitStarted &&
                        mousePos.x > 0f && mousePos.x < Screen.width &&
                        mousePos.y > 0f && mousePos.y < Screen.height) {
                    _orbitStarted = true;
                }
            } else {
                _orbitStarted = false;
            }
            // Debug.LogFormat("mouse position: {0}", );
            // if (context.control.
            // if (_mouseEnabled && !_orbitStarted)
            //     _orbitStarted = true;


        }

        // public void OnMouseUp(InputAction.CallbackContext context)
        // {
        //     _orbitStarted = false;
        // }


        public void OnMouseMove(InputAction.CallbackContext context) {
            // #if ENABLE_INPUT_SYSTEM

            // if (Mouse.current.leftButton.isPressed)
            // {
            //     if (Mouse.current.leftButton.wasPressedThisFrame && _mouseEnabled)
            //     {
            //         _orbitStarted = true;
            //     }

                if (_orbitStarted) {
                    Vector2 delta = context.ReadValue<Vector2>();

                    _eulerAnglesTargetDelta.x -= delta.y * OrbitSpeedY;
                    _eulerAnglesTargetDelta.y += delta.x * OrbitSpeedX;
                }
            // }
            // else
            // {
            //     _orbitStarted = false;
            // }
        }

        private void UpdateRotationTarget() {
            #if ENABLE_INPUT_SYSTEM
            if (Mouse.current.leftButton.isPressed) {
            #else
            if (Input.GetMouseButton(0)) {
            #endif
                #if ENABLE_INPUT_SYSTEM
                if (Mouse.current.leftButton.wasPressedThisFrame && _mouseEnabled)
                #else
                if (Input.GetMouseButtonDown(0) && _mouseEnabled)
                #endif
                {
                    _orbitStarted = true;
                }


                if (_orbitStarted) {
                    #if ENABLE_INPUT_SYSTEM
                    _eulerAnglesTargetDelta.x -= Mouse.current.delta.y.ReadValue() * OrbitSpeedY;
                    _eulerAnglesTargetDelta.y += Mouse.current.delta.x.ReadValue() * OrbitSpeedX;
                    #else
                    _eulerAnglesTargetDelta.x -= _mouseDelta.y * OrbitSpeedY;
                    _eulerAnglesTargetDelta.y += _mouseDelta.x * OrbitSpeedX;
                    #endif
                }
            } else {
                _orbitStarted = false;
            }
        }

        /// <summary>
        /// Swivel the camera to face the focus target.
        /// This is a completely separate process from user-directed rotation and panning.
        /// </summary>
        private void LookTowardsTarget()
        {
            Vector3 targetPosition;
            if (FocusObject != null && !LockPositionToObject)
            {
                targetPosition = FocusObject.position;
            }
            else if (FocusOnCentroidWithTag != "")
            {
                GameObject[] objects = GameObject.FindGameObjectsWithTag(FocusOnCentroidWithTag);
                if (objects.Length > 0)
                {
                    targetPosition = Vector3.zero;
                    foreach (GameObject obj in objects)
                    {
                        targetPosition += obj.transform.position;
                    }
                    targetPosition /= (float)objects.Length;
                    }
                    else
                {
                    return;
                }
            }
            else
            {
                return;
            }

            // For this motion we want the camera to stay fixed, but the camera focus needs to
            // swing towards the target, rotating about the camera so the camera doesn't appear
            // to translate. This is the opposite of normal orbit mode.
            Vector3 cameraToTarget = targetPosition - _cam.transform.position;
            Vector2 cameraToTargetHorizontal = new Vector2(cameraToTarget.x, cameraToTarget.z);

            Vector3 cameraLookDirection = _cam.transform.forward;
            Vector2 cameraLookDirectionHorizontal = new Vector2(cameraLookDirection.x, cameraLookDirection.z);

            // Compute angle from current camera direction to the focus object
            float yawDeltaToTarget = -Vector2.SignedAngle(cameraLookDirectionHorizontal, cameraToTargetHorizontal);
            // Apply an easing function so the camera doesn't obsessively keep the target in the
            // exact center of the screen
            yawDeltaToTarget = MaxFollowDeltaAngle * (float)System.Math.Tanh(Mathf.Pow(yawDeltaToTarget / FollowAngleDeadZone, 3f));
            yawDeltaToTarget *= FollowStrength * Time.deltaTime;

            // Compute a position delta to rotate the focus point about the camera's position
            Vector3 cameraArmHorizontal = Vector3.ProjectOnPlane(transform.position - _cam.transform.position, Vector3.up);
            Vector3 focusPointDisplacement = (Quaternion.AngleAxis(yawDeltaToTarget, Vector3.up) * cameraArmHorizontal) - cameraArmHorizontal;

            if (_panningStarted)
            {
                _dragStartPoint += focusPointDisplacement;
            }
            transform.position += focusPointDisplacement;
            _focusPositionTarget += focusPointDisplacement;
            // This feels awkward, but it would be inefficient to compute euler angles and
            // quaternions both here and in RotateTowardsTarget()
            _currentEulerAngles.y += yawDeltaToTarget;
        }

        /// <summary>
        /// track how far the mouse has been dragged,
        /// so we know when to stop recognizing mouseUp as a click
        /// </summary>
        private void CheckRightClickThreshhold() {
            #if ENABLE_INPUT_SYSTEM
            if (Mouse.current.rightButton.isPressed)
            #else
            if (Input.GetMouseButton(1))
            #endif
            {
                #if ENABLE_INPUT_SYSTEM
                Vector2 mousePosition = Mouse.current.position.ReadValue();
                if (Mouse.current.rightButton.wasPressedThisFrame)
                #else
                Vector2 mousePosition = Input.mousePosition;
                if (Input.GetMouseButtonDown(1))
                #endif
                {
                    _rightClickStart = mousePosition;
                }

                float mouseDist = Mathf.Abs(mousePosition.x - _rightClickStart.x) +
                    Mathf.Abs(mousePosition.y - _rightClickStart.y);

                if (!CameraController.IgnoreRightClick && mouseDist > RightClickThreshhold) {
                    CameraController.IgnoreRightClick = true;
                }

            }
            #if ENABLE_INPUT_SYSTEM
            else if (!Mouse.current.rightButton.wasReleasedThisFrame)
            #else
            else if (!Input.GetMouseButtonDown(1))
            #endif
            {
                // we want to leave this true for the frame the mouse is released, otherwise it would be pointless
                CameraController.IgnoreRightClick = false;
            }
            // if (Input.GetMouseButton(1))
            // {
            //     if (Input.GetMouseButtonDown(1))
            //     {
            //         rightClickStart = Input.mousePosition;
            //     }

            //     float mouseDist = Mathf.Abs(Input.mousePosition.x - rightClickStart.x) +
            //         Mathf.Abs(Input.mousePosition.y - rightClickStart.y);

            //     if (!CameraController.ignoreRightClick && mouseDist > rightClickThreshhold)
            //     {
            //         CameraController.ignoreRightClick = true;
            //     }

            // }
            // else if (!Input.GetMouseButtonUp(1))
            // {
            //     // we want to leave this true for the frame the mouse is released, otherwise it would be pointless
            //     ThirdPersonCamera.ignoreRightClick = false;
            // }
        }
    }











//   public class CameraController : BetterMonoBehaviour {

//     public static Camera Camera { get; protected set; }

//     public float panSmoothing = 8f;
//     public float orbitSmoothing = 8f;
//     public float zoomSmoothing = 6f;

//     public float panSpeed = 8f;
//     public float zoomSpeed = 0.5f;
//     public float orbitSpeedX = 4f;
//     public float orbitSpeedY = 3f;

//     public float maxZoom = 3f;
//     public float minZoom = 6f;
//     public float minTheta = 35f;
//     public float maxTheta = 75f;

//     public float maxX = 5f;
//     public float minX = -5f;
//     public float maxZ = 5f;
//     public float minZ = -5f;

//     public bool enableDynamicFOV = true;
//     public float baseFOV = 35f;
//     public float FOVFactor = 0.45f;

//     public const int PAN_MOUSE_BUTTON = 1;
//     public const int ORBIT_MOUSE_BUTTON = 2;

//     // if we're right clicking to rotate the camera, that mouseup shouldn't
//     // cancel any tools or whatever else right click is supposed to do.
//     // however, if we happen to move the mouse just a little, it still counts.
//     public float rightClickThreshhold = 20f;
//     public static bool ignoreRightClick = false;

//     Vector3 dragStartPoint = Vector3.zero;
//     Vector3 rightClickStart;
//     Camera cam;
//     Vector3 focusTarget;
//     Vector3 zoomTarget;
//     Vector3 currentZoom;
//     Vector3 eulerAnglesTarget;
//     Quaternion rotationTarget;

//     Plane groundPlane;

//     bool mouseOverUI = false;
//     bool panningStarted = false;
//     bool orbitStarted = false;

//     // TODO: there appears to be a unity bug when clicking BTN 1, then BTN 2,
//     // then releasing BTN 2, BTN 1 outside the window. The state of BTN 1 as
//     // reported by Input.GetMouseButton() never changes. idiots!

//     public override void Start() {
//       this.cam = this.GetComponentInChildren<Camera>();
//       CameraController.Camera = this.cam;
//       this.focusTarget = this.transform.position;
//       this.zoomTarget = this.cam.transform.localPosition;
//       this.currentZoom = this.cam.transform.localPosition;
//       this.eulerAnglesTarget = this.transform.eulerAngles;
//       this.rotationTarget = Quaternion.Euler(this.eulerAnglesTarget);
//       this.cam.fieldOfView = this.baseFOV + this.FOVFactor * (this.maxTheta - this.eulerAnglesTarget.x);

//       this.groundPlane = new Plane(Vector3.up, Vector3.zero);

//       base.Start();
//     }

//     // right click to orbit
//     // middle click to pan
//     // mousewheel to zoom
//     public void Update() {
//       this.mouseOverUI = EventSystem.current.IsPointerOverGameObject(-1);

//       // TODO: track start/stop drag events, so you can't start dragging over a menu and then
//       // it gets all stupid when you mouse out onto the map
//       this.UpdateFocusTarget();
//       this.UpdateZoomTarget();
//       this.UpdateRotationTarget();
//       this.CheckRightClickThreshhold();

//       // update camera position
//       this.transform.position = Vector3.Lerp(
//         this.transform.position,
//         this.focusTarget,
//         this.panSmoothing * Time.deltaTime
//       );

//       // update camera rotation
//       Quaternion newRotation = Quaternion.Slerp(
//         this.transform.rotation,
//         this.rotationTarget,
//         this.orbitSmoothing * Time.deltaTime
//       );
//       float fovCorrection = 1f;
//       if (this.enableDynamicFOV) {
//         // change field of view based on polar angle,
//         // and calculate the necessary dolly zoom to keep things the same size
//         float newFOV = this.baseFOV + this.FOVFactor * (this.maxTheta - newRotation.eulerAngles.x);
//         // changing the projection matrix is probably expensive, better not do it constantly
//         if (Mathf.Abs(newFOV - this.cam.fieldOfView) > 0.01)
//           this.cam.fieldOfView = newFOV;
//         fovCorrection = 0.5f / Mathf.Tan(this.cam.fieldOfView * Mathf.Deg2Rad * 0.5f);
//       }
//       this.transform.rotation = newRotation;

//       // update camera zoom distance
//       this.currentZoom = Vector3.Lerp(
//         this.currentZoom,
//         this.zoomTarget,
//         this.zoomSmoothing * Time.deltaTime
//       );
//       this.cam.transform.localPosition = this.currentZoom * fovCorrection;
//     }

//     protected void UpdateFocusTarget() {
//       if (Input.GetMouseButton(CameraController.PAN_MOUSE_BUTTON)) {
//         bool mouseBtnDown = Input.GetMouseButtonDown(CameraController.PAN_MOUSE_BUTTON);
//         if (mouseBtnDown && !this.mouseOverUI)
//           this.panningStarted = true;

//         if (this.panningStarted) {
//           Ray clickRay = this.cam.ScreenPointToRay(Input.mousePosition);
//           float dist;
//           if (this.groundPlane.Raycast(clickRay, out dist)) {
//             Vector3 gridPoint = clickRay.GetPoint(dist);

//             if (mouseBtnDown) {
//               this.dragStartPoint = gridPoint;
//               // change cursor?
//             }
//             this.focusTarget = this.transform.position + this.dragStartPoint - gridPoint;
//             this.focusTarget.x = Mathf.Clamp(this.focusTarget.x, this.minX, this.maxX);
//             this.focusTarget.z = Mathf.Clamp(this.focusTarget.z, this.minZ, this.maxZ);
//           }
//         }
//       } else {
//         this.panningStarted = false;
//         // change cursor back?
//       }

//       Vector3 cameraForward = new Vector3(this.transform.forward.x, 0, this.transform.forward.z);
//       Vector3 delta = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical")) * this.panSpeed * Time.deltaTime;
//       delta = Quaternion.LookRotation(cameraForward) * delta;

//       float clampedTargetX = Mathf.Clamp(this.focusTarget.x + delta.x, this.minX, this.maxX);
//       float clampedTargetZ = Mathf.Clamp(this.focusTarget.z + delta.z, this.minZ, this.maxZ);

//       // we can't just let the keyboard keep shifting dragStartPoint
//       // forever if we've scrolled to the edge, or it will get all stupid
//       this.dragStartPoint.x += clampedTargetX - this.focusTarget.x;
//       this.dragStartPoint.z += clampedTargetZ - this.focusTarget.z;

//       this.focusTarget.x = clampedTargetX;
//       this.focusTarget.z = clampedTargetZ;
//     }

//     protected void UpdateZoomTarget() {
//       if (!this.mouseOverUI) {
//         float mouseWheel = Input.GetAxis("Mouse ScrollWheel");
//         if (mouseWheel > 0) {
//           // zoom in
//           this.zoomTarget.z = Mathf.Clamp(this.zoomTarget.z + this.zoomSpeed, -this.minZoom, -this.maxZoom);
//         } else if (mouseWheel < 0) {
//           // zoom out
//           this.zoomTarget.z = Mathf.Clamp(this.zoomTarget.z - this.zoomSpeed, -this.minZoom, -this.maxZoom);
//         }
//       }
//     }

//     protected void UpdateRotationTarget() {
//       if (Input.GetMouseButton(CameraController.ORBIT_MOUSE_BUTTON)) {
//         bool mouseBtnDown = Input.GetMouseButtonDown(CameraController.ORBIT_MOUSE_BUTTON);
//         if (mouseBtnDown && !this.mouseOverUI)
//           this.orbitStarted = true;

//         if (this.orbitStarted) {
//           this.eulerAnglesTarget.x -= Input.GetAxis("Mouse Y") * this.orbitSpeedY;
//           this.eulerAnglesTarget.y += Input.GetAxis("Mouse X") * this.orbitSpeedX;

//           this.eulerAnglesTarget.x = Mathf.Clamp(this.eulerAnglesTarget.x, this.minTheta, this.maxTheta);
//           this.eulerAnglesTarget.y -= (Mathf.Floor(this.eulerAnglesTarget.y / 360f) * 360f); // normalize to 0-360 range

//           this.rotationTarget = Quaternion.Euler(this.eulerAnglesTarget);
//         }
//       } else {
//         this.orbitStarted = false;
//       }
//     }
//     /// <summary>
//     /// track how far the mouse has been dragged,
//     /// so we know when to stop recognizing mouseUp as a click
//     /// </summary>
//     protected void CheckRightClickThreshhold() {
//       if (Input.GetMouseButton(1)) {
//         if (Input.GetMouseButtonDown(1))
//           this.rightClickStart = Input.mousePosition;

//         float mouseDist = Mathf.Abs(Input.mousePosition.x - this.rightClickStart.x) +
//           Mathf.Abs(Input.mousePosition.y - this.rightClickStart.y);

//         if (!CameraController.ignoreRightClick && mouseDist > this.rightClickThreshhold)
//           CameraController.ignoreRightClick = true;

//       } else if (!Input.GetMouseButtonUp(1)) {
//         // we want to leave this true for the frame the mouse is released, otherwise it would be pointless
//         CameraController.ignoreRightClick = false;
//       }
//     }
//   }
}
